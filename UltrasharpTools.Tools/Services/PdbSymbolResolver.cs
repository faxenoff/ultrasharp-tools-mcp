using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Resolves symbols using PDB debug information.
/// Provides accurate mapping between source locations and compiled methods.
/// </summary>
public sealed partial class PdbSymbolResolver : IPdbSymbolResolver, IDisposable
{
    private readonly ILogger<PdbSymbolResolver> _logger;
    private readonly ConcurrentDictionary<string, PdbInfo?> _pdbCache = new();
    private bool _disposed;

    public PdbSymbolResolver(ILogger<PdbSymbolResolver> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Attempts to resolve a method token from a stack trace line.
    /// </summary>
    /// <param name="assemblyPath">Path to the assembly DLL</param>
    /// <param name="methodName">Method name from stack trace</param>
    /// <param name="lineNumber">Line number from stack trace (if available)</param>
    /// <returns>Sequence point information if found</returns>
    public SequencePointInfo? ResolveSequencePoint(
        string assemblyPath,
        string methodName,
        int? lineNumber = null
    )
    {
        if (!File.Exists(assemblyPath))
        {
            LogAssemblyNotFound(assemblyPath);
            return null;
        }

        var pdbInfo = GetOrLoadPdb(assemblyPath);
        if (pdbInfo == null)
        {
            return null;
        }

        // Search for method in PDB
        foreach (var method in pdbInfo.Methods)
        {
            if (method.MethodName.Contains(methodName, StringComparison.OrdinalIgnoreCase))
            {
                // If line number specified, find closest sequence point
                if (lineNumber.HasValue && method.SequencePoints.Count > 0)
                {
                    var closestPoint = method
                        .SequencePoints.OrderBy(sp => Math.Abs(sp.StartLine - lineNumber.Value))
                        .FirstOrDefault();

                    if (closestPoint != null)
                    {
                        return closestPoint;
                    }
                }

                // Return first sequence point
                return method.SequencePoints.FirstOrDefault();
            }
        }

        return null;
    }

    /// <summary>
    /// Gets all sequence points for a method.
    /// </summary>
    public List<SequencePointInfo> GetSequencePoints(string assemblyPath, string methodName)
    {
        var pdbInfo = GetOrLoadPdb(assemblyPath);
        if (pdbInfo == null)
        {
            return new List<SequencePointInfo>();
        }

        var result = new List<SequencePointInfo>();
        foreach (var method in pdbInfo.Methods)
        {
            if (method.MethodName.Contains(methodName, StringComparison.OrdinalIgnoreCase))
            {
                result.AddRange(method.SequencePoints);
            }
        }

        return result;
    }

    /// <summary>
    /// Checks if a method has been inlined.
    /// </summary>
    public bool IsInlinedMethod(string assemblyPath, string methodName)
    {
        // Inlined methods typically don't have their own sequence points
        // or are marked in PDB metadata
        var sequencePoints = GetSequencePoints(assemblyPath, methodName);
        return sequencePoints.Count == 0;
    }

    private PdbInfo? GetOrLoadPdb(string assemblyPath)
    {
        return _pdbCache.GetOrAdd(
            assemblyPath,
            path =>
            {
                try
                {
                    return LoadPdbInfo(path);
                }
                catch (Exception ex)
                {
                    LogPdbLoadFailed(ex, path);
                    return null;
                }
            }
        );
    }

    private PdbInfo? LoadPdbInfo(string assemblyPath)
    {
        using var peReader = new PEReader(File.OpenRead(assemblyPath));

        // Check for embedded PDB first
        var embeddedPdbEntry = peReader
            .ReadDebugDirectory()
            .FirstOrDefault(e => e.Type == DebugDirectoryEntryType.EmbeddedPortablePdb);

        MetadataReaderProvider? pdbReaderProvider = null;

        if (embeddedPdbEntry.DataSize > 0)
        {
            // Embedded PDB
            pdbReaderProvider = peReader.ReadEmbeddedPortablePdbDebugDirectoryData(
                embeddedPdbEntry
            );
            LogEmbeddedPdbLoaded(assemblyPath);
        }
        else
        {
            // External PDB file
            var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
            if (!File.Exists(pdbPath))
            {
                LogPdbNotFound(pdbPath);
                return null;
            }

#pragma warning disable CA2000 // MetadataReaderProvider owns and disposes the stream
            pdbReaderProvider = MetadataReaderProvider.FromPortablePdbStream(
                File.OpenRead(pdbPath)
            );
#pragma warning restore CA2000
            LogExternalPdbLoaded(assemblyPath);
        }

        using (pdbReaderProvider)
        {
            var pdbReader = pdbReaderProvider.GetMetadataReader();
            var methods = new List<MethodDebugInfo>();

            // Iterate through all methods in PDB
            foreach (var methodDebugInfoHandle in pdbReader.MethodDebugInformation)
            {
                var methodDebugInfo = pdbReader.GetMethodDebugInformation(methodDebugInfoHandle);

                if (methodDebugInfo.SequencePointsBlob.IsNil)
                {
                    continue;
                }

                var sequencePoints = new List<SequencePointInfo>();
                foreach (var sequencePoint in methodDebugInfo.GetSequencePoints())
                {
                    if (sequencePoint.IsHidden)
                    {
                        continue;
                    }

                    // Get document name
                    string? documentPath = null;
                    if (!sequencePoint.Document.IsNil)
                    {
                        var document = pdbReader.GetDocument(sequencePoint.Document);
                        documentPath = pdbReader.GetString(document.Name);
                    }

                    sequencePoints.Add(
                        new SequencePointInfo
                        {
                            StartLine = sequencePoint.StartLine,
                            EndLine = sequencePoint.EndLine,
                            StartColumn = sequencePoint.StartColumn,
                            EndColumn = sequencePoint.EndColumn,
                            Offset = sequencePoint.Offset,
                            DocumentPath = documentPath,
                        }
                    );
                }

                if (sequencePoints.Count > 0)
                {
                    // Try to get method name from metadata
                    var methodName = GetMethodName(peReader, methodDebugInfoHandle);

                    methods.Add(
                        new MethodDebugInfo
                        {
                            MethodName = methodName,
                            SequencePoints = sequencePoints,
                        }
                    );
                }
            }

            return new PdbInfo { AssemblyPath = assemblyPath, Methods = methods };
        }
    }

    private string GetMethodName(PEReader peReader, MethodDebugInformationHandle handle)
    {
        // Method debug info handle has same row number as method def
        var rowNumber = MetadataTokens.GetRowNumber(handle);

        try
        {
            var metadataReader = peReader.GetMetadataReader();
            var methodDefHandle = MetadataTokens.MethodDefinitionHandle(rowNumber);

            if (!methodDefHandle.IsNil)
            {
                var methodDef = metadataReader.GetMethodDefinition(methodDefHandle);
                var methodName = metadataReader.GetString(methodDef.Name);

                // Try to get type name
                var typeDefHandle = methodDef.GetDeclaringType();
                if (!typeDefHandle.IsNil)
                {
                    var typeDef = metadataReader.GetTypeDefinition(typeDefHandle);
                    var typeName = metadataReader.GetString(typeDef.Name);
                    var typeNamespace = metadataReader.GetString(typeDef.Namespace);

                    return $"{typeNamespace}.{typeName}.{methodName}";
                }

                return methodName;
            }
        }
        catch
        {
            // Fallback to row number if method def not found
        }

        return $"Method_{rowNumber}";
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _pdbCache.Clear();
        _disposed = true;
    }
}

/// <summary>
/// Information extracted from PDB file.
/// </summary>
public sealed class PdbInfo
{
    public required string AssemblyPath { get; init; }
    public required List<MethodDebugInfo> Methods { get; init; }
}

/// <summary>
/// Debug information for a single method.
/// </summary>
public sealed class MethodDebugInfo
{
    public required string MethodName { get; init; }
    public required List<SequencePointInfo> SequencePoints { get; init; }
}

/// <summary>
/// Represents a sequence point in source code.
/// Maps IL offset to source location.
/// </summary>
public sealed class SequencePointInfo
{
    public int StartLine { get; init; }
    public int EndLine { get; init; }
    public int StartColumn { get; init; }
    public int EndColumn { get; init; }
    public int Offset { get; init; }
    public string? DocumentPath { get; init; }

    public override string ToString()
    {
        return $"{DocumentPath}:({StartLine},{StartColumn})-({EndLine},{EndColumn})";
    }
}
