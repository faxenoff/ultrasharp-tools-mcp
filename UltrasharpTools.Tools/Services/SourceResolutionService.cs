using System.Net.Http;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;

namespace UltrasharpTools.Tools.Services
{
    public partial class SourceResolutionService(
        ISolutionManager solutionManager,
        ILogger<SourceResolutionService> logger
    ) : ISourceResolutionService
    {
        private readonly ISolutionManager _solutionManager =
            solutionManager ?? throw new ArgumentNullException(nameof(solutionManager));
        private readonly ILogger<SourceResolutionService> _logger =
            logger ?? throw new ArgumentNullException(nameof(logger));
        private readonly HttpClient _httpClient = new();

        public async Task<SourceResult?> ResolveSourceAsync(
            Microsoft.CodeAnalysis.ISymbol symbol,
            CancellationToken cancellationToken
        )
        {
            if (symbol == null)
            {
                LogSymbolNull();
                return null;
            }

            // 1. Try to get from syntax references (source available)
            if (symbol.DeclaringSyntaxReferences.Length > 0)
            {
                var syntaxRef = symbol.DeclaringSyntaxReferences[0];
                var sourceText = await syntaxRef.GetSyntaxAsync(cancellationToken);
                if (sourceText != null)
                {
                    var tree = syntaxRef.SyntaxTree;
                    return new SourceResult
                    {
                        Source = sourceText.ToString(),
                        FilePath = tree.FilePath,
                        IsOriginalSource = true,
                        IsDecompiled = false,
                        ResolutionMethod = "Local Source",
                    };
                }
            }

            // 2. Try Source Link
            var sourceLinkResult = await TrySourceLinkAsync(symbol, cancellationToken);
            if (sourceLinkResult != null)
                return sourceLinkResult;

            // 3. Try embedded source
            var embeddedResult = await TryEmbeddedSourceAsync(symbol, cancellationToken);
            if (embeddedResult != null)
                return embeddedResult;

            // 4. Try decompilation as fallback
            var decompiledResult = await TryDecompilationAsync(symbol, cancellationToken);
            if (decompiledResult != null)
                return decompiledResult;

            return null;
        }

        public async Task<SourceResult?> TrySourceLinkAsync(
            Microsoft.CodeAnalysis.ISymbol symbol,
            CancellationToken cancellationToken
        )
        {
            LogAttemptingSourceLink(symbol.Name);
            try
            {
                // Get location of the assembly containing the symbol
                var assembly = symbol.ContainingAssembly;
                if (assembly == null)
                {
                    LogNoContainingAssembly(symbol.Name);
                    return null;
                }

                // Find the PE reference for this assembly
                var metadataReference = GetMetadataReferenceForAssembly(assembly);
                if (metadataReference == null)
                {
                    LogNoMetadataReference(assembly.Name);
                    return null;
                }

                // Check for PDB adjacent to the DLL
                var dllPath = metadataReference.Display;
                if (string.IsNullOrEmpty(dllPath) || !File.Exists(dllPath))
                {
                    LogAssemblyNotFound(dllPath);
                    return null;
                }

                var pdbPath = Path.ChangeExtension(dllPath, ".pdb");
                if (!File.Exists(pdbPath))
                {
                    LogPdbNotFound(pdbPath);
                    return null;
                }

                LogFoundPdb(pdbPath);

                // Open the PDB and look for Source Link information
                using var pdbStream = File.OpenRead(pdbPath);
                using var metadataReaderProvider = MetadataReaderProvider.FromPortablePdbStream(
                    pdbStream
                );
                var metadataReader = metadataReaderProvider.GetMetadataReader();

                // Extract Source Link JSON document
                string? sourceLinkJson = null;
                foreach (var customDebugInfoHandle in metadataReader.CustomDebugInformation)
                {
                    var customDebugInfo = metadataReader.GetCustomDebugInformation(
                        customDebugInfoHandle
                    );
                    var kind = metadataReader.GetGuid(customDebugInfo.Kind);

                    // Source Link kind GUID
                    if (kind == new Guid("CC110556-A091-4D38-9FEC-25AB9A351A6A"))
                    {
                        var blobReader = metadataReader.GetBlobReader(customDebugInfo.Value);
                        sourceLinkJson = Encoding.UTF8.GetString(
                            blobReader.ReadBytes(blobReader.Length)
                        );
                        break;
                    }
                }

                if (string.IsNullOrEmpty(sourceLinkJson))
                {
                    LogNoSourceLinkInPdb();
                    return null;
                }

                LogFoundSourceLinkJson(sourceLinkJson);

                // Parse the JSON and extract source URLs
                var sourceLinkDoc = System.Text.Json.JsonDocument.Parse(sourceLinkJson);
                var urlsElement = sourceLinkDoc.RootElement.GetProperty("documents");

                // Get the document containing our symbol
                string symbolDocumentPath = GetSymbolDocumentPath(symbol);
                if (string.IsNullOrEmpty(symbolDocumentPath))
                {
                    LogCannotDetermineDocumentPath(symbol.Name);
                    return null;
                }

                // Normalize path for comparison with Source Link entries
                symbolDocumentPath = symbolDocumentPath.Replace('\\', '/');

                // Find matching URL in Source Link data
                string? sourceUrl = null;
                foreach (var property in urlsElement.EnumerateObject())
                {
                    var pattern = property.Name;
                    var url = property.Value.GetString();

                    // Source Link uses wildcard patterns like "C:/Projects/*" -> "https://raw.githubusercontent.com/user/repo/*"
                    if (IsPathMatch(symbolDocumentPath, pattern) && !string.IsNullOrEmpty(url))
                    {
                        // Replace the wildcard part in the URL
                        sourceUrl = url.Replace("*", GetWildcardMatch(symbolDocumentPath, pattern));
                        break;
                    }
                }

                if (string.IsNullOrEmpty(sourceUrl))
                {
                    LogNoMatchingSourceUrl(symbolDocumentPath);
                    return null;
                }

                // Download the source from the URL
                LogDownloadingSource(sourceUrl);
                var sourceCode = await _httpClient.GetStringAsync(sourceUrl, cancellationToken);

                return new SourceResult
                {
                    Source = sourceCode,
                    FilePath = sourceUrl,
                    IsOriginalSource = true,
                    IsDecompiled = false,
                    ResolutionMethod = "Source Link",
                };
            }
            catch (Exception ex)
            {
                LogSourceLinkError(ex, symbol.Name);
                return null;
            }
        }

        public async Task<SourceResult?> TryEmbeddedSourceAsync(
            Microsoft.CodeAnalysis.ISymbol symbol,
            CancellationToken cancellationToken
        )
        {
            LogAttemptingEmbeddedSource(symbol.Name);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Get location of the assembly containing the symbol
                var assembly = symbol.ContainingAssembly;
                if (assembly == null)
                {
                    LogNoContainingAssembly(symbol.Name);
                    return null;
                }

                // Find the PE reference for this assembly
                var metadataReference = GetMetadataReferenceForAssembly(assembly);
                if (metadataReference == null)
                {
                    LogNoMetadataReference(assembly.Name);
                    return null;
                }

                // Get the assembly path
                var assemblyPath = metadataReference.Display;
                if (string.IsNullOrEmpty(assemblyPath) || !File.Exists(assemblyPath))
                {
                    LogAssemblyNotFound(assemblyPath);
                    return null;
                }

                LogCheckingEmbeddedSource(assemblyPath);

                // Get embedded source information for this symbol
                var embeddedSourceInfo = EmbeddedSourceReader.GetEmbeddedSourceForSymbol(symbol);
                if (embeddedSourceInfo == null)
                {
                    LogNoEmbeddedSourceInfo(symbol.Name);
                    return null;
                }

                // Check for PDB embedded in the assembly
                Dictionary<string, EmbeddedSourceReader.SourceResult> embeddedSources = new();
                try
                {
                    embeddedSources = await Task.Run(
                        () => EmbeddedSourceReader.ReadEmbeddedSourcesFromAssembly(assemblyPath),
                        cancellationToken
                    );
                }
                catch (Exception ex)
                {
                    LogEmbeddedSourceReadError(ex, assemblyPath);
                    // Continue to check standalone PDB
                }

                // If no embedded sources found, check for standalone PDB
                if (embeddedSources.Count == 0)
                {
                    var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
                    if (File.Exists(pdbPath))
                    {
                        LogCheckingStandalonePdb(pdbPath);

                        try
                        {
                            // Read embedded sources in a background task to avoid blocking
                            embeddedSources = await Task.Run(
                                () => EmbeddedSourceReader.ReadEmbeddedSources(pdbPath),
                                cancellationToken
                            );
                        }
                        catch (Exception ex)
                        {
                            LogPdbSourceReadError(ex, pdbPath);
                        }
                    }
                }

                if (embeddedSources.Count == 0)
                {
                    LogNoEmbeddedSourcesFound(symbol.Name);
                    return null;
                }

                // Try to find matching source based on file name
                string symbolFileName = embeddedSourceInfo.FilePath ?? string.Empty;

                // Try exact match first
                if (
                    !string.IsNullOrEmpty(symbolFileName)
                    && embeddedSources.TryGetValue(symbolFileName, out var exactMatch)
                )
                {
                    LogFoundExactMatch(symbolFileName);
                    return new SourceResult
                    {
                        Source = exactMatch.SourceCode ?? string.Empty,
                        FilePath = symbolFileName,
                        IsOriginalSource = true,
                        IsDecompiled = false,
                        ResolutionMethod = "Embedded Source (Exact Match)",
                    };
                }

                // Try filename match (ignoring path)
                string fileNameOnly = Path.GetFileName(symbolFileName);
                foreach (var source in embeddedSources)
                {
                    string sourceFileName = Path.GetFileName(source.Key);
                    if (
                        string.Equals(
                            sourceFileName,
                            fileNameOnly,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    {
                        LogFoundFileNameMatch(sourceFileName);
                        return new SourceResult
                        {
                            Source = source.Value.SourceCode ?? string.Empty,
                            FilePath = source.Key,
                            IsOriginalSource = true,
                            IsDecompiled = false,
                            ResolutionMethod = "Embedded Source (Filename Match)",
                        };
                    }
                }

                // If the symbol is a method, property, etc., try to find its containing type
                if (symbol.ContainingType != null)
                {
                    string containingTypeName = symbol.ContainingType.Name + ".cs";
                    foreach (var source in embeddedSources)
                    {
                        string sourceFileName = Path.GetFileName(source.Key);
                        if (
                            string.Equals(
                                sourceFileName,
                                containingTypeName,
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                        {
                            LogFoundContainingTypeSource(symbol.ContainingType.Name);
                            return new SourceResult
                            {
                                Source = source.Value.SourceCode ?? string.Empty,
                                FilePath = source.Key,
                                IsOriginalSource = true,
                                IsDecompiled = false,
                                ResolutionMethod = "Embedded Source (Containing Type)",
                            };
                        }
                    }
                }

                // If we still don't have a match and there's just one source file, use it
                // This is common for small libraries with a single source file
                if (embeddedSources.Count == 1)
                {
                    var singleSource = embeddedSources.First();
                    LogUsingSingleSource(singleSource.Key);
                    return new SourceResult
                    {
                        Source = singleSource.Value.SourceCode ?? string.Empty,
                        FilePath = singleSource.Key,
                        IsOriginalSource = true,
                        IsDecompiled = false,
                        ResolutionMethod = "Embedded Source (Single File)",
                    };
                }

                LogNoMatchingEmbeddedSource(symbol.Name, embeddedSources.Count);
                return null;
            }
            catch (Exception ex)
            {
                LogEmbeddedSourceError(ex, symbol.Name);
                return null;
            }
        }

        public async Task<SourceResult?> TryDecompilationAsync(
            Microsoft.CodeAnalysis.ISymbol symbol,
            CancellationToken cancellationToken
        )
        {
            LogAttemptingDecompilation(symbol.Name);
            try
            {
                // Get location of the assembly containing the symbol
                var assembly = symbol.ContainingAssembly;
                if (assembly == null)
                {
                    LogNoContainingAssembly(symbol.Name);
                    return null;
                }

                // Find the PE reference for this assembly
                var metadataReference = GetMetadataReferenceForAssembly(assembly);
                if (metadataReference == null)
                {
                    LogNoMetadataReference(assembly.Name);
                    return null;
                }

                var assemblyPath = metadataReference.Display;
                if (string.IsNullOrEmpty(assemblyPath) || !File.Exists(assemblyPath))
                {
                    LogAssemblyNotFound(assemblyPath);
                    return null;
                }

                LogDecompilingFromAssembly(assemblyPath);

                // Create settings for the decompiler
                var decompilerSettings = new DecompilerSettings
                {
                    ThrowOnAssemblyResolveErrors = false,
                    UseExpressionBodyForCalculatedGetterOnlyProperties = true,
                    UsingDeclarations = true,
                    NullPropagation = true,
                    AlwaysUseBraces = true,
                    RemoveDeadCode = true,
                };

                // Decompilation can be CPU intensive, so run it in a background task
                return await Task.Run(
                    () =>
                    {
                        try
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            // Create the decompiler
                            var decompiler = new CSharpDecompiler(assemblyPath, decompilerSettings);

                            // Process based on symbol type
                            string? typeFullName = null;
                            string? memberName = null;

                            if (symbol is Microsoft.CodeAnalysis.INamedTypeSymbol namedType)
                            {
                                typeFullName = namedType.ToDisplayString(
                                    SymbolDisplayFormat.FullyQualifiedFormat
                                );
                            }
                            else if (symbol is Microsoft.CodeAnalysis.IMethodSymbol method)
                            {
                                typeFullName = method.ContainingType.ToDisplayString(
                                    SymbolDisplayFormat.FullyQualifiedFormat
                                );
                                memberName = method.Name;
                            }
                            else if (symbol is Microsoft.CodeAnalysis.IPropertySymbol property)
                            {
                                typeFullName = property.ContainingType.ToDisplayString(
                                    SymbolDisplayFormat.FullyQualifiedFormat
                                );
                                memberName = property.Name;
                            }
                            else if (symbol is Microsoft.CodeAnalysis.IFieldSymbol field)
                            {
                                typeFullName = field.ContainingType.ToDisplayString(
                                    SymbolDisplayFormat.FullyQualifiedFormat
                                );
                                memberName = field.Name;
                            }
                            else if (symbol is Microsoft.CodeAnalysis.IEventSymbol eventSymbol)
                            {
                                typeFullName = eventSymbol.ContainingType.ToDisplayString(
                                    SymbolDisplayFormat.FullyQualifiedFormat
                                );
                                memberName = eventSymbol.Name;
                            }

                            if (string.IsNullOrEmpty(typeFullName))
                            {
                                LogCannotDetermineTypeName(symbol.Name);
                                return null;
                            }

                            // Clean up the type name for the decompiler
                            typeFullName = typeFullName
                                .Replace("global::", "")
                                .Replace("<", "{")
                                .Replace(">", "}");

                            try
                            {
                                // Try to decompile the type or member
                                string decompiled;
                                if (string.IsNullOrEmpty(memberName))
                                {
                                    // Decompile entire type
                                    decompiled = decompiler.DecompileTypeAsString(
                                        new FullTypeName(typeFullName)
                                    );
                                }
                                else
                                {
                                    // Decompile specific member
                                    var typeDef = decompiler
                                        .TypeSystem.FindType(new FullTypeName(typeFullName))
                                        ?.GetDefinition();
                                    if (typeDef == null)
                                    {
                                        LogTypeDefinitionNotFound(typeFullName);
                                        return null;
                                    }

                                    var memberDef = typeDef.Members.FirstOrDefault(m =>
                                        m.Name == memberName
                                    );
                                    if (memberDef == null)
                                    {
                                        LogMemberNotFound(memberName, typeFullName);
                                        return null;
                                    }

                                    decompiled = decompiler.DecompileAsString(
                                        memberDef.MetadataToken
                                    );
                                }

                                return new SourceResult
                                {
                                    Source = decompiled,
                                    FilePath = $"{typeFullName}.cs (decompiled)",
                                    IsOriginalSource = false,
                                    IsDecompiled = true,
                                    ResolutionMethod = "Decompilation",
                                };
                            }
                            catch (Exception ex)
                            {
                                LogDecompilationFallback(ex, symbol.Name);

                                // Fallback: try to decompile just the containing type
                                try
                                {
                                    cancellationToken.ThrowIfCancellationRequested();

                                    var containingTypeFullName =
                                        symbol.ContainingType?.ToDisplayString(
                                            SymbolDisplayFormat.FullyQualifiedFormat
                                        );
                                    if (!string.IsNullOrEmpty(containingTypeFullName))
                                    {
                                        containingTypeFullName = containingTypeFullName
                                            .Replace("global::", "")
                                            .Replace("<", "{")
                                            .Replace(">", "}");

                                        var decompiled = decompiler.DecompileTypeAsString(
                                            new FullTypeName(containingTypeFullName)
                                        );
                                        return new SourceResult
                                        {
                                            Source = decompiled,
                                            FilePath = $"{containingTypeFullName}.cs (decompiled)",
                                            IsOriginalSource = false,
                                            IsDecompiled = true,
                                            ResolutionMethod = "Decompilation (Fallback)",
                                        };
                                    }
                                }
                                catch (Exception innerEx)
                                {
                                    LogFallbackDecompilationFailed(innerEx, symbol.Name);
                                }
                            }

                            return null;
                        }
                        catch (Exception ex)
                        {
                            LogDecompilationError(ex, symbol.Name);
                            return null;
                        }
                    },
                    cancellationToken
                );
            }
            catch (Exception ex)
            {
                LogDecompilationError(ex, symbol.Name);
                return null;
            }
        }

        #region Helper Methods

        private PortableExecutableReference? GetMetadataReferenceForAssembly(
            IAssemblySymbol assembly
        )
        {
            if (!_solutionManager.IsSolutionLoaded)
            {
                LogSolutionNotLoaded();
                return null;
            }

            foreach (var project in _solutionManager.GetProjects())
            {
                foreach (
                    var reference in project.MetadataReferences.OfType<PortableExecutableReference>()
                )
                {
                    if (Path.GetFileNameWithoutExtension(reference.FilePath) == assembly.Name)
                    {
                        return reference;
                    }
                }
            }

            return null;
        }

        private string GetSymbolDocumentPath(Microsoft.CodeAnalysis.ISymbol symbol)
        {
            // For symbols with syntax references, get the file path directly
            if (symbol.DeclaringSyntaxReferences.Length > 0)
            {
                var syntaxRef = symbol.DeclaringSyntaxReferences[0];
                return syntaxRef.SyntaxTree.FilePath;
            }

            // For metadata symbols, try to infer document path
            // This is a simplistic approach and might not work in all cases
            return $"{symbol.ContainingType?.Name ?? symbol.Name}.cs";
        }

        private bool IsPathMatch(string path, string pattern)
        {
            // Source Link uses patterns with * wildcards
            if (!pattern.Contains('*'))
            {
                return string.Equals(path, pattern, StringComparison.OrdinalIgnoreCase);
            }

            // Simple wildcard matching for patterns like "C:/Projects/*"
            var prefix = pattern.Substring(0, pattern.IndexOf('*'));
            var suffix = pattern.Substring(pattern.IndexOf('*') + 1);

            return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && (
                    suffix.Length == 0 || path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)
                );
        }

        private string GetWildcardMatch(string path, string pattern)
        {
            var prefix = pattern.Substring(0, pattern.IndexOf('*'));
            var suffix = pattern.Substring(pattern.IndexOf('*') + 1);

            if (suffix.Length == 0)
            {
                return path.Substring(prefix.Length);
            }

            return path.Substring(prefix.Length, path.Length - prefix.Length - suffix.Length);
        }

        #endregion
    }
}
