using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Microsoft.Extensions.FileSystemGlobbing;
using ModelContextProtocol;
using UltrasharpTools.Tools.Infrastructure;
using UltrasharpTools.Tools.Mcp;
using UltrasharpTools.Tools.Models;

namespace UltrasharpTools.Tools.Mcp.Tools;

// Marker class for ILogger<T> category specific to ModificationTools
public class ModificationToolsLogCategory { }

[McpServerToolType]
public static class ModificationTools {
    [McpServerTool(
        Name = "add_member",
        Idempotent = false,
        Destructive = false,
        OpenWorld = false,
        ReadOnly = false
    )]
    [Description(
        "Adds one or more new member definitions (Property, Field, Method, inner Class, etc.) to a specified type. Code is parsed, inserted, and formatted. Definition can include xml documentation and attributes. Writing small components produces cleaner code, so you can use this to break up large components, in addition to adding new functionality."
    )]
    public static async Task<string> AddMember(
        ISolutionManager solutionManager,
        ICodeModificationService modificationService,
        IComplexityAnalysisService complexityAnalysisService,
        ISemanticSimilarityService semanticSimilarityService,
        UltrasharpTools.Tools.Preview.PreviewManager previewManager,
        ILogger<ModificationToolsLogCategory> logger,
        [Description("FQN of the parent type or method.")] string fullyQualifiedTargetName,
        [Description("The C# code to add.")] string codeSnippet,
        [Description(
            "If the target is a partial type, specifies which file to add to. Set to 'auto' to determine automatically."
        )]
            string fileNameHint,
        [Description(
            "Suggest a line number to insert the member near. '-1' to determine automatically."
        )]
            int lineNumberHint,
        string commitMessage,
        [Description(
            "Preview mode: if true, returns diff without applying changes (default: false)"
        )]
            bool preview = false,
        CancellationToken cancellationToken = default
    ) {
        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(
            async () => {
                // Validate parameters
                ErrorHandlingHelpers.ValidateStringParameter(
                    fullyQualifiedTargetName,
                    "fullyQualifiedTargetName",
                    logger
                );
                ErrorHandlingHelpers.ValidateStringParameter(codeSnippet, "codeSnippet", logger);
                codeSnippet = codeSnippet.TrimBackslash();

                // Ensure solution is loaded
                await ToolHelpers.EnsureSolutionLoadedOrAutoLoadAsync(
                    solutionManager,
                    logger,
                    nameof(AddMember),
                    cancellationToken
                );
                logger.LogInformation(
                    "Executing '{AddMember}' for target: {TargetName} (preview={Preview})",
                    nameof(AddMember),
                    fullyQualifiedTargetName,
                    preview
                );

                // Preview mode: return diff without applying changes
                if (preview) {
                    try {
                        var previewResult = await previewManager.PreviewAddMemberAsync(
                            fullyQualifiedTargetName,
                            codeSnippet,
                            cancellationToken
                        );

                        return ToolHelpers.ToJson(
                            new {
                                mode = "preview",
                                operation = previewResult.Operation,
                                targetFqn = previewResult.TargetFqn,
                                filesAffected = previewResult.FilesAffected,
                                diff = previewResult.Diff,
                                impact = new {
                                    entitiesAffected = previewResult
                                        .EstimatedImpact
                                        .EntitiesAffected,
                                    referencesAffected = previewResult
                                        .EstimatedImpact
                                        .ReferencesAffected,
                                    breakingChange = previewResult.EstimatedImpact.BreakingChange,
                                },
                                message = "Preview mode: no changes applied. Set preview=false to apply changes.",
                            }
                        );
                    } catch (Exception ex)
                          when (ex is not McpException && ex is not OperationCanceledException) {
                        logger.LogError(
                            ex,
                            "Preview generation failed for {TargetName}",
                            fullyQualifiedTargetName
                        );
                        throw new McpException($"Preview generation failed: {ex.Message}");
                    }
                }

                // Get the target symbol
                var targetSymbol = await ToolHelpers.GetRoslynSymbolOrThrowAsync(
                    solutionManager,
                    fullyQualifiedTargetName,
                    cancellationToken
                );

                SyntaxReference? targetSyntaxRef = null;
                if (!string.IsNullOrEmpty(fileNameHint) && fileNameHint != "auto") {
                    targetSyntaxRef = targetSymbol.DeclaringSyntaxReferences.FirstOrDefault(sr =>
                        sr.SyntaxTree.FilePath != null
                        && sr.SyntaxTree.FilePath.Contains(fileNameHint)
                    );

                    if (targetSyntaxRef == null) {
                        throw new McpException(
                            $"File hint '{fileNameHint}' did not match any declaring syntax reference for symbol '{fullyQualifiedTargetName}'."
                        );
                    }
                } else {
                    targetSyntaxRef = targetSymbol.DeclaringSyntaxReferences.FirstOrDefault();
                }
                if (targetSyntaxRef == null) {
                    throw new McpException(
                        $"Could not find a suitable syntax reference for symbol '{fullyQualifiedTargetName}'."
                    );
                }

                if (solutionManager.CurrentSolution == null) {
                    throw new McpException(
                        "Current solution is unexpectedly null after validation checks."
                    );
                }

                var syntaxNode = await targetSyntaxRef.GetSyntaxAsync(cancellationToken);
                var document = ToolHelpers.GetDocumentFromSyntaxNodeOrThrow(
                    solutionManager.CurrentSolution,
                    syntaxNode
                );

                if (targetSymbol is not INamedTypeSymbol typeSymbol) {
                    throw new McpException(
                        $"Target '{fullyQualifiedTargetName}' is not a type, cannot add member."
                    );
                }

                // Parse the code snippet
                MemberDeclarationSyntax? memberSyntax;
                try {
                    memberSyntax = SyntaxFactory.ParseMemberDeclaration(codeSnippet);
                    if (memberSyntax == null) {
                        throw new McpException(
                            "Failed to parse code snippet as a valid member declaration."
                        );
                    }
                } catch (Exception ex)
                      when (!(ex is McpException || ex is OperationCanceledException)) {
                    logger.LogError(ex, "Failed to parse code snippet as member declaration");
                    throw new McpException($"Invalid C# syntax in code snippet: {ex.Message}");
                }

                // Verify the member name doesn't already exist in the type
                string memberName = GetMemberName(memberSyntax);
                logger.LogInformation("Adding member with name: {MemberName}", memberName);

                // Check for duplicate members
                if (!IsMemberAllowed(typeSymbol, memberSyntax, memberName, cancellationToken)) {
                    throw new McpException(
                        $"A member with the name '{memberName}' already exists in '{fullyQualifiedTargetName}'"
                            + (
                                memberSyntax is MethodDeclarationSyntax
                                    ? " with the same parameter signature."
                                    : "."
                            )
                    );
                }

                try {
                    // Use the lineNumberHint parameter when calling AddMemberAsync
                    var newSolution = await modificationService.AddMemberAsync(
                        document.Id,
                        typeSymbol,
                        memberSyntax,
                        lineNumberHint,
                        cancellationToken
                    );

                    string finalCommitMessage =
                        $"Add {memberName} to {typeSymbol.Name}: " + commitMessage;
                    var lintingResult = await modificationService.ApplyChangesAsync(
                        newSolution,
                        cancellationToken,
                        finalCommitMessage
                    );

                    // Check for compilation errors after adding the code
                    var updatedDocument = solutionManager.CurrentSolution.GetDocument(document.Id);
                    if (updatedDocument is null) {
                        logger.LogError(
                            "Updated document for {TargetName} is null after applying changes",
                            fullyQualifiedTargetName
                        );
                        throw new McpException(
                            $"Failed to retrieve updated document for {fullyQualifiedTargetName} after applying changes."
                        );
                    }
                    var (hasErrors, errorMessages) =
                        await ContextInjectors.CheckCompilationErrorsAsync(
                            solutionManager,
                            updatedDocument,
                            logger,
                            cancellationToken
                        );

                    // Perform complexity and similarity analysis on the added member
                    string analysisResults = string.Empty;

                    // Get the updated type symbol to find the newly added member
                    var updatedSemanticModel = await updatedDocument.GetSemanticModelAsync(
                        cancellationToken
                    );
                    if (updatedSemanticModel != null) {
                        // Find the type symbol in the updated document by FQN instead of using old syntax reference
                        var updatedTypeSymbol =
                            await ToolHelpers.GetRoslynSymbolOrThrowAsync(
                                solutionManager,
                                fullyQualifiedTargetName,
                                cancellationToken
                            ) as INamedTypeSymbol;

                        if (updatedTypeSymbol != null) {
                            // Find the added member by name
                            var addedSymbol = updatedTypeSymbol
                                .GetMembers(memberName)
                                .FirstOrDefault();
                            if (addedSymbol != null) {
                                analysisResults =
                                    await MemberAnalysisHelper.AnalyzeAddedMemberAsync(
                                        addedSymbol,
                                        complexityAnalysisService,
                                        semanticSimilarityService,
                                        logger,
                                        cancellationToken
                                    );
                            }
                        }
                    }

                    // Add linting results to the response
                    string lintOutput = LintingHelper.FormatLintingResults(lintingResult);

                    string baseMessage =
                        $"Successfully added member to {fullyQualifiedTargetName} in {document.FilePath ?? "unknown file"}.\n\n"
                        + (
                            (!hasErrors)
                                ? "<errorCheck>No compilation issues detected.</errorCheck>"
                                : (
                                    $"{errorMessages}\n"
                                    + //Code added is not necessary in Copilot, as it can see the invocation  $"\nCode added:\n{codeSnippet}\n\n" +
                                    $"If you choose to fix these issues, you must use {ToolHelpers.SharpToolPrefix + nameof(OverwriteMember)} to replace the member with a new definition."
                                )
                        );

                    string finalMessage = baseMessage;
                    if (!string.IsNullOrWhiteSpace(analysisResults)) {
                        finalMessage += $"\n\n{analysisResults}";
                    }
                    if (!string.IsNullOrWhiteSpace(lintOutput)) {
                        finalMessage += lintOutput;
                    }

                    return finalMessage;
                } catch (Exception ex)
                      when (!(ex is McpException || ex is OperationCanceledException)) {
                    logger.LogError(
                        ex,
                        "Failed to add member to {TypeName}",
                        fullyQualifiedTargetName
                    );
                    throw new McpException(
                        $"Failed to add member to {fullyQualifiedTargetName}: {ex.Message}"
                    );
                }
            },
            logger,
            nameof(AddMember),
            cancellationToken
        );
    }

    private static string GetMemberName(MemberDeclarationSyntax memberSyntax) {
        return memberSyntax switch {
            MethodDeclarationSyntax method => method.Identifier.Text,
            ConstructorDeclarationSyntax ctor => ctor.Identifier.Text,
            DestructorDeclarationSyntax dtor => dtor.Identifier.Text,
            OperatorDeclarationSyntax op => op.OperatorToken.Text,
            ConversionOperatorDeclarationSyntax conv => conv.Type.ToString(),
            PropertyDeclarationSyntax property => property.Identifier.Text,
            FieldDeclarationSyntax field => field.Declaration.Variables.First().Identifier.Text,
            EnumDeclarationSyntax enumDecl => enumDecl.Identifier.Text,
            TypeDeclarationSyntax type => type.Identifier.Text,
            DelegateDeclarationSyntax del => del.Identifier.Text,
            EventDeclarationSyntax evt => evt.Identifier.Text,
            EventFieldDeclarationSyntax evtField => evtField
                .Declaration.Variables.First()
                .Identifier.Text,
            IndexerDeclarationSyntax indexer => "this[]", // Indexers don't have names but use the 'this' keyword
            _ => throw new NotSupportedException(
                $"Unsupported member type: {memberSyntax.GetType().Name}"
            ),
        };
    }

    // Helper method to check if the member is allowed to be added
    private static bool IsMemberAllowed(
        INamedTypeSymbol typeSymbol,
        MemberDeclarationSyntax newMember,
        string memberName,
        CancellationToken cancellationToken
    ) {
        // Special handling for method overloads
        if (newMember is MethodDeclarationSyntax newMethod) {
            // Get all existing methods with the same name
            var existingMethods = typeSymbol
                .GetMembers(memberName)
                .OfType<IMethodSymbol>()
                .Where(m => !m.IsImplicitlyDeclared && m.MethodKind == MethodKind.Ordinary)
                .ToList();

            if (existingMethods.Count == 0) {
                return true; // No method with the same name exists
            }

            // Convert parameters of the new method to comparable format
            var newMethodParams = newMethod
                .ParameterList.Parameters.Select(p => new {
                    Type = p.Type?.ToString() ?? "unknown",
                    IsRef = p.Modifiers.Any(m => m.IsKind(SyntaxKind.RefKeyword)),
                    IsOut = p.Modifiers.Any(m => m.IsKind(SyntaxKind.OutKeyword)),
                })
                .ToList();

            // Check if any existing method has the same parameter signature
            foreach (var existingMethod in existingMethods) {
                if (existingMethod.Parameters.Length != newMethodParams.Count) {
                    continue; // Different parameter count, not a duplicate
                }

                bool signatureMatches = true;
                for (int i = 0; i < existingMethod.Parameters.Length; i++) {
                    var existingParam = existingMethod.Parameters[i];
                    var newParam = newMethodParams[i];

                    // Compare parameter types and ref/out modifiers
                    if (
                        existingParam.Type.ToDisplayString() != newParam.Type
                        || existingParam.RefKind == RefKind.Ref != newParam.IsRef
                        || existingParam.RefKind == RefKind.Out != newParam.IsOut
                    ) {
                        signatureMatches = false;
                        break;
                    }
                }

                if (signatureMatches) {
                    return false; // Found a method with the same signature
                }
            }

            return true; // No matching signature found
        } else {
            // For non-method members, simply check if a member with the same name exists
            return !typeSymbol.GetMembers(memberName).Any(m => !m.IsImplicitlyDeclared);
        }
    }
    [McpServerTool(
        Name = "modify_code",
        Idempotent = false,
        Destructive = true,
        OpenWorld = false,
        ReadOnly = false
    )]
    [Description(
        "Replaces the definition of an existing member or type with new C# code, or deletes it. Code is parsed and formatted. Code can contain multiple new members, update the existing member, and/or replace it with a new one."
    )]
    public static async Task<string> OverwriteMember(
        ISolutionManager solutionManager,
        ICodeModificationService modificationService,
        UltrasharpTools.Tools.Preview.PreviewManager previewManager,
        ILogger<ModificationToolsLogCategory> logger,
        [Description("FQN of the member or type to rewrite.")] string fullyQualifiedMemberName,
        [Description(
        "The new C# code for the member or type. *If this member has attributes or XML documentation, they MUST be included here.* To Delete the target instead, set this to `// Delete {memberName}`."
    )]
    string newMemberCode,
        string commitMessage,
        [Description(
        "Preview mode: if true, returns diff without applying changes (default: false)"
    )]
    bool preview = false,
        CancellationToken cancellationToken = default
    ) {
        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(
            async () => {
                ErrorHandlingHelpers.ValidateStringParameter(
                    fullyQualifiedMemberName,
                    nameof(fullyQualifiedMemberName),
                    logger
                );
                ErrorHandlingHelpers.ValidateStringParameter(
                    newMemberCode,
                    nameof(newMemberCode),
                    logger
                );
                newMemberCode = newMemberCode.TrimBackslash();

                await ToolHelpers.EnsureSolutionLoadedOrAutoLoadAsync(
                    solutionManager,
                    logger,
                    nameof(OverwriteMember),
                    cancellationToken
                );
                logger.LogInformation(
                    "Executing '{OverwriteMember}' for: {SymbolName} (preview={Preview})",
                    nameof(OverwriteMember),
                    fullyQualifiedMemberName,
                    preview
                );

                // Preview mode: return diff without applying changes
                if (preview) {
                    try {
                        var previewResult = await previewManager.PreviewCodeModificationAsync(
                            fullyQualifiedMemberName,
                            newMemberCode,
                            cancellationToken
                        );

                        return ToolHelpers.ToJson(
                            new {
                                mode = "preview",
                                operation = previewResult.Operation,
                                targetFqn = previewResult.TargetFqn,
                                filesAffected = previewResult.FilesAffected,
                                diff = previewResult.Diff,
                                impact = new {
                                    entitiesAffected = previewResult
                                        .EstimatedImpact
                                        .EntitiesAffected,
                                    referencesAffected = previewResult
                                        .EstimatedImpact
                                        .ReferencesAffected,
                                    breakingChange = previewResult.EstimatedImpact.BreakingChange,
                                },
                                message = "Preview mode: no changes applied. Set preview=false to apply changes.",
                            }
                        );
                    } catch (Exception ex)
                      when (ex is not McpException && ex is not OperationCanceledException) {
                        logger.LogError(
                            ex,
                            "Preview generation failed for {SymbolName}",
                            fullyQualifiedMemberName
                        );
                        throw new McpException($"Preview generation failed: {ex.Message}");
                    }
                }

                var symbol = await ToolHelpers.GetRoslynSymbolOrThrowAsync(
                    solutionManager,
                    fullyQualifiedMemberName,
                    cancellationToken
                );

                if (!symbol.DeclaringSyntaxReferences.Any()) {
                    throw new McpException(
                        $"Symbol '{fullyQualifiedMemberName}' has no declaring syntax references."
                    );
                }

                var syntaxRef = symbol.DeclaringSyntaxReferences.First();
                var oldNode = await syntaxRef.GetSyntaxAsync(cancellationToken);

                if (solutionManager.CurrentSolution is null) {
                    throw new McpException(
                        "Current solution is unexpectedly null after validation checks."
                    );
                }

                var document = ToolHelpers.GetDocumentFromSyntaxNodeOrThrow(
                    solutionManager.CurrentSolution,
                    oldNode
                );

                // Save file path before changes - document ID may change after ApplyChangesAsync
                var documentFilePath = document.FilePath;

                if (oldNode is not MemberDeclarationSyntax && oldNode is not TypeDeclarationSyntax) {
                    throw new McpException(
                        $"Symbol '{fullyQualifiedMemberName}' does not represent a replaceable member or type."
                    );
                }

                // Get a simple name for the symbol for the commit message
                string symbolName = symbol.Name;

                bool isDelete = newMemberCode.StartsWith(
                    "// Delete",
                    StringComparison.OrdinalIgnoreCase
                );
                string finalCommitMessage =
                    (isDelete ? $"Delete {symbolName}" : $"Update {symbolName}")
                    + ": "
                    + commitMessage;
                if (isDelete) {
                    var commentTrivia = SyntaxFactory.Comment(newMemberCode);
                    var emptyNode = SyntaxFactory
                        .EmptyStatement()
                        .WithLeadingTrivia(commentTrivia)
                        .WithTrailingTrivia(SyntaxFactory.EndOfLine("\n"));

                    try {
                        var newSolution = await modificationService.ReplaceNodeAsync(
                            document.Id,
                            oldNode,
                            emptyNode,
                            cancellationToken
                        );
                        await modificationService.ApplyChangesAsync(
                            newSolution,
                            cancellationToken,
                            finalCommitMessage
                        );

                        // Find document by file path after changes (ID may have changed)
                        Document? updatedDocument = null;
                        if (!string.IsNullOrEmpty(documentFilePath) && solutionManager.CurrentSolution != null) {
                            var docIds = solutionManager.CurrentSolution.GetDocumentIdsWithFilePath(documentFilePath);
                            updatedDocument = docIds.FirstOrDefault() is { } docId
                                ? solutionManager.CurrentSolution.GetDocument(docId)
                                : null;
                        }

                        if (updatedDocument != null) {
                            var (hasErrors, errorMessages) =
                                await ContextInjectors.CheckCompilationErrorsAsync(
                                    solutionManager,
                                    updatedDocument,
                                    logger,
                                    cancellationToken
                                );
                            if (!hasErrors)
                                errorMessages =
                                    "<errorCheck>No compilation issues detected.</errorCheck>";

                            return $"Successfully deleted symbol {fullyQualifiedMemberName}.\n\n{errorMessages}";
                        }
                        return $"Successfully deleted symbol {fullyQualifiedMemberName}";
                    } catch (Exception ex)
                      when (ex is not McpException && ex is not OperationCanceledException) {
                        logger.LogError(
                            ex,
                            "Failed to delete symbol {SymbolName}",
                            fullyQualifiedMemberName
                        );
                        throw new McpException(
                            $"Failed to delete symbol {fullyQualifiedMemberName}: {ex.Message}"
                        );
                    }
                }

                SyntaxNode? newNode;
                try {
                    var parsedCode = SyntaxFactory.ParseCompilationUnit(newMemberCode);
                    newNode = parsedCode.Members.FirstOrDefault();

                    if (newNode is null) {
                        throw new McpException(
                            "Failed to parse new code as a valid member or type declaration. The parsed result was empty."
                        );
                    }

                    // Validate that the parsed node is of an expected type if the original was a TypeDeclaration
                    if (oldNode is TypeDeclarationSyntax && newNode is not TypeDeclarationSyntax) {
                        throw new McpException(
                            $"The new code for '{fullyQualifiedMemberName}' was parsed as a {newNode.Kind()}, but a TypeDeclaration was expected to replace the existing TypeDeclaration."
                        );
                    }
                    // Validate that the parsed node is of an expected type if the original was a MemberDeclaration (but not a TypeDeclaration, which is a subtype)
                    else if (
                        oldNode is MemberDeclarationSyntax
                        && oldNode is not TypeDeclarationSyntax
                        && newNode is not MemberDeclarationSyntax
                    ) {
                        throw new McpException(
                            $"The new code for '{fullyQualifiedMemberName}' was parsed as a {newNode.Kind()}, but a MemberDeclaration was expected to replace the existing MemberDeclaration."
                        );
                    }
                } catch (Exception ex)
                  when (ex is not McpException && ex is not OperationCanceledException) {
                    logger.LogError(
                        ex,
                        "Failed to parse replacement code for {SymbolName}",
                        fullyQualifiedMemberName
                    );
                    throw new McpException($"Invalid C# syntax in replacement code: {ex.Message}");
                }

                if (newNode is null) { // Should be caught by earlier checks, but as a safeguard.
                    throw new McpException(
                        "Critical error: Failed to parse new code and newNode is null."
                    );
                }

                try {
                    var newSolution = await modificationService.ReplaceNodeAsync(
                        document.Id,
                        oldNode,
                        newNode,
                        cancellationToken
                    );
                    await modificationService.ApplyChangesAsync(
                        newSolution,
                        cancellationToken,
                        finalCommitMessage
                    );

                    if (solutionManager.CurrentSolution is null) {
                        throw new McpException(
                            "Current solution is unexpectedly null after applying changes."
                        );
                    }

                    // Generate diff using the centralized ContextInjectors
                    var diffResult = ContextInjectors.CreateCodeDiff(
                        oldNode.ToFullString(),
                        newNode.ToFullString()
                    );

                    // Find document by file path after changes (ID may have changed)
                    Document? updatedDocument = null;
                    if (!string.IsNullOrEmpty(documentFilePath)) {
                        var docIds = solutionManager.CurrentSolution.GetDocumentIdsWithFilePath(documentFilePath);
                        updatedDocument = docIds.FirstOrDefault() is { } docId
                            ? solutionManager.CurrentSolution.GetDocument(docId)
                            : null;
                    }

                    if (updatedDocument is null) {
                        // Document not found, but changes were applied successfully - log warning and return success
                        logger.LogWarning(
                            "Could not retrieve updated document for {SymbolName} after applying changes (file: {FilePath}). Skipping compilation check.",
                            fullyQualifiedMemberName,
                            documentFilePath
                        );
                        return $"Successfully replaced symbol {fullyQualifiedMemberName}.\n\n{diffResult}\n\nNote: Could not verify compilation - document not found in updated solution.";
                    }

                    var (hasErrors, errorMessages) =
                        await ContextInjectors.CheckCompilationErrorsAsync(
                            solutionManager,
                            updatedDocument,
                            logger,
                            cancellationToken
                        );
                    if (!hasErrors)
                        errorMessages = "<errorCheck>No compilation issues detected.</errorCheck>";

                    return $"Successfully replaced symbol {fullyQualifiedMemberName}.\n\n{diffResult}\n\n{errorMessages}";
                } catch (Exception ex)
                  when (ex is not McpException && ex is not OperationCanceledException) {
                    logger.LogError(
                        ex,
                        "Failed to replace symbol {SymbolName}",
                        fullyQualifiedMemberName
                    );
                    throw new McpException(
                        $"Failed to replace symbol {fullyQualifiedMemberName}: {ex.Message}"
                    );
                }
            },
            logger,
            nameof(OverwriteMember),
            cancellationToken
        );
    }
    [
        McpServerTool(
            Name = "rename_symbol",
            Idempotent = true,
            Destructive = true,
            OpenWorld = false,
            ReadOnly = false
        ),
        Description(
            "Renames a symbol (variable, method, property, type) and updates all references. Changes are formatted."
        )
    ]
    public static async Task<string> RenameSymbol(
        ISolutionManager solutionManager,
        ICodeModificationService modificationService,
        UltrasharpTools.Tools.Preview.PreviewManager previewManager,
        ILogger<ModificationToolsLogCategory> logger,
        [Description("FQN of the symbol to rename.")] string fullyQualifiedSymbolName,
        [Description("The new name for the symbol.")] string newName,
        string commitMessage,
        [Description(
            "Preview mode: if true, returns impact analysis without applying changes (default: false)"
        )]
            bool preview = false,
        CancellationToken cancellationToken = default
    ) {
        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(
            async () => {
                // Validate parameters
                ErrorHandlingHelpers.ValidateStringParameter(
                    fullyQualifiedSymbolName,
                    "fullyQualifiedSymbolName",
                    logger
                );
                ErrorHandlingHelpers.ValidateStringParameter(newName, "newName", logger);

                // Validate that the new name is a valid C# identifier
                if (!IsValidCSharpIdentifier(newName)) {
                    throw new McpException(
                        $"'{newName}' is not a valid C# identifier for renaming."
                    );
                }

                // Ensure solution is loaded
                await ToolHelpers.EnsureSolutionLoadedOrAutoLoadAsync(
                    solutionManager,
                    logger,
                    nameof(RenameSymbol),
                    cancellationToken
                );
                logger.LogInformation(
                    "Executing '{RenameSymbol}' for {SymbolName} to {NewName} (preview={Preview})",
                    nameof(RenameSymbol),
                    fullyQualifiedSymbolName,
                    newName,
                    preview
                );

                // Preview mode: return impact analysis without applying changes
                if (preview) {
                    try {
                        var previewResult = await previewManager.PreviewRenameSymbolAsync(
                            fullyQualifiedSymbolName,
                            newName,
                            cancellationToken
                        );

                        return ToolHelpers.ToJson(
                            new {
                                mode = "preview",
                                operation = previewResult.Operation,
                                targetFqn = previewResult.TargetFqn,
                                oldName = previewResult.OldCode,
                                newName = previewResult.NewCode,
                                filesAffected = previewResult.FilesAffected.Length,
                                affectedFiles = previewResult.FilesAffected,
                                diff = previewResult.Diff,
                                impact = new {
                                    entitiesAffected = previewResult
                                        .EstimatedImpact
                                        .EntitiesAffected,
                                    referencesAffected = previewResult
                                        .EstimatedImpact
                                        .ReferencesAffected,
                                    breakingChange = previewResult.EstimatedImpact.BreakingChange,
                                },
                                message = "Preview mode: no changes applied. Set preview=false to apply changes.",
                            }
                        );
                    } catch (Exception ex)
                          when (ex is not McpException && ex is not OperationCanceledException) {
                        logger.LogError(
                            ex,
                            "Preview generation failed for {SymbolName}",
                            fullyQualifiedSymbolName
                        );
                        throw new McpException($"Preview generation failed: {ex.Message}");
                    }
                }

                // Get the symbol to rename
                var symbol = await ToolHelpers.GetRoslynSymbolOrThrowAsync(
                    solutionManager,
                    fullyQualifiedSymbolName,
                    cancellationToken
                );

                // Check if symbol is renamable
                if (symbol.IsImplicitlyDeclared) {
                    throw new McpException(
                        $"Cannot rename implicitly declared symbol '{fullyQualifiedSymbolName}'."
                    );
                }

                string finalCommitMessage = $"Rename {symbol.Name} to {newName}: " + commitMessage;

                try {
                    // Perform the rename operation
                    var newSolution = await modificationService.RenameSymbolAsync(
                        symbol,
                        newName,
                        cancellationToken
                    );

                    // Check if the operation actually made changes
                    var changeset = newSolution.GetChanges(solutionManager.CurrentSolution!);
                    var changedDocumentCount = changeset
                        .GetProjectChanges()
                        .Sum(p => p.GetChangedDocuments().Count());

                    if (changedDocumentCount == 0) {
                        logger.LogWarning(
                            "Rename operation for {SymbolName} to {NewName} produced no changes",
                            fullyQualifiedSymbolName,
                            newName
                        );
                    }

                    // Apply the changes with the commit message
                    await modificationService.ApplyChangesAsync(
                        newSolution,
                        cancellationToken,
                        finalCommitMessage
                    );

                    // Check for compilation errors after renaming the symbol using centralized ContextInjectors
                    // Get the first few affected documents to check
                    var affectedDocumentIds = changeset
                        .GetProjectChanges()
                        .SelectMany(pc => pc.GetChangedDocuments())
                        .Take(5) // Limit to first 5 documents to avoid excessive checking
                        .ToList();

                    var errorBuilder = ObjectPoolProvider.Instance.GetStringBuilder();
                    errorBuilder.Append("<errorCheck>");

                    try {
                        // Check each affected document for compilation errors
                        foreach (var docId in affectedDocumentIds) {
                            if (solutionManager.CurrentSolution != null) {
                                var updatedDoc = solutionManager.CurrentSolution.GetDocument(docId);
                                if (updatedDoc != null) {
                                    var (docHasErrors, docErrorMessages) =
                                        await ContextInjectors.CheckCompilationErrorsAsync(
                                            solutionManager,
                                            updatedDoc,
                                            logger,
                                            cancellationToken
                                        );

                                    if (docHasErrors) {
                                        errorBuilder.AppendLine(
                                            $"Issues in file {updatedDoc.FilePath ?? "unknown"}:"
                                        );
                                        errorBuilder.AppendLine(docErrorMessages);
                                        errorBuilder.AppendLine();
                                    } else {
                                        errorBuilder.AppendLine(
                                            $"No compilation issues in file {updatedDoc.FilePath ?? "unknown"}."
                                        );
                                    }
                                }
                            }
                        }
                        errorBuilder.AppendLine("</errorCheck>");

                        var errorCheckResult = errorBuilder.ToString();
                        return $"Symbol '{symbol.Name}' (originally '{fullyQualifiedSymbolName}') successfully renamed to '{newName}' and references updated in {changedDocumentCount} documents.\n\n{errorCheckResult}";
                    } finally {
                        ObjectPoolProvider.Instance.ReturnStringBuilder(errorBuilder);
                    }
                } catch (InvalidOperationException ex) {
                    logger.LogError(
                        ex,
                        "Invalid rename operation for {SymbolName} to {NewName}",
                        fullyQualifiedSymbolName,
                        newName
                    );
                    throw new McpException(
                        $"Cannot rename symbol '{fullyQualifiedSymbolName}' to '{newName}': {ex.Message}"
                    );
                } catch (Exception ex)
                      when (!(ex is McpException || ex is OperationCanceledException)) {
                    logger.LogError(
                        ex,
                        "Failed to rename symbol {SymbolName} to {NewName}",
                        fullyQualifiedSymbolName,
                        newName
                    );
                    throw new McpException(
                        $"Failed to rename symbol '{fullyQualifiedSymbolName}' to '{newName}': {ex.Message}"
                    );
                }
            },
            logger,
            nameof(RenameSymbol),
            cancellationToken
        );
    }

    // Helper method to check if a string is a valid C# identifier
    private static bool IsValidCSharpIdentifier(string name) {
        return SyntaxFacts.IsValidIdentifier(name);
    }

    private static readonly string[] operation = new[]
                    {
                        "variable",
                        "method",
                        "class",
                        "property",
                        "field",
                        "invocation",
                        "parameter",
                        "type",
                        "identifier",
                    };

    [McpServerTool(
        Name = "replace_all_references",
        Idempotent = false,
        Destructive = true,
        OpenWorld = false,
        ReadOnly = false
    )]
    [Description(
        "Surgically replaces all references to a symbol with new C# code across the solution. Perfect for systematic API upgrades - e.g., replacing all Console.WriteLine() calls with Logger.Info(). Supports filtering by file patterns, syntax node types, and excluding comments."
    )]
    public static async Task<string> ReplaceAllReferences(
        ISolutionManager solutionManager,
        ICodeModificationService modificationService,
        ILogger<ModificationToolsLogCategory> logger,
        [Description("FQN of the symbol whose references should be replaced.")]
            string fullyQualifiedSymbolName,
        [Description("The C# code replace references with.")] string replacementCode,
        [Description(
            "Only replace symbols in files with this pattern. Supports globbing (`*`). Empty = all files."
        )]
            string filenameFilter = "",
        [Description(
            "Only replace specific syntax node types. Options: 'variable', 'method', 'class', 'property', 'field', 'invocation', 'parameter', 'type'. Empty array = all types. Example: ['invocation', 'variable']"
        )]
            string[]? syntaxNodeKinds = null,
        [Description("If true, exclude replacements in comments and strings (default: true)")]
            bool excludeCommentsAndStrings = true,
        [Description("If true, only preview changes without applying them (default: true)")]
            bool preview = true,
        [Description("Commit message (only used when preview=false)")] string commitMessage = "",
        CancellationToken cancellationToken = default
    ) {
        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(
            async () => {
                // Validate parameters
                ErrorHandlingHelpers.ValidateStringParameter(
                    fullyQualifiedSymbolName,
                    "fullyQualifiedSymbolName",
                    logger
                );
                ErrorHandlingHelpers.ValidateStringParameter(
                    replacementCode,
                    "replacementCode",
                    logger
                );
                replacementCode = replacementCode.TrimBackslash();

                // Note: filenameFilter can be empty or null, as this indicates "replace in all files"

                // Validate commitMessage only when not in preview mode
                if (!preview && string.IsNullOrWhiteSpace(commitMessage)) {
                    throw new McpException("commitMessage is required when preview=false");
                }

                // Ensure solution is loaded
                await ToolHelpers.EnsureSolutionLoadedOrAutoLoadAsync(
                    solutionManager,
                    logger,
                    nameof(ReplaceAllReferences),
                    cancellationToken
                );
                logger.LogInformation(
                    "Executing '{ReplaceAllReferences}' for {SymbolName} with text '{ReplacementCode}', filter: {Filter}, preview: {Preview}",
                    nameof(ReplaceAllReferences),
                    fullyQualifiedSymbolName,
                    replacementCode,
                    filenameFilter ?? "none",
                    preview
                );

                // Get the symbol whose references will be replaced
                var symbol = await ToolHelpers.GetRoslynSymbolOrThrowAsync(
                    solutionManager,
                    fullyQualifiedSymbolName,
                    cancellationToken
                );

                // Create a shortened version of the replacement code for the commit message
                string shortReplacementCode =
                    replacementCode.Length > 30
                        ? string.Concat(replacementCode.AsSpan(0, 30), "...")
                        : replacementCode;

                string finalCommitMessage =
                    $"Replace references to {symbol.Name} with {shortReplacementCode}: "
                    + commitMessage;

                // Validate that the replacement code can be parsed as a valid C# expression
                try {
                    var expressionSyntax = SyntaxFactory.ParseExpression(replacementCode);
                    if (expressionSyntax == null) {
                        logger.LogWarning(
                            "Replacement code '{ReplacementCode}' may not be a valid C# expression",
                            replacementCode
                        );
                    }
                } catch (Exception ex) {
                    logger.LogWarning(
                        ex,
                        "Replacement code '{ReplacementCode}' could not be parsed as a C# expression",
                        replacementCode
                    );
                    // We don't throw here, because some valid replacements might not be valid expressions on their own
                }

                // Create combined predicate filter
                Func<SyntaxNode, bool>? predicateFilter = null;

                // Setup filename filter if provided
                Matcher? fileMatcher = null;
                string? fileRoot = null;
                if (!string.IsNullOrEmpty(filenameFilter)) {
                    fileMatcher = new Matcher(StringComparison.OrdinalIgnoreCase);
                    string normalizedFilter = filenameFilter.Replace('\\', '/');
                    fileMatcher.AddInclude(normalizedFilter);
                    fileRoot =
                        Path.GetPathRoot(solutionManager.CurrentSolution?.FilePath)
                        ?? Path.GetPathRoot(Environment.CurrentDirectory)!;
                }

                // Setup syntax node kind filter
                HashSet<string>? nodeKindSet = null;
                if (syntaxNodeKinds != null && syntaxNodeKinds.Length > 0) {
                    nodeKindSet = new HashSet<string>(
                        syntaxNodeKinds,
                        StringComparer.OrdinalIgnoreCase
                    );

                    // Validate node kinds
                    var validKinds = operation;
                    foreach (var kind in syntaxNodeKinds) {
                        if (!validKinds.Contains(kind.ToLowerInvariant())) {
                            logger.LogWarning(
                                "Unknown syntax node kind: {Kind}. Valid kinds: {ValidKinds}",
                                kind,
                                string.Join(", ", validKinds)
                            );
                        }
                    }
                }

                // Create combined filter
                if (fileMatcher != null || nodeKindSet != null || excludeCommentsAndStrings) {
                    predicateFilter = node => {
                        try {
                            // Check filename filter
                            if (fileMatcher != null && fileRoot != null) {
                                var location = node.GetLocation();
                                if (
                                    location == null
                                    || string.IsNullOrWhiteSpace(location.SourceTree?.FilePath)
                                ) {
                                    return false;
                                }
                                string filePath = location.SourceTree.FilePath;
                                if (!fileMatcher.Match(fileRoot, filePath).HasMatches) {
                                    return false;
                                }
                            }

                            // Check if node is in comment or string
                            if (excludeCommentsAndStrings) {
                                // Check if any ancestor is a string or comment
                                var current = node;
                                while (current != null) {
                                    if (
                                        current.IsKind(SyntaxKind.StringLiteralExpression)
                                        || current.IsKind(SyntaxKind.InterpolatedStringExpression)
                                        || current.IsKind(SyntaxKind.SingleLineCommentTrivia)
                                        || current.IsKind(SyntaxKind.MultiLineCommentTrivia)
                                        || current.IsKind(
                                            SyntaxKind.SingleLineDocumentationCommentTrivia
                                        )
                                        || current.IsKind(
                                            SyntaxKind.MultiLineDocumentationCommentTrivia
                                        )
                                    ) {
                                        return false;
                                    }
                                    current = current.Parent;
                                }
                            }

                            // Check syntax node kind filter
                            if (nodeKindSet != null) {
                                bool matchesKind = false;

                                if (
                                    nodeKindSet.Contains("variable")
                                    && (
                                        node is VariableDeclaratorSyntax
                                        || node.Parent is VariableDeclaratorSyntax
                                    )
                                ) {
                                    matchesKind = true;
                                } else if (
                                      nodeKindSet.Contains("method")
                                      && (
                                          node is MethodDeclarationSyntax
                                          || node.Parent is MethodDeclarationSyntax
                                      )
                                  ) {
                                    matchesKind = true;
                                } else if (
                                      nodeKindSet.Contains("class")
                                      && (
                                          node is ClassDeclarationSyntax
                                          || node.Parent is ClassDeclarationSyntax
                                      )
                                  ) {
                                    matchesKind = true;
                                } else if (
                                      nodeKindSet.Contains("property")
                                      && (
                                          node is PropertyDeclarationSyntax
                                          || node.Parent is PropertyDeclarationSyntax
                                      )
                                  ) {
                                    matchesKind = true;
                                } else if (
                                      nodeKindSet.Contains("field")
                                      && (
                                          node is FieldDeclarationSyntax
                                          || node.Parent is FieldDeclarationSyntax
                                      )
                                  ) {
                                    matchesKind = true;
                                } else if (
                                      nodeKindSet.Contains("invocation")
                                      && (
                                          node is InvocationExpressionSyntax
                                          || node.Parent is InvocationExpressionSyntax
                                      )
                                  ) {
                                    matchesKind = true;
                                } else if (
                                      nodeKindSet.Contains("parameter")
                                      && (node is ParameterSyntax || node.Parent is ParameterSyntax)
                                  ) {
                                    matchesKind = true;
                                } else if (
                                      nodeKindSet.Contains("type")
                                      && (
                                          node is TypeDeclarationSyntax
                                          || node.Parent is TypeDeclarationSyntax
                                      )
                                  ) {
                                    matchesKind = true;
                                } else if (
                                      nodeKindSet.Contains("identifier")
                                      && node is IdentifierNameSyntax
                                  ) {
                                    matchesKind = true;
                                }

                                if (!matchesKind) {
                                    return false;
                                }
                            }

                            return true;
                        } catch (Exception ex) {
                            logger.LogWarning(ex, "Error applying filter to node");
                            return false;
                        }
                    };
                }

                try {
                    // Replace all references to the symbol with the new code
                    var newSolution = await modificationService.ReplaceAllReferencesAsync(
                        symbol,
                        replacementCode,
                        cancellationToken,
                        predicateFilter
                    );

                    // Count changes before applying them
                    if (solutionManager.CurrentSolution == null) {
                        throw new McpException(
                            "Current solution is null after replacement operation."
                        );
                    }

                    var originalSolution = solutionManager.CurrentSolution;
                    var solutionChanges = newSolution.GetChanges(originalSolution);
                    var changedDocumentsCount = solutionChanges
                        .GetProjectChanges()
                        .SelectMany(pc => pc.GetChangedDocuments())
                        .Count();

                    if (changedDocumentsCount == 0) {
                        logger.LogWarning(
                            "No documents were changed when replacing references to '{SymbolName}'",
                            fullyQualifiedSymbolName
                        );

                        var filterDesc = ObjectPoolProvider.Instance.GetStringBuilder();
                        try {
                            if (!string.IsNullOrEmpty(filenameFilter))
                                filterDesc.Append($" matching '{filenameFilter}'");
                            if (syntaxNodeKinds != null && syntaxNodeKinds.Length > 0)
                                filterDesc.Append(
                                    $" of types [{string.Join(", ", syntaxNodeKinds)}]"
                                );

                            var filterDescStr = filterDesc.ToString();
                            return $"No references to '{symbol.Name}' found{filterDescStr}. No changes were made.";
                        } finally {
                            ObjectPoolProvider.Instance.ReturnStringBuilder(filterDesc);
                        }
                    }

                    // Preview mode: show what would be changed without applying
                    if (preview) {
                        var previewOutput = ObjectPoolProvider.Instance.GetStringBuilder();
                        try {
                            previewOutput.AppendLine(
                                $"## Preview: Replace references to '{symbol.Name}' with '{replacementCode}'"
                            );
                            previewOutput.AppendLine();
                            previewOutput.AppendLine(
                                $"**Would modify {changedDocumentsCount} document(s)**"
                            );
                            previewOutput.AppendLine();

                            // Show active filters
                            if (!string.IsNullOrEmpty(filenameFilter))
                                previewOutput.AppendLine($"• File filter: `{filenameFilter}`");
                            if (syntaxNodeKinds != null && syntaxNodeKinds.Length > 0)
                                previewOutput.AppendLine(
                                    $"• Node types: `{string.Join(", ", syntaxNodeKinds)}`"
                                );
                            if (excludeCommentsAndStrings)
                                previewOutput.AppendLine($"• Excluding comments and strings");
                            previewOutput.AppendLine();

                            previewOutput.AppendLine("### Changed files:");

                            var changedDocs = solutionChanges
                                .GetProjectChanges()
                                .SelectMany(pc => pc.GetChangedDocuments())
                                .Take(20)
                                .ToList();

                            foreach (var docId in changedDocs) {
                                var doc = originalSolution.GetDocument(docId);
                                var newDoc = newSolution.GetDocument(docId);
                                if (doc != null && newDoc != null) {
                                    previewOutput.AppendLine($"  • {doc.FilePath ?? doc.Name}");
                                }
                            }

                            if (changedDocumentsCount > 20) {
                                previewOutput.AppendLine(
                                    $"  ... and {changedDocumentsCount - 20} more file(s)"
                                );
                            }

                            previewOutput.AppendLine();
                            previewOutput.AppendLine(
                                "**⚠️ Preview mode:** No changes were applied."
                            );
                            previewOutput.AppendLine(
                                "**💡 Tip:** Set `preview=false` and provide `commitMessage` to apply the changes."
                            );

                            return previewOutput.ToString();
                        } finally {
                            ObjectPoolProvider.Instance.ReturnStringBuilder(previewOutput);
                        }
                    }

                    // Apply the changes
                    await modificationService.ApplyChangesAsync(
                        newSolution,
                        cancellationToken,
                        finalCommitMessage
                    );

                    // Check for compilation errors in changed documents
                    var changedDocIds = solutionChanges
                        .GetProjectChanges()
                        .SelectMany(pc => pc.GetChangedDocuments())
                        .Take(5) // Limit to first 5 documents to avoid excessive checking
                        .ToList();

                    var errorBuilder = ObjectPoolProvider.Instance.GetStringBuilder();
                    errorBuilder.Append("<errorCheck>");

                    try {
                        // Check each affected document for compilation errors
                        foreach (var docId in changedDocIds) {
                            if (solutionManager.CurrentSolution != null) {
                                var updatedDoc = solutionManager.CurrentSolution.GetDocument(docId);
                                if (updatedDoc != null) {
                                    var (docHasErrors, docErrorMessages) =
                                        await ContextInjectors.CheckCompilationErrorsAsync(
                                            solutionManager,
                                            updatedDoc,
                                            logger,
                                            cancellationToken
                                        );

                                    if (docHasErrors) {
                                        errorBuilder.AppendLine(
                                            $"Issues in file {updatedDoc.FilePath ?? "unknown"}:"
                                        );
                                        errorBuilder.AppendLine(docErrorMessages);
                                        errorBuilder.AppendLine();
                                    } else {
                                        errorBuilder.AppendLine(
                                            $"No compilation issues in file {updatedDoc.FilePath ?? "unknown"}."
                                        );
                                    }
                                }
                            }
                        }
                        errorBuilder.AppendLine("</errorCheck>");

                        var filterMessage = string.IsNullOrEmpty(filenameFilter)
                            ? ""
                            : $" (with filter '{filenameFilter}')";
                        var errorCheckResult = errorBuilder.ToString();

                        return $"Successfully replaced references to '{symbol.Name}'{filterMessage} with '{replacementCode}' in {changedDocumentsCount} document(s).\n\n{errorCheckResult}";
                    } finally {
                        ObjectPoolProvider.Instance.ReturnStringBuilder(errorBuilder);
                    }
                } catch (Exception ex)
                      when (!(ex is McpException || ex is OperationCanceledException)) {
                    logger.LogError(
                        ex,
                        "Failed to replace references to symbol '{SymbolName}' with '{ReplacementCode}'",
                        fullyQualifiedSymbolName,
                        replacementCode
                    );
                    throw new McpException($"Failed to replace references: {ex.Message}");
                }
            },
            logger,
            nameof(ReplaceAllReferences),
            cancellationToken
        );
    }

    [McpServerTool(
        Name = "replace_references_by_pattern",
        Idempotent = false,
        Destructive = true,
        OpenWorld = false,
        ReadOnly = false
    )]
    [Description(
        "Batch rename symbols matching a pattern. Supports wildcards (* and ?), regex, and filters by symbol kind, namespace, and file patterns. Perfect for systematic refactoring like test* → nonmod*."
    )]
    public static async Task<string> ReplaceAllReferencesByPattern(
        ISolutionManager solutionManager,
        ICodeModificationService modificationService,
        ILogger<ModificationToolsLogCategory> logger,
        [Description(
            "Pattern to match symbol names. Supports wildcards (* and ?) or regex. Examples: 'test*', 'Old*Class', '^test\\w+'."
        )]
            string symbolNamePattern,
        [Description(
            "Replacement pattern. Wildcards are substituted with captured parts. Examples: 'nonmod*', 'New*Class'."
        )]
            string replacementPattern,
        [Description(
            "Pattern type: 'Auto' (default, auto-detect), 'Wildcard' (* and ?), 'Regex', or 'Exact'."
        )]
            string patternType = "Auto",
        [Description(
            "Filter by symbol kinds. Options: 'method', 'class', 'property', 'field', 'variable', 'parameter', 'type'. Empty = all types."
        )]
            string[]? symbolKinds = null,
        [Description(
            "Filter by namespace pattern (supports wildcards). Example: 'MyApp.Services.*'. Empty = all namespaces."
        )]
            string namespaceFilter = "",
        [Description(
            "Filter by file pattern (supports globbing). Example: '*.cs', '*Controller.cs'. Empty = all files."
        )]
            string filenameFilter = "",
        [Description("If true, only preview changes without applying them (default: true)")]
            bool preview = true,
        [Description("Commit message (only used when preview=false)")] string commitMessage = "",
        CancellationToken cancellationToken = default
    ) {
        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(
            async () => {
                // Validate parameters
                ErrorHandlingHelpers.ValidateStringParameter(
                    symbolNamePattern,
                    nameof(symbolNamePattern),
                    logger
                );
                ErrorHandlingHelpers.ValidateStringParameter(
                    replacementPattern,
                    nameof(replacementPattern),
                    logger
                );

                if (!preview && string.IsNullOrWhiteSpace(commitMessage)) {
                    throw new McpException("commitMessage is required when preview=false");
                }

                // Ensure solution is loaded
                await ToolHelpers.EnsureSolutionLoadedOrAutoLoadAsync(
                    solutionManager,
                    logger,
                    nameof(ReplaceAllReferencesByPattern),
                    cancellationToken
                );

                logger.LogInformation(
                    "Executing {ToolName}: pattern '{Pattern}' → '{Replacement}', type: {Type}, preview: {Preview}",
                    nameof(ReplaceAllReferencesByPattern),
                    symbolNamePattern,
                    replacementPattern,
                    patternType,
                    preview
                );

                // Parse pattern type
                PatternType parsedPatternType = patternType.ToLowerInvariant() switch {
                    "auto" => PatternType.Auto,
                    "wildcard" => PatternType.Wildcard,
                    "regex" => PatternType.Regex,
                    "exact" => PatternType.Exact,
                    _ => throw new McpException(
                        $"Invalid patternType: {patternType}. Valid values: Auto, Wildcard, Regex, Exact"
                    ),
                };

                // Create pattern matcher
                var matcher = new SymbolPatternMatcher(symbolNamePattern, parsedPatternType);

                // Create namespace filter if provided
                SymbolPatternMatcher? namespaceMatcher = null;
                if (!string.IsNullOrEmpty(namespaceFilter)) {
                    namespaceMatcher = new SymbolPatternMatcher(
                        namespaceFilter,
                        PatternType.Wildcard
                    );
                }

                // Build symbol kind filter
                HashSet<string>? symbolKindSet = null;
                if (symbolKinds != null && symbolKinds.Length > 0) {
                    symbolKindSet = new HashSet<string>(
                        symbolKinds,
                        StringComparer.OrdinalIgnoreCase
                    );
                }

                // Find all matching symbols
                var solution = solutionManager.CurrentSolution;
                if (solution == null) {
                    throw new McpException("No solution loaded");
                }

                var matchingSymbols =
                    new List<(ISymbol Symbol, string CurrentName, string NewName)>();

                // Iterate through all projects and symbols
                foreach (var project in solution.Projects) {
                    cancellationToken.ThrowIfCancellationRequested();

                    var compilation = await project.GetCompilationAsync(cancellationToken);
                    if (compilation == null)
                        continue;

                    // Get all symbols from compilation
                    var allSymbols = GetAllSymbolsFromCompilation(compilation);

                    foreach (var symbol in allSymbols) {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Skip symbols without source location if filename filter is specified
                        if (!string.IsNullOrEmpty(filenameFilter)) {
                            var location = symbol.Locations.FirstOrDefault(l => l.IsInSource);
                            if (
                                location == null
                                || string.IsNullOrEmpty(location.SourceTree?.FilePath)
                            )
                                continue;

                            // Check filename filter
                            var filePath = location.SourceTree.FilePath;
                            var matcher_file = new Matcher(StringComparison.OrdinalIgnoreCase);
                            matcher_file.AddInclude(filenameFilter.Replace('\\', '/'));
                            var fileRoot =
                                Path.GetPathRoot(solution.FilePath)
                                ?? Path.GetPathRoot(Environment.CurrentDirectory)!;

                            if (!matcher_file.Match(fileRoot, filePath).HasMatches)
                                continue;
                        }

                        // Check namespace filter
                        if (namespaceMatcher != null) {
                            var ns = symbol.ContainingNamespace?.ToDisplayString() ?? "";
                            if (!namespaceMatcher.IsMatch(ns))
                                continue;
                        }

                        // Check symbol kind filter
                        if (symbolKindSet != null) {
                            if (!MatchesSymbolKind(symbol, symbolKindSet))
                                continue;
                        }

                        // Check if symbol name matches pattern
                        var symbolName = symbol.Name;
                        if (!matcher.IsMatch(symbolName))
                            continue;

                        // Generate new name
                        var newName = matcher.ApplyReplacement(symbolName, replacementPattern);

                        // Skip if name doesn't change
                        if (newName == symbolName)
                            continue;

                        matchingSymbols.Add((symbol, symbolName, newName));
                    }
                }

                if (matchingSymbols.Count == 0) {
                    logger.LogWarning(
                        "No symbols found matching pattern '{Pattern}'",
                        symbolNamePattern
                    );
                    return $"No symbols found matching pattern '{symbolNamePattern}' with the specified filters.";
                }

                // Preview mode: show what would be changed
                if (preview) {
                    var previewOutput = new StringBuilder();
                    previewOutput.AppendLine(
                        $"## Preview: Batch rename symbols matching '{symbolNamePattern}' → '{replacementPattern}'"
                    );
                    previewOutput.AppendLine();
                    previewOutput.AppendLine($"**Would rename {matchingSymbols.Count} symbol(s)**");
                    previewOutput.AppendLine();

                    // Show active filters
                    if (!string.IsNullOrEmpty(namespaceFilter))
                        previewOutput.AppendLine($"• Namespace filter: `{namespaceFilter}`");
                    if (symbolKinds != null && symbolKinds.Length > 0)
                        previewOutput.AppendLine(
                            $"• Symbol kinds: `{string.Join(", ", symbolKinds)}`"
                        );
                    if (!string.IsNullOrEmpty(filenameFilter))
                        previewOutput.AppendLine($"• File filter: `{filenameFilter}`");
                    previewOutput.AppendLine();

                    previewOutput.AppendLine("### Renames:");

                    var displayCount = Math.Min(matchingSymbols.Count, 50);
                    for (int i = 0; i < displayCount; i++) {
                        var (symbol, currentName, newName) = matchingSymbols[i];
                        var symbolKind = GetSymbolKindName(symbol);
                        var ns = symbol.ContainingNamespace?.ToDisplayString() ?? "(global)";
                        previewOutput.AppendLine(
                            $"  • [{symbolKind}] {ns}.{currentName} → {newName}"
                        );
                    }

                    if (matchingSymbols.Count > 50) {
                        previewOutput.AppendLine(
                            $"  ... and {matchingSymbols.Count - 50} more symbol(s)"
                        );
                    }

                    previewOutput.AppendLine();
                    previewOutput.AppendLine("**⚠️ Preview mode:** No changes were applied.");
                    previewOutput.AppendLine(
                        "**💡 Tip:** Set `preview=false` and provide `commitMessage` to apply the changes."
                    );

                    return previewOutput.ToString();
                }

                // Apply batch rename
                logger.LogInformation(
                    "Applying batch rename for {Count} symbols...",
                    matchingSymbols.Count
                );

                var currentSolution = solution;
                int successCount = 0;
                int failureCount = 0;
                var errors = new List<string>();

                foreach (var (symbol, currentName, newName) in matchingSymbols) {
                    cancellationToken.ThrowIfCancellationRequested();

                    try {
                        // Rename this symbol
                        var newSolution = await modificationService.RenameSymbolAsync(
                            symbol,
                            newName,
                            cancellationToken
                        );
                        currentSolution = newSolution;
                        successCount++;

                        logger.LogDebug("Renamed {CurrentName} → {NewName}", currentName, newName);
                    } catch (Exception ex) {
                        failureCount++;
                        var error = $"Failed to rename {currentName} → {newName}: {ex.Message}";
                        errors.Add(error);
                        logger.LogWarning(ex, "Failed to rename symbol {CurrentName}", currentName);
                    }
                }

                // Apply all changes at once
                if (successCount > 0) {
                    var finalCommitMessage =
                        $"Batch rename: '{symbolNamePattern}' → '{replacementPattern}' ({successCount} symbols): {commitMessage}";
                    await modificationService.ApplyChangesAsync(
                        currentSolution,
                        cancellationToken,
                        finalCommitMessage
                    );
                }

                // Build result message
                var resultOutput = new StringBuilder();
                resultOutput.AppendLine($"## Batch Rename Complete");
                resultOutput.AppendLine();
                resultOutput.AppendLine($"**Successfully renamed:** {successCount} symbol(s)");

                if (failureCount > 0) {
                    resultOutput.AppendLine($"**Failed:** {failureCount} symbol(s)");
                    resultOutput.AppendLine();
                    resultOutput.AppendLine("### Errors:");
                    foreach (var error in errors.Take(10)) {
                        resultOutput.AppendLine($"  • {error}");
                    }
                    if (errors.Count > 10) {
                        resultOutput.AppendLine($"  ... and {errors.Count - 10} more error(s)");
                    }
                }

                return resultOutput.ToString();
            },
            logger,
            nameof(ReplaceAllReferencesByPattern),
            cancellationToken
        );
    }

    private static IEnumerable<ISymbol> GetAllSymbolsFromCompilation(Compilation compilation) {
        var symbols = new List<ISymbol>();
        var visitor = new SymbolCollectorVisitor(symbols);
        compilation.GlobalNamespace.Accept(visitor);
        return symbols;
    }

    private static bool MatchesSymbolKind(ISymbol symbol, HashSet<string> symbolKindSet) {
        if (symbolKindSet.Contains("method") && symbol.Kind == SymbolKind.Method)
            return true;
        if (
            symbolKindSet.Contains("class")
            && symbol is INamedTypeSymbol { TypeKind: TypeKind.Class }
        )
            return true;
        if (symbolKindSet.Contains("property") && symbol.Kind == SymbolKind.Property)
            return true;
        if (symbolKindSet.Contains("field") && symbol.Kind == SymbolKind.Field)
            return true;
        if (symbolKindSet.Contains("variable") && symbol.Kind == SymbolKind.Local)
            return true;
        if (symbolKindSet.Contains("parameter") && symbol.Kind == SymbolKind.Parameter)
            return true;
        if (symbolKindSet.Contains("type") && symbol is INamedTypeSymbol)
            return true;

        return false;
    }

    private static string GetSymbolKindName(ISymbol symbol) {
        return symbol switch {
            IMethodSymbol => "Method",
            IPropertySymbol => "Property",
            IFieldSymbol => "Field",
            INamedTypeSymbol { TypeKind: TypeKind.Class } => "Class",
            INamedTypeSymbol { TypeKind: TypeKind.Interface } => "Interface",
            INamedTypeSymbol { TypeKind: TypeKind.Struct } => "Struct",
            INamedTypeSymbol { TypeKind: TypeKind.Enum } => "Enum",
            _ when symbol.Kind == SymbolKind.Parameter => "Parameter",
            _ when symbol.Kind == SymbolKind.Local => "Variable",
            _ => symbol.Kind.ToString(),
        };
    }

    // Symbol visitor to collect all symbols from namespace hierarchy
    private class SymbolCollectorVisitor : SymbolVisitor {
        private readonly List<ISymbol> _symbols;

        public SymbolCollectorVisitor(List<ISymbol> symbols) {
            _symbols = symbols;
        }

        public override void VisitNamespace(INamespaceSymbol symbol) {
            foreach (var member in symbol.GetMembers()) {
                member.Accept(this);
            }
        }

        public override void VisitNamedType(INamedTypeSymbol symbol) {
            _symbols.Add(symbol);

            foreach (var member in symbol.GetMembers()) {
                member.Accept(this);
            }
        }

        public override void VisitMethod(IMethodSymbol symbol) {
            _symbols.Add(symbol);

            foreach (var parameter in symbol.Parameters) {
                _symbols.Add(parameter);
            }
        }

        public override void VisitProperty(IPropertySymbol symbol) {
            _symbols.Add(symbol);
        }

        public override void VisitField(IFieldSymbol symbol) {
            _symbols.Add(symbol);
        }

        public override void VisitEvent(IEventSymbol symbol) {
            _symbols.Add(symbol);
        }
    }

    [McpServerTool(
        Name = "undo",
        Idempotent = false,
        Destructive = true,
        OpenWorld = false,
        ReadOnly = false
    )]
    [Description(
        $"Reverts the last applied change to the solution. You can undo all consecutive changes you have made. Returns a diff of the change that was undone."
    )]
    public static async Task<string> Undo(
        ISolutionManager solutionManager,
        ICodeModificationService modificationService,
        ILogger<ModificationToolsLogCategory> logger,
        CancellationToken cancellationToken
    ) {
        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(
            async () => {
                await ToolHelpers.EnsureSolutionLoadedOrAutoLoadAsync(
                    solutionManager,
                    logger,
                    nameof(Undo),
                    cancellationToken
                );
                logger.LogInformation("Executing '{UndoLastChange}'", nameof(Undo));

                var (success, message) = await modificationService.UndoLastChangeAsync(
                    cancellationToken
                );
                if (!success) {
                    logger.LogWarning("Undo operation failed: {Message}", message);
                    throw new McpException($"Failed to undo the last change. {message}");
                }
                logger.LogInformation("Undo operation succeeded");
                return message;
            },
            logger,
            nameof(Undo),
            cancellationToken
        );
    }

    [McpServerTool(
        Name = "find_and_replace",
        Idempotent = false,
        Destructive = true,
        OpenWorld = false,
        ReadOnly = false
    )]
    [Description(
        "Every developer's favorite. Use this for all small edits (code tweaks, usings, namespaces, interface implementations, attributes, etc.) instead of rewriting large members or types."
    )]
    public static async Task<string> FindAndReplace(
        ISolutionManager solutionManager,
        ICodeModificationService modificationService,
        IDocumentOperationsService documentOperations,
        ILogger<ModificationToolsLogCategory> logger,
        [Description(
            "Regex operating in multiline mode, so `^` and `$` match per line. Always use `\\s*` at the beginnings of lines for unknown indentation. Make sure to escape your escapes for json."
        )]
            string regexPattern,
        [Description("Replacement text, which can include regex groups ($1, ${name}, etc.)")]
            string replacementText,
        [Description(
            "Target, which can be either a FQN (replaces text within a declaration) or a filepath supporting globbing (`*`) (replaces all instances across files)"
        )]
            string target,
        string commitMessage,
        CancellationToken cancellationToken = default
    ) {
        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(
            async () => {
                // Validate parameters
                ErrorHandlingHelpers.ValidateStringParameter(regexPattern, "regexPattern", logger);
                ErrorHandlingHelpers.ValidateStringParameter(target, "targetString", logger);

                // Normalize newlines in pattern: all line endings should match \n only
                // Because NormalizeEndOfLines() converts all \r\n and \r to \n in the text
                //
                // Strategy:
                // 1. First, normalize literal line ending characters (\r\n, \r -> \n)
                // 2. Then, normalize regex escape sequences (\r?\n, \r\n, \r -> \n)
                // 3. Finally, convert literal \n to regex pattern \n
                //
                // Step 1: Normalize literal characters (from JSON decode)
                regexPattern = regexPattern
                    .Replace("\r\n", "\n")   // Real CRLF -> LF
                    .Replace("\r", "\n");    // Real CR -> LF

                // Step 2: Normalize regex patterns (handle \r?\n, \r\n, \r in regex syntax)
                // These patterns might exist if user wrote \\r?\\n in JSON (becomes literal \r?\n after decode)
                // But BEFORE we convert \n to \\n
                regexPattern = Regex.Replace(regexPattern, @"\\r\?\\n", @"%%NEWLINE%%");  // \r?\n -> temp marker
                regexPattern = Regex.Replace(regexPattern, @"\\r\\n", @"%%NEWLINE%%");    // \r\n -> temp marker
                regexPattern = regexPattern.Replace(@"\\r", @"%%NEWLINE%%");              // \r -> temp marker

                // Step 3: Convert literal \n to regex \n
                regexPattern = regexPattern.Replace("\n", @"\n");   // Real LF -> regex \n

                // Step 4: Replace temp markers with \n
                regexPattern = regexPattern.Replace("%%NEWLINE%%", @"\n");

                // Ensure solution is loaded
                await ToolHelpers.EnsureSolutionLoadedOrAutoLoadAsync(
                    solutionManager,
                    logger,
                    nameof(FindAndReplace),
                    cancellationToken
                );
                logger.LogInformation(
                    "Executing '{FindAndReplace}' with pattern: '{Pattern}', replacement: {Replacement}, target: {Target}",
                    nameof(FindAndReplace),
                    regexPattern,
                    replacementText,
                    target
                );

                // Validate the regex pattern
                try {
                    // Create the regex with multiline option to test it
                    _ = new Regex(regexPattern, RegexOptions.Multiline | RegexOptions.Compiled);
                } catch (ArgumentException ex) {
                    throw new McpException($"Invalid regular expression pattern: {ex.Message}");
                }

                try {
                    // Get the original solution for later comparison
                    var originalSolution =
                        solutionManager.CurrentSolution
                        ?? throw new McpException(
                            "Current solution is null before find and replace operation."
                        );

                    // Track all modified files for both code and non-code files
                    var modifiedFiles = new List<string>();
                    var changedDocuments = new List<DocumentId>();
                    var nonCodeFilesModified = new List<string>();
                    var nonCodeDiffs = new Dictionary<string, string>();

                    // First, check if the target is a file path pattern (contains wildcard or is a direct file path)
                    if (
                        target.Contains("*")
                        || target.Contains("?")
                        || (File.Exists(target) && !documentOperations.IsCodeFile(target))
                    ) {
                        logger.LogInformation(
                            "Target appears to be a file path pattern or non-code file: {Target}",
                            target
                        );

                        // Normalize the target path to use forward slashes consistently
                        string normalizedTarget = target.Replace('\\', '/');

                        // Create matcher for the pattern
                        Matcher matcher = new(StringComparison.OrdinalIgnoreCase);
                        matcher.AddInclude(normalizedTarget);

                        string root =
                            Path.GetPathRoot(originalSolution.FilePath)
                            ?? Path.GetPathRoot(Environment.CurrentDirectory)!;

                        // Get all files in solution directory matching the pattern
                        var solutionDirectory = Path.GetDirectoryName(originalSolution.FilePath);
                        if (string.IsNullOrEmpty(solutionDirectory)) {
                            throw new McpException("Could not determine solution directory");
                        }

                        // Handle direct file path (no wildcards)
                        if (!target.Contains("*") && !target.Contains("?") && File.Exists(target)) {
                            // Direct file path, process just this file
                            var pathInfo = documentOperations.GetPathInfo(target);

                            if (!pathInfo.IsWithinSolutionDirectory) {
                                throw new McpException(
                                    $"File {target} exists but is outside the solution directory. Cannot modify for safety reasons."
                                );
                            }

                            // Check if it's a non-code file
                            if (!documentOperations.IsCodeFile(target)) {
                                var (changed, diff) = await ProcessNonCodeFile(
                                    target,
                                    regexPattern,
                                    replacementText,
                                    documentOperations,
                                    nonCodeFilesModified,
                                    cancellationToken
                                );
                                if (changed) {
                                    nonCodeDiffs.Add(target, diff);
                                }
                            }
                        } else {
                            // Use glob pattern to find matching files
                            DirectoryInfo dirInfo = new DirectoryInfo(solutionDirectory);
                            List<FileInfo> allFiles = dirInfo
                                .GetFiles("*.*", SearchOption.AllDirectories)
                                .ToList();
                            string rootDir =
                                Path.GetPathRoot(solutionDirectory)
                                ?? Path.GetPathRoot(Environment.CurrentDirectory)!;
                            foreach (var file in allFiles) {
                                if (matcher.Match(rootDir, file.FullName).HasMatches) {
                                    var pathInfo = documentOperations.GetPathInfo(file.FullName);

                                    // Skip files in unsafe directories or outside solution
                                    if (
                                        !pathInfo.IsWithinSolutionDirectory
                                        || !string.IsNullOrEmpty(pathInfo.WriteRestrictionReason)
                                    ) {
                                        logger.LogWarning(
                                            "Skipping file due to restrictions: {FilePath}, Reason: {Reason}",
                                            file.FullName,
                                            pathInfo.WriteRestrictionReason
                                                ?? "Outside solution directory"
                                        );
                                        continue;
                                    }

                                    // Process non-code files directly
                                    if (!documentOperations.IsCodeFile(file.FullName)) {
                                        var (changed, diff) = await ProcessNonCodeFile(
                                            file.FullName,
                                            regexPattern,
                                            replacementText,
                                            documentOperations,
                                            nonCodeFilesModified,
                                            cancellationToken
                                        );
                                        if (changed) {
                                            nonCodeDiffs.Add(file.FullName, diff);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // Now process code files through the Roslyn workspace
                    var newSolution = await modificationService.FindAndReplaceAsync(
                        target,
                        regexPattern,
                        replacementText,
                        cancellationToken,
                        RegexOptions.Multiline
                    );

                    // Get changed code documents
                    var solutionChanges = newSolution.GetChanges(originalSolution);
                    foreach (var projectChange in solutionChanges.GetProjectChanges()) {
                        changedDocuments.AddRange(projectChange.GetChangedDocuments());
                    }

                    // FIX: Возвращаем info message вместо ошибки при отсутствии изменений
                    if (changedDocuments.Count == 0 && nonCodeFilesModified.Count == 0) {
                        logger.LogInformation(
                            "No matches found for pattern '{Pattern}' in target '{Target}'",
                            regexPattern,
                            target
                        );
                        return JsonSerializer.Serialize(
                            new {
                                success = true,
                                message = $"No matches found for pattern '{regexPattern}' in target '{target}'. No changes were made.",
                                matchesFound = 0,
                                filesChanged = 0
                            },
                            new JsonSerializerOptions { WriteIndented = true }
                        );
                    }

                    // Add code document file paths to the modifiedFiles list
                    foreach (var docId in changedDocuments) {
                        var document = originalSolution.GetDocument(docId);
                        if (document?.FilePath != null) {
                            modifiedFiles.Add(document.FilePath);
                        }
                    }

                    // Add non-code files to the list
                    modifiedFiles.AddRange(nonCodeFilesModified);

                    string finalCommitMessage = $"Find and replace on {target}: {commitMessage}";

                    // Apply the changes to code files
                    if (changedDocuments.Count > 0) {
                        await modificationService.ApplyChangesAsync(
                            newSolution,
                            cancellationToken,
                            finalCommitMessage,
                            nonCodeFilesModified
                        );
                    }

                    // Commit non-code files (if we only modified non-code files)
                    if (nonCodeFilesModified.Count > 0 && changedDocuments.Count == 0) {
                        // Get solution path
                        var solutionPath = originalSolution.FilePath;
                        if (string.IsNullOrEmpty(solutionPath)) {
                            logger.LogDebug(
                                "Solution path is not available, skipping Git operations for non-code files"
                            );
                        } else {
                            await documentOperations.ProcessGitOperationsAsync(
                                nonCodeFilesModified,
                                cancellationToken,
                                finalCommitMessage
                            );
                        }
                    }

                    // Check for compilation errors in changed code documents
                    var changedDocIds = changedDocuments.Take(5).ToList(); // Limit to first 5 documents
                    StringBuilder errorBuilder = new StringBuilder("<errorCheck>");

                    // Check each affected document for compilation errors
                    foreach (var docId in changedDocIds) {
                        if (solutionManager.CurrentSolution != null) {
                            var updatedDoc = solutionManager.CurrentSolution.GetDocument(docId);
                            if (updatedDoc != null) {
                                var (docHasErrors, docErrorMessages) =
                                    await ContextInjectors.CheckCompilationErrorsAsync(
                                        solutionManager,
                                        updatedDoc,
                                        logger,
                                        cancellationToken
                                    );

                                if (docHasErrors) {
                                    errorBuilder.AppendLine(
                                        $"Issues in file {updatedDoc.FilePath ?? "unknown"}:"
                                    );
                                    errorBuilder.AppendLine(docErrorMessages);
                                    errorBuilder.AppendLine();
                                } else {
                                    errorBuilder.AppendLine(
                                        $"No compilation issues in file {updatedDoc.FilePath ?? "unknown"}."
                                    );
                                }
                            }
                        }
                    }
                    errorBuilder.AppendLine("</errorCheck>");

                    // Generate multi-document diff for code files
                    string diffOutput =
                        changedDocuments.Count > 0
                            ? await ContextInjectors.CreateMultiDocumentDiff(
                                originalSolution,
                                newSolution,
                                changedDocuments,
                                5,
                                cancellationToken
                            )
                            : "";

                    // For non-code files, build a similar diff output format
                    StringBuilder nonCodeDiffBuilder = new StringBuilder();
                    if (nonCodeDiffs.Count > 0) {
                        nonCodeDiffBuilder.AppendLine();
                        nonCodeDiffBuilder.AppendLine("Non-code file changes:");

                        int nonCodeFileCount = 0;
                        foreach (var diffEntry in nonCodeDiffs) {
                            if (nonCodeFileCount >= 5) {
                                nonCodeDiffBuilder.AppendLine(
                                    $"...and {nonCodeDiffs.Count - 5} more non-code files"
                                );
                                break;
                            }
                            nonCodeDiffBuilder.AppendLine();
                            nonCodeDiffBuilder.AppendLine($"Document: {diffEntry.Key}");
                            nonCodeDiffBuilder.AppendLine(diffEntry.Value);
                            nonCodeFileCount++;
                        }
                    }

                    return $"Successfully replaced pattern '{regexPattern}' with '{replacementText}' in {modifiedFiles.Count} file(s).\n\n{errorBuilder}\n\n{diffOutput}{nonCodeDiffBuilder}";
                } catch (Exception ex)
                      when (!(ex is McpException || ex is OperationCanceledException)) {
                    logger.LogError(
                        ex,
                        "Failed to replace pattern '{Pattern}' with '{Replacement}' in '{Target}'",
                        regexPattern,
                        replacementText,
                        target
                    );
                    throw new McpException($"Failed to perform find and replace: {ex.Message}");
                }
            },
            logger,
            nameof(FindAndReplace),
            cancellationToken
        );
    }

    [McpServerTool(
        Name = "move_member",
        Idempotent = false,
        Destructive = true,
        OpenWorld = false,
        ReadOnly = false
    )]
    [Description(
        "Moves a member (property, field, method, nested type, etc.) from one type/namespace to another. The member is removed from the source location and added to the destination."
    )]
    public static async Task<string> MoveMember(
        ISolutionManager solutionManager,
        ICodeModificationService modificationService,
        ILogger<ModificationToolsLogCategory> logger,
        [Description("FQN of the member to move.")] string fullyQualifiedMemberName,
        [Description("FQN of the destination type or namespace where the member should be moved.")]
            string fullyQualifiedDestinationTypeOrNamespaceName,
        string commitMessage,
        CancellationToken cancellationToken = default
    ) {
        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(
            async () => {
                ErrorHandlingHelpers.ValidateStringParameter(
                    fullyQualifiedMemberName,
                    nameof(fullyQualifiedMemberName),
                    logger
                );
                ErrorHandlingHelpers.ValidateStringParameter(
                    fullyQualifiedDestinationTypeOrNamespaceName,
                    nameof(fullyQualifiedDestinationTypeOrNamespaceName),
                    logger
                );

                await ToolHelpers.EnsureSolutionLoadedOrAutoLoadAsync(
                    solutionManager,
                    logger,
                    nameof(MoveMember),
                    cancellationToken
                );
                logger.LogInformation(
                    "Executing '{MoveMember}' moving {MemberName} to {DestinationName}",
                    nameof(MoveMember),
                    fullyQualifiedMemberName,
                    fullyQualifiedDestinationTypeOrNamespaceName
                );

                if (solutionManager.CurrentSolution == null) {
                    throw new McpException(
                        "Current solution is unexpectedly null after validation checks."
                    );
                }
                Solution currentSolution = solutionManager.CurrentSolution;

                var sourceMemberSymbol = await ToolHelpers.GetRoslynSymbolOrThrowAsync(
                    solutionManager,
                    fullyQualifiedMemberName,
                    cancellationToken
                );

                if (
                    sourceMemberSymbol
                    is not (
                        IFieldSymbol
                        or IPropertySymbol
                        or IMethodSymbol
                        or IEventSymbol
                        or INamedTypeSymbol {
                            TypeKind: TypeKind.Class
                                or TypeKind.Struct
                                or TypeKind.Interface
                                or TypeKind.Enum
                                or TypeKind.Delegate
                        }
                    )
                ) {
                    throw new McpException(
                        $"Symbol '{fullyQualifiedMemberName}' is not a movable member type. Only fields, properties, methods, events, and nested types can be moved."
                    );
                }

                var destinationSymbol = await ToolHelpers.GetRoslynSymbolOrThrowAsync(
                    solutionManager,
                    fullyQualifiedDestinationTypeOrNamespaceName,
                    cancellationToken
                );

                if (destinationSymbol is not (INamedTypeSymbol or INamespaceSymbol)) {
                    throw new McpException(
                        $"Destination '{fullyQualifiedDestinationTypeOrNamespaceName}' must be a type or namespace."
                    );
                }

                var sourceSyntaxRef = sourceMemberSymbol.DeclaringSyntaxReferences.FirstOrDefault();
                if (sourceSyntaxRef == null) {
                    throw new McpException(
                        $"Could not find syntax reference for member '{fullyQualifiedMemberName}'."
                    );
                }

                var sourceMemberNode = await sourceSyntaxRef.GetSyntaxAsync(cancellationToken);
                if (sourceMemberNode is not MemberDeclarationSyntax memberDeclaration) {
                    throw new McpException(
                        $"Source member '{fullyQualifiedMemberName}' is not a valid member declaration."
                    );
                }

                Document sourceDocument = ToolHelpers.GetDocumentFromSyntaxNodeOrThrow(
                    currentSolution,
                    sourceMemberNode
                );
                Document destinationDocument;
                INamedTypeSymbol? destinationTypeSymbol = null;
                INamespaceSymbol? destinationNamespaceSymbol = null;

                if (destinationSymbol is INamedTypeSymbol typeSym) {
                    destinationTypeSymbol = typeSym;
                    var destSyntaxRef = typeSym.DeclaringSyntaxReferences.FirstOrDefault();
                    if (destSyntaxRef == null) {
                        throw new McpException(
                            $"Could not find syntax reference for destination type '{fullyQualifiedDestinationTypeOrNamespaceName}'."
                        );
                    }
                    var destNode = await destSyntaxRef.GetSyntaxAsync(cancellationToken);
                    destinationDocument = ToolHelpers.GetDocumentFromSyntaxNodeOrThrow(
                        currentSolution,
                        destNode
                    );
                } else if (destinationSymbol is INamespaceSymbol nsSym) {
                    destinationNamespaceSymbol = nsSym;
                    var projectForDestination = currentSolution
                        .GetDocument(sourceDocument.Id)!
                        .Project;
                    var existingDoc = await FindExistingDocumentWithNamespaceAsync(
                        projectForDestination,
                        nsSym,
                        cancellationToken
                    );
                    if (existingDoc != null) {
                        destinationDocument = existingDoc;
                    } else {
                        var newDoc = await CreateDocumentForNamespaceAsync(
                            projectForDestination,
                            nsSym,
                            cancellationToken
                        );
                        destinationDocument = newDoc;
                        currentSolution = newDoc.Project.Solution; // Update currentSolution after adding a document
                    }
                } else {
                    throw new McpException("Invalid destination symbol type.");
                }

                if (
                    sourceDocument.Id == destinationDocument.Id
                    && sourceMemberSymbol.ContainingSymbol.Equals(
                        destinationSymbol,
                        SymbolEqualityComparer.Default
                    )
                ) {
                    throw new McpException(
                        $"Source and destination are the same. Member '{fullyQualifiedMemberName}' is already in '{fullyQualifiedDestinationTypeOrNamespaceName}'."
                    );
                }

                string memberName = GetMemberName(memberDeclaration);
                INamedTypeSymbol? updatedDestinationTypeSymbol = null;
                if (destinationTypeSymbol != null) {
                    // Re-resolve destinationTypeSymbol from the potentially updated currentSolution
                    var destinationDocumentFromCurrentSolution =
                        currentSolution.GetDocument(destinationDocument.Id)
                        ?? throw new McpException(
                            $"Destination document '{destinationDocument.FilePath}' not found in current solution for symbol re-resolution."
                        );
                    var tempDestSymbol = await SymbolFinder.FindSymbolAtPositionAsync(
                        destinationDocumentFromCurrentSolution,
                        destinationTypeSymbol.Locations.First().SourceSpan.Start,
                        cancellationToken
                    );
                    updatedDestinationTypeSymbol = tempDestSymbol as INamedTypeSymbol;
                    if (updatedDestinationTypeSymbol == null) {
                        throw new McpException(
                            $"Could not re-resolve destination type symbol '{destinationTypeSymbol.ToDisplayString()}' in the current solution state at file '{destinationDocumentFromCurrentSolution.FilePath}'. Original location span: {destinationTypeSymbol.Locations.First().SourceSpan}"
                        );
                    }
                }

                if (
                    updatedDestinationTypeSymbol != null
                    && !IsMemberAllowed(
                        updatedDestinationTypeSymbol,
                        memberDeclaration,
                        memberName,
                        cancellationToken
                    )
                ) {
                    throw new McpException(
                        $"A member with the name '{memberName}' already exists in destination type '{fullyQualifiedDestinationTypeOrNamespaceName}'."
                    );
                }

                try {
                    var actualDestinationDocument =
                        currentSolution.GetDocument(destinationDocument.Id)
                        ?? throw new McpException(
                            $"Destination document '{destinationDocument.FilePath}' not found in current solution before adding member."
                        );

                    if (updatedDestinationTypeSymbol != null) {
                        currentSolution = await modificationService.AddMemberAsync(
                            actualDestinationDocument.Id,
                            updatedDestinationTypeSymbol,
                            memberDeclaration,
                            -1,
                            cancellationToken
                        );
                    } else {
                        if (destinationNamespaceSymbol == null)
                            throw new McpException(
                                "Destination namespace symbol is null when expected for namespace move."
                            );
                        currentSolution = await AddMemberToNamespaceAsync(
                            actualDestinationDocument,
                            destinationNamespaceSymbol,
                            memberDeclaration,
                            modificationService,
                            cancellationToken
                        );
                    }

                    // Re-acquire source document and node from the *new* currentSolution
                    var sourceDocumentInCurrentSolution =
                        currentSolution.GetDocument(sourceDocument.Id)
                        ?? throw new McpException(
                            "Source document not found in current solution after adding member to destination."
                        );
                    var syntaxRootOfSourceInCurrentSolution =
                        await sourceDocumentInCurrentSolution.GetSyntaxRootAsync(cancellationToken)
                        ?? throw new McpException(
                            "Could not get syntax root for source document in current solution."
                        );

                    // Attempt to find the node again. Its span might have changed if the destination was in the same file.
                    var sourceMemberNodeInCurrentTree =
                        syntaxRootOfSourceInCurrentSolution.FindNode(
                            sourceMemberNode.Span,
                            findInsideTrivia: true,
                            getInnermostNodeForTie: true
                        );
                    if (
                        sourceMemberNodeInCurrentTree == null
                        || !(sourceMemberNodeInCurrentTree is MemberDeclarationSyntax)
                    ) {
                        // Fallback: Try to find by kind and name if span-based lookup failed (e.g. due to formatting changes or other modifications)
                        sourceMemberNodeInCurrentTree = syntaxRootOfSourceInCurrentSolution
                            .DescendantNodes()
                            .OfType<MemberDeclarationSyntax>()
                            .FirstOrDefault(m =>
                                m.Kind() == memberDeclaration.Kind()
                                && GetMemberName(m) == memberName
                            );

                        if (sourceMemberNodeInCurrentTree == null) {
                            logger.LogWarning(
                                "Could not precisely re-locate source member node by original span or by kind/name after destination add. Original span: {Span}. Member kind: {Kind}, Name: {Name}. File: {File}",
                                sourceMemberNode.Span,
                                memberDeclaration.Kind(),
                                memberName,
                                sourceDocumentInCurrentSolution.FilePath
                            );
                            // As a last resort, if the original node is still part of the new tree (by reference), use it.
                            // This is risky if the tree has been significantly changed, but better than failing if it's just minor formatting.
                            if (
                                syntaxRootOfSourceInCurrentSolution
                                    .DescendantNodes()
                                    .Contains(sourceMemberNode)
                            ) {
                                sourceMemberNodeInCurrentTree = sourceMemberNode;
                                logger.LogWarning(
                                    "Fallback: Using original source member node reference for removal. This might be risky if tree changed significantly."
                                );
                            } else {
                                throw new McpException(
                                    $"Critically failed to re-locate source member node '{memberName}' in '{sourceDocumentInCurrentSolution.FilePath}' for removal after modifications. Original span {sourceMemberNode.Span}. This usually indicates significant tree changes that broke span tracking or the member was unexpectedly altered or removed."
                                );
                            }
                        } else {
                            logger.LogInformation(
                                "Re-located source member node by kind and name for removal. Original span: {OriginalSpan}, New span: {NewSpan}",
                                sourceMemberNode.Span,
                                sourceMemberNodeInCurrentTree.Span
                            );
                        }
                    }

                    currentSolution = await RemoveMemberFromParentAsync(
                        sourceDocumentInCurrentSolution,
                        sourceMemberNodeInCurrentTree,
                        modificationService,
                        cancellationToken
                    );

                    string finalCommitMessage =
                        $"Move {memberName} to {fullyQualifiedDestinationTypeOrNamespaceName}: {commitMessage}";
                    await modificationService.ApplyChangesAsync(
                        currentSolution,
                        cancellationToken,
                        finalCommitMessage
                    );

                    // After ApplyChangesAsync, the solutionManager.CurrentSolution should be the most up-to-date.
                    // Re-fetch documents from there for final error checking.
                    var finalSourceDocumentAfterApply =
                        solutionManager.CurrentSolution?.GetDocument(sourceDocument.Id);
                    var finalDestinationDocumentAfterApply =
                        solutionManager.CurrentSolution?.GetDocument(destinationDocument.Id);

                    StringBuilder errorBuilder = new StringBuilder("<errorCheck>");
                    if (finalSourceDocumentAfterApply != null) {
                        var (sourceHasErrors, sourceErrorMessages) =
                            await ContextInjectors.CheckCompilationErrorsAsync(
                                solutionManager,
                                finalSourceDocumentAfterApply,
                                logger,
                                cancellationToken
                            );
                        errorBuilder.AppendLine(
                            sourceHasErrors
                                ? $"Issues in source file {finalSourceDocumentAfterApply.FilePath ?? "unknown"}:\n{sourceErrorMessages}\n"
                                : $"No compilation errors detected in source file {finalSourceDocumentAfterApply.FilePath ?? "unknown"}."
                        );
                    }

                    if (
                        finalDestinationDocumentAfterApply != null
                        && (
                            !finalSourceDocumentAfterApply?.Id.Equals(
                                finalDestinationDocumentAfterApply.Id
                            ) ?? true
                        )
                    ) {
                        var (destHasErrors, destErrorMessages) =
                            await ContextInjectors.CheckCompilationErrorsAsync(
                                solutionManager,
                                finalDestinationDocumentAfterApply,
                                logger,
                                cancellationToken
                            );
                        errorBuilder.AppendLine(
                            destHasErrors
                                ? $"Issues in destination file {finalDestinationDocumentAfterApply.FilePath ?? "unknown"}:\n{destErrorMessages}\n"
                                : $"No compilation errors detected in destination file {finalDestinationDocumentAfterApply.FilePath ?? "unknown"}."
                        );
                    }
                    errorBuilder.AppendLine("</errorCheck>");

                    var sourceFilePathDisplay =
                        finalSourceDocumentAfterApply?.FilePath
                        ?? sourceDocument.FilePath
                        ?? "unknown source file";
                    var destinationFilePathDisplay =
                        finalDestinationDocumentAfterApply?.FilePath
                        ?? destinationDocument.FilePath
                        ?? "unknown destination file";

                    var locationInfo =
                        sourceFilePathDisplay == destinationFilePathDisplay
                            ? $"within {sourceFilePathDisplay}"
                            : $"from {sourceFilePathDisplay} to {destinationFilePathDisplay}";

                    return $"Successfully moved member '{memberName}' to '{fullyQualifiedDestinationTypeOrNamespaceName}' {locationInfo}.\n\n{errorBuilder}";
                } catch (Exception ex)
                      when (!(ex is McpException || ex is OperationCanceledException)) {
                    logger.LogError(
                        ex,
                        "Failed to move member {MemberName} to {DestinationName}",
                        fullyQualifiedMemberName,
                        fullyQualifiedDestinationTypeOrNamespaceName
                    );
                    throw new McpException(
                        $"Failed to move member '{fullyQualifiedMemberName}' to '{fullyQualifiedDestinationTypeOrNamespaceName}': {ex.Message}",
                        ex
                    );
                }
            },
            logger,
            nameof(MoveMember),
            cancellationToken
        );
    }

    /// <summary>
    /// Finds an existing document in the project that contains the specified namespace.
    /// </summary>
    private static async Task<Document?> FindExistingDocumentWithNamespaceAsync(
        Project project,
        INamespaceSymbol namespaceSymbol,
        CancellationToken cancellationToken
    ) {
        var namespaceName = namespaceSymbol.ToDisplayString();

        foreach (var document in project.Documents) {
            if (document.FilePath?.EndsWith(".cs") != true)
                continue;

            var root = await document.GetSyntaxRootAsync(cancellationToken);
            if (root is CompilationUnitSyntax compilationUnit) {
                // Check if this document already contains the target namespace
                var hasNamespace = compilationUnit
                    .Members.OfType<NamespaceDeclarationSyntax>()
                    .Any(n => n.Name.ToString() == namespaceName);

                if (hasNamespace
                    || (namespaceSymbol.IsGlobalNamespace && compilationUnit.Members.Count > 0)
                ) {
                    return document;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Creates a new document for the specified namespace.
    /// </summary>
    private static Task<Document> CreateDocumentForNamespaceAsync(
        Project project,
        INamespaceSymbol namespaceSymbol,
        CancellationToken cancellationToken
    ) {
        var namespaceName = namespaceSymbol.ToDisplayString();
        var fileName =
            string.IsNullOrEmpty(namespaceName) || namespaceSymbol.IsGlobalNamespace
                ? "GlobalNamespace.cs"
                : $"{namespaceName.Split('.').Last()}.cs";

        // Ensure the file name doesn't conflict with existing files
        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var counter = 1;
        var projectDirectory =
            Path.GetDirectoryName(project.FilePath)
            ?? throw new InvalidOperationException("Project directory not found");

        var fullPath = Path.Combine(projectDirectory, fileName);
        while (
            project.Documents.Any(d =>
                string.Equals(d.FilePath, fullPath, StringComparison.OrdinalIgnoreCase)
            )
        ) {
            fileName = $"{baseName}{counter}{extension}";
            fullPath = Path.Combine(projectDirectory, fileName);
            counter++;
        }

        // Create basic content for the new file
        var content = namespaceSymbol.IsGlobalNamespace
            ? "// Global namespace file\n"
            : $"namespace {namespaceName} {{\n    // Namespace content\n}}\n";

        var newDocument = project.AddDocument(fileName, content, filePath: fullPath);
        return Task.FromResult(newDocument);
    }

    /// <summary>
    /// Adds a member to the specified namespace in the given document.
    /// </summary>
    private static async Task<Solution> AddMemberToNamespaceAsync(
        Document document,
        INamespaceSymbol namespaceSymbol,
        MemberDeclarationSyntax memberDeclaration,
        ICodeModificationService modificationService,
        CancellationToken cancellationToken
    ) {
        var root = await document.GetSyntaxRootAsync(cancellationToken);
        if (root is not CompilationUnitSyntax compilationUnit) {
            throw new McpException("Destination document does not have a valid compilation unit.");
        }

        var editor = await DocumentEditor.CreateAsync(document, cancellationToken);

        if (namespaceSymbol.IsGlobalNamespace) {
            // Add to global namespace (compilation unit)
            editor.AddMember(compilationUnit, memberDeclaration);
        } else {
            // Find or create the target namespace
            var namespaceName = namespaceSymbol.ToDisplayString();
            var targetNamespace = compilationUnit
                .Members.OfType<NamespaceDeclarationSyntax>()
                .FirstOrDefault(n => n.Name.ToString() == namespaceName);

            if (targetNamespace == null) {
                // Create the namespace and add the member to it
                targetNamespace = SyntaxFactory
                    .NamespaceDeclaration(SyntaxFactory.ParseName(namespaceName))
                    .AddMembers(memberDeclaration);
                editor.AddMember(compilationUnit, targetNamespace);
            } else {
                // Add member to existing namespace
                editor.AddMember(targetNamespace, memberDeclaration);
            }
        }

        var changedDocument = editor.GetChangedDocument();
        var formattedDocument = await modificationService.FormatDocumentAsync(
            changedDocument,
            cancellationToken
        );
        return formattedDocument.Project.Solution;
    }

    /// <summary>
    /// Properly removes a member from its parent by deleting it from the parent's member collection.
    /// </summary>
    private static async Task<Solution> RemoveMemberFromParentAsync(
        Document document,
        SyntaxNode memberNode,
        ICodeModificationService modificationService,
        CancellationToken cancellationToken
    ) {
        if (memberNode is not MemberDeclarationSyntax memberDeclaration) {
            throw new McpException(
                $"Node is not a member declaration: {memberNode.GetType().Name}"
            );
        }

        var root = await document.GetSyntaxRootAsync(cancellationToken);
        if (root == null) {
            throw new McpException("Could not get syntax root from document.");
        }

        SyntaxNode newRoot;

        if (memberNode.Parent is CompilationUnitSyntax compilationUnit) {
            // Handle top-level members in the compilation unit
            var newMembers = compilationUnit.Members.Remove(memberDeclaration);
            newRoot = compilationUnit.WithMembers(newMembers);
        } else if (memberNode.Parent is NamespaceDeclarationSyntax namespaceDecl) {
            // Handle members in a namespace
            var newMembers = namespaceDecl.Members.Remove(memberDeclaration);
            var newNamespace = namespaceDecl.WithMembers(newMembers);
            newRoot = root.ReplaceNode(namespaceDecl, newNamespace);
        } else if (memberNode.Parent is TypeDeclarationSyntax typeDecl) {
            // Handle members in a type declaration (class, struct, interface, etc.)
            var newMembers = typeDecl.Members.Remove(memberDeclaration);
            var newType = typeDecl.WithMembers(newMembers);
            newRoot = root.ReplaceNode(typeDecl, newType);
        } else {
            throw new McpException(
                $"Cannot remove member from parent of type {memberNode.Parent?.GetType().Name ?? "null"}."
            );
        }

        var newDocument = document.WithSyntaxRoot(newRoot);
        var formattedDocument = await modificationService.FormatDocumentAsync(
            newDocument,
            cancellationToken
        );
        return formattedDocument.Project.Solution;
    }

    private static async Task<(bool changed, string diff)> ProcessNonCodeFile(
        string filePath,
        string regexPattern,
        string replacementText,
        IDocumentOperationsService documentOperations,
        List<string> modifiedFiles,
        CancellationToken cancellationToken
    ) {
        try {
            var (originalContent, _) = await documentOperations.ReadFileAsync(
                filePath,
                false,
                cancellationToken
            );
            var regex = new Regex(regexPattern, RegexOptions.Multiline | RegexOptions.Compiled);
            string newContent = regex.Replace(
                originalContent.NormalizeEndOfLines(),
                replacementText
            );

            // Only write if content changed
            if (newContent != originalContent) {
                // Note: we don't pass commit message here as we'll handle Git at a higher level
                // for all modified non-code files at once
                await documentOperations.WriteFileAsync(
                    filePath,
                    newContent,
                    true,
                    cancellationToken,
                    string.Empty
                );
                modifiedFiles.Add(filePath);

                // Generate diff
                string diff = ContextInjectors.CreateCodeDiff(originalContent, newContent);
                return (true, diff);
            }

            return (false, string.Empty);
        } catch (Exception ex) {
            throw new McpException($"Error processing non-code file {filePath}: {ex.Message}");
        }
    }
}
