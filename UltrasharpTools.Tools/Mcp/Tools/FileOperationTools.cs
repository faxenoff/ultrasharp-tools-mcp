using ModelContextProtocol;
using UltrasharpTools.Tools.Mcp;

namespace UltrasharpTools.Tools.Mcp.Tools;

/// <summary>
/// MCP tools for advanced file operations (split, synthesize).
/// Token-efficient operations for refactoring large files.
/// </summary>
public class FileOperationToolsLogCategory { }

[McpServerToolType]
public static partial class FileOperationTools
{
    /// <summary>
    /// Split a large C# file into multiple files by top-level types.
    /// </summary>
    [McpServerTool(
        Name = "split_file",
        Idempotent = false,
        ReadOnly = false,
        Destructive = false,
        OpenWorld = false
    )]
    [Description(
        "Split large C# file into separate files, one per top-level type (class/interface/enum/struct). "
            + "Preserves usings and namespaces. Automatically optimizes imports. Preview mode shows what will be created without making changes."
    )]
    public static async Task<object> SplitFile(
        ISolutionManager solutionManager,
        ImportUpdateService importUpdateService,
        ILogger<FileOperationToolsLogCategory> logger,
        [Description("Absolute path to .cs file to split")] string filePath,
        [Description("Target directory for split files (absolute)")] string targetDirectory,
        [Description("Preview mode (default: true, shows what will be created)")]
            bool preview = true,
        [Description("Delete original file after split (default: false)")]
            bool deleteOriginal = false,
        [Description("Auto-optimize imports in split files (default: true)")]
            bool optimizeImports = true,
        CancellationToken cancellationToken = default
    )
    {
        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(
            async () =>
            {
                ErrorHandlingHelpers.ValidateStringParameter(filePath, nameof(filePath), logger);
                ErrorHandlingHelpers.ValidateStringParameter(
                    targetDirectory,
                    nameof(targetDirectory),
                    logger
                );

                if (!File.Exists(filePath))
                {
                    throw new McpException($"File not found: {filePath}");
                }

                if (!filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    throw new McpException("Only .cs files are supported");
                }

                logger.LogInformation(
                    "Splitting file: {FilePath} -> {TargetDir} (preview={Preview})",
                    filePath,
                    targetDirectory,
                    preview
                );

                // Parse the file
                var sourceText = await File.ReadAllTextAsync(filePath, cancellationToken);
                var syntaxTree = CSharpSyntaxTree.ParseText(
                    sourceText,
                    cancellationToken: cancellationToken
                );
                var root =
                    await syntaxTree.GetRootAsync(cancellationToken) as CompilationUnitSyntax;

                if (root == null)
                {
                    throw new McpException("Failed to parse file");
                }

                // Extract top-level type declarations (including enums as MemberDeclarationSyntax)
                var allMembers = root
                    .Members.OfType<BaseNamespaceDeclarationSyntax>()
                    .SelectMany(ns => ns.Members)
                    .Concat(root.Members)
                    .Where(m => m is TypeDeclarationSyntax || m is EnumDeclarationSyntax)
                    .ToList();

                if (allMembers.Count == 0)
                {
                    throw new McpException("No type declarations found in file");
                }

                if (allMembers.Count == 1)
                {
                    return ToolHelpers.ToJson(
                        new
                        {
                            filePath,
                            message = "File contains only one type, split not needed",
                            typeCount = 1,
                        }
                    );
                }

                // Prepare split information
                var newFiles = allMembers
                    .Select(member =>
                    {
                        string typeName;
                        string typeKind;

                        if (member is EnumDeclarationSyntax enumDecl)
                        {
                            typeName = enumDecl.Identifier.Text;
                            typeKind = "enum";
                        }
                        else if (member is TypeDeclarationSyntax typeDecl)
                        {
                            typeName = typeDecl.Identifier.Text;
                            typeKind = typeDecl switch
                            {
                                ClassDeclarationSyntax => "class",
                                InterfaceDeclarationSyntax => "interface",
                                StructDeclarationSyntax => "struct",
                                RecordDeclarationSyntax => "record",
                                _ => "type",
                            };
                        }
                        else
                        {
                            typeName = "Unknown";
                            typeKind = "type";
                        }

                        var lineCount =
                            member.GetLocation().GetLineSpan().Span.End.Line
                            - member.GetLocation().GetLineSpan().Span.Start.Line
                            + 1;

                        return new
                        {
                            fileName = $"{typeName}.cs",
                            typeName,
                            typeKind,
                            lineCount,
                            targetPath = Path.Combine(targetDirectory, $"{typeName}.cs"),
                        };
                    })
                    .ToList();

                if (preview)
                {
                    return ToolHelpers.ToJson(
                        new
                        {
                            operation = "split",
                            mode = "preview",
                            currentFile = filePath,
                            targetDirectory,
                            typeCount = allMembers.Count,
                            newFiles,
                            deleteOriginal,
                            message = "Set preview=false to perform the split",
                        }
                    );
                }

                // Perform the split
                if (!Directory.Exists(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                var createdFiles = new List<string>();

                foreach (var member in allMembers)
                {
                    string typeName;
                    if (member is EnumDeclarationSyntax enumDecl)
                        typeName = enumDecl.Identifier.Text;
                    else if (member is TypeDeclarationSyntax typeDecl)
                        typeName = typeDecl.Identifier.Text;
                    else
                        continue;

                    var newFilePath = Path.Combine(targetDirectory, $"{typeName}.cs");

                    // Create new compilation unit
                    var newRoot = SyntaxFactory
                        .CompilationUnit()
                        .WithUsings(root.Usings)
                        .WithExterns(root.Externs);

                    // Preserve namespace structure
                    var parentNamespace = member
                        .Ancestors()
                        .OfType<BaseNamespaceDeclarationSyntax>()
                        .FirstOrDefault();
                    if (parentNamespace != null)
                    {
                        var newNamespace = SyntaxFactory
                            .FileScopedNamespaceDeclaration(parentNamespace.Name)
                            .AddMembers(member);
                        newRoot = newRoot.AddMembers(newNamespace);
                    }
                    else
                    {
                        newRoot = newRoot.AddMembers(member);
                    }

                    // Write formatted code
                    var formattedCode = newRoot.NormalizeWhitespace().ToFullString();
                    await File.WriteAllTextAsync(newFilePath, formattedCode, cancellationToken);
                    createdFiles.Add(newFilePath);

                    logger.LogInformation("Created file: {NewFile}", newFilePath);
                }

                // Optimize imports in new files if requested
                var importResults = new List<object>();
                if (optimizeImports && createdFiles.Count > 0)
                {
                    logger.LogInformation(
                        "Optimizing imports in {Count} files...",
                        createdFiles.Count
                    );

                    // Wait a bit for solution to reload files
                    await Task.Delay(500, cancellationToken);

                    foreach (var file in createdFiles)
                    {
                        try
                        {
                            var updateResult = await importUpdateService.UpdateUsingsAsync(
                                file,
                                removeUnused: true,
                                addMissing: true,
                                cancellationToken
                            );

                            importResults.Add(
                                new
                                {
                                    file = Path.GetFileName(file),
                                    success = updateResult.Success,
                                    usingsAdded = updateResult.UsingsAdded.Count,
                                    usingsRemoved = updateResult.UsingsRemoved.Count,
                                }
                            );

                            logger.LogDebug(
                                "Optimized imports for {File}: +{Added} -{Removed}",
                                Path.GetFileName(file),
                                updateResult.UsingsAdded.Count,
                                updateResult.UsingsRemoved.Count
                            );
                        }
                        catch (Exception ex)
                        {
                            logger.LogWarning(ex, "Failed to optimize imports for {File}", file);
                            importResults.Add(
                                new
                                {
                                    file = Path.GetFileName(file),
                                    success = false,
                                    error = ex.Message,
                                }
                            );
                        }
                    }
                }

                // Delete original if requested
                if (deleteOriginal && createdFiles.Count > 0)
                {
                    File.Delete(filePath);
                    logger.LogInformation("Deleted original file: {FilePath}", filePath);
                }

                return ToolHelpers.ToJson(
                    new
                    {
                        operation = "split",
                        mode = "applied",
                        originalFile = filePath,
                        targetDirectory,
                        filesCreated = createdFiles,
                        typeCount = allMembers.Count,
                        originalDeleted = deleteOriginal,
                        importsOptimized = optimizeImports,
                        importOptimization = optimizeImports ? importResults : null,
                        message = $"Successfully split into {createdFiles.Count} files"
                            + (
                                optimizeImports
                                    ? $", optimized imports in {importResults.Count(r => ((dynamic)r).success)} files"
                                    : ""
                            ),
                    }
                );
            },
            logger,
            nameof(SplitFile),
            cancellationToken
        );
    }

    /// <summary>
    /// Synthesize (combine) multiple C# files into a single file.
    /// </summary>
    [McpServerTool(
        Name = "synthesize_files",
        Idempotent = false,
        ReadOnly = false,
        Destructive = false,
        OpenWorld = false
    )]
    [Description(
        "Combine multiple C# files into a single file. Merges usings and types, preserving namespaces. "
            + "Automatically optimizes imports. Preview mode shows the result without creating files."
    )]
    public static async Task<object> SynthesizeFiles(
        ISolutionManager solutionManager,
        ImportUpdateService importUpdateService,
        ILogger<FileOperationToolsLogCategory> logger,
        [Description("Array of absolute paths to .cs files to combine")] string[] filePaths,
        [Description("Target file path (absolute)")] string targetFilePath,
        [Description("Preview mode (default: true, shows combined code)")] bool preview = true,
        [Description("Delete original files after synthesis (default: false)")]
            bool deleteOriginals = false,
        [Description("Auto-optimize imports in synthesized file (default: true)")]
            bool optimizeImports = true,
        CancellationToken cancellationToken = default
    )
    {
        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(
            async () =>
            {
                if (filePaths == null || filePaths.Length == 0)
                {
                    throw new McpException("No files specified");
                }

                if (filePaths.Length == 1)
                {
                    return ToolHelpers.ToJson(
                        new
                        {
                            message = "Only one file specified, synthesis not needed",
                            fileCount = 1,
                        }
                    );
                }

                ErrorHandlingHelpers.ValidateStringParameter(
                    targetFilePath,
                    nameof(targetFilePath),
                    logger
                );

                if (!targetFilePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                {
                    throw new McpException("Target file must have .cs extension");
                }

                logger.LogInformation(
                    "Synthesizing {Count} files into {Target} (preview={Preview})",
                    filePaths.Length,
                    targetFilePath,
                    preview
                );

                // Verify all files exist
                foreach (var file in filePaths)
                {
                    if (!File.Exists(file))
                    {
                        throw new McpException($"File not found: {file}");
                    }

                    if (!file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new McpException($"Only .cs files are supported: {file}");
                    }
                }

                // Parse all files
                var roots = new List<CompilationUnitSyntax>();
                foreach (var file in filePaths)
                {
                    var sourceText = await File.ReadAllTextAsync(file, cancellationToken);
                    var syntaxTree = CSharpSyntaxTree.ParseText(
                        sourceText,
                        cancellationToken: cancellationToken
                    );
                    var root =
                        await syntaxTree.GetRootAsync(cancellationToken) as CompilationUnitSyntax;

                    if (root != null)
                    {
                        roots.Add(root);
                    }
                }

                // Merge usings (distinct)
                var allUsings = roots
                    .SelectMany(r => r.Usings)
                    .GroupBy(u => u.Name?.ToString())
                    .Select(g => g.First())
                    .OrderBy(u => u.Name?.ToString())
                    .ToList();

                // Merge externs (distinct)
                var allExterns = roots
                    .SelectMany(r => r.Externs)
                    .GroupBy(e => e.ToString())
                    .Select(g => g.First())
                    .ToList();

                // Merge type declarations
                var allTypes = new List<TypeDeclarationSyntax>();
                var allNamespaces = new Dictionary<string, List<TypeDeclarationSyntax>>();

                foreach (var root in roots)
                {
                    // Types directly in root
                    allTypes.AddRange(root.Members.OfType<TypeDeclarationSyntax>());

                    // Types in namespaces
                    foreach (var ns in root.Members.OfType<BaseNamespaceDeclarationSyntax>())
                    {
                        var nsName = ns.Name.ToString();
                        if (!allNamespaces.TryGetValue(nsName, out List<TypeDeclarationSyntax>? value))
                        {
                            value = new List<TypeDeclarationSyntax>();
                            allNamespaces[nsName] = value;
                        }

                        value.AddRange(ns.Members.OfType<TypeDeclarationSyntax>());
                    }
                }

                // Create combined compilation unit
                var combinedRoot = SyntaxFactory
                    .CompilationUnit()
                    .WithUsings(SyntaxFactory.List(allUsings))
                    .WithExterns(SyntaxFactory.List(allExterns));

                // Add types from root
                combinedRoot = combinedRoot.AddMembers(allTypes.ToArray());

                // Add types from namespaces
                foreach (var (nsName, types) in allNamespaces)
                {
                    var nsDecl = SyntaxFactory
                        .FileScopedNamespaceDeclaration(SyntaxFactory.ParseName(nsName))
                        .AddMembers(types.ToArray());
                    combinedRoot = combinedRoot.AddMembers(nsDecl);
                }

                // Format the combined code
                var combinedCode = combinedRoot.NormalizeWhitespace().ToFullString();
                var lineCount = combinedCode.Split('\n').Length;

                if (preview)
                {
                    return ToolHelpers.ToJson(
                        new
                        {
                            operation = "synthesize",
                            mode = "preview",
                            sourceFiles = filePaths,
                            targetFilePath,
                            totalSourceFiles = filePaths.Length,
                            combinedLineCount = lineCount,
                            combinedUsings = allUsings.Count,
                            combinedTypes = allTypes.Count + allNamespaces.Values.Sum(v => v.Count),
                            namespaces = allNamespaces.Keys.ToList(),
                            deleteOriginals,
                            preview = combinedCode.Length > 10000
                                ? combinedCode.Substring(0, 10000)
                                    + "\n\n... (truncated, total "
                                    + lineCount
                                    + " lines)"
                                : combinedCode,
                            message = "Set preview=false to create the file",
                        }
                    );
                }

                // Write the combined file
                await File.WriteAllTextAsync(targetFilePath, combinedCode, cancellationToken);
                logger.LogInformation("Created combined file: {TargetFile}", targetFilePath);

                // Optimize imports in synthesized file if requested
                object? importOptimization = null;
                if (optimizeImports)
                {
                    logger.LogInformation("Optimizing imports in synthesized file...");

                    // Wait a bit for solution to reload file
                    await Task.Delay(500, cancellationToken);

                    try
                    {
                        var updateResult = await importUpdateService.UpdateUsingsAsync(
                            targetFilePath,
                            removeUnused: true,
                            addMissing: true,
                            cancellationToken
                        );

                        importOptimization = new
                        {
                            success = updateResult.Success,
                            usingsAdded = updateResult.UsingsAdded.Count,
                            usingsRemoved = updateResult.UsingsRemoved.Count,
                            message = updateResult.Message,
                        };

                        logger.LogInformation(
                            "Optimized imports: +{Added} -{Removed}",
                            updateResult.UsingsAdded.Count,
                            updateResult.UsingsRemoved.Count
                        );
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to optimize imports");
                        importOptimization = new { success = false, error = ex.Message };
                    }
                }

                // Delete originals if requested
                var deletedFiles = new List<string>();
                if (deleteOriginals)
                {
                    foreach (var file in filePaths)
                    {
                        File.Delete(file);
                        deletedFiles.Add(file);
                        logger.LogInformation("Deleted original file: {File}", file);
                    }
                }

                return ToolHelpers.ToJson(
                    new
                    {
                        operation = "synthesize",
                        mode = "applied",
                        sourceFiles = filePaths,
                        targetFilePath,
                        totalSourceFiles = filePaths.Length,
                        combinedLineCount = lineCount,
                        filesDeleted = deletedFiles,
                        importsOptimized = optimizeImports,
                        importOptimization,
                        message = $"Successfully synthesized {filePaths.Length} files into {targetFilePath}"
                            + (
                                optimizeImports && importOptimization != null
                                    ? ", imports optimized"
                                    : ""
                            ),
                    }
                );
            },
            logger,
            nameof(SynthesizeFiles),
            cancellationToken
        );
    }
}
