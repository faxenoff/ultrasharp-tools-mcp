using UltrasharpTools.Tools.Services;

namespace UltrasharpTools.Tools.Interfaces;

/// <summary>
/// Service for resolving symbols using PDB debug information.
/// </summary>
public interface IPdbSymbolResolver
{
/// <summary>
/// Resolves a sequence point from stack trace information.
/// </summary>
SequencePointInfo? ResolveSequencePoint(
string assemblyPath,
string methodName,
int? lineNumber = null
);

/// <summary>
/// Gets all sequence points for a method.
/// </summary>
List<SequencePointInfo> GetSequencePoints(string assemblyPath, string methodName);

/// <summary>
/// Checks if a method has been inlined.
/// </summary>
bool IsInlinedMethod(string assemblyPath, string methodName);
}
