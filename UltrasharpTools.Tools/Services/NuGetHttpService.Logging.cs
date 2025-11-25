using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Tools.Services;

public partial class NuGetHttpService
{
    [LoggerMessage(EventId = 3600, Level = LogLevel.Debug,
        Message = "Validating package {PackageId} {Version} on NuGet.org")]
    private partial void LogValidatingPackage(string packageId, string version);

    [LoggerMessage(EventId = 3601, Level = LogLevel.Warning,
        Message = "Package {PackageId} not found on NuGet.org")]
    private partial void LogPackageNotFound(string packageId);

    [LoggerMessage(EventId = 3602, Level = LogLevel.Warning,
        Message = "Version {Version} not found for package {PackageId}")]
    private partial void LogVersionNotFound(string version, string packageId);

    [LoggerMessage(EventId = 3603, Level = LogLevel.Error,
        Message = "Error validating NuGet package {PackageId} {Version}")]
    private partial void LogValidationError(Exception exception, string packageId, string? version);

    [LoggerMessage(EventId = 3604, Level = LogLevel.Debug,
        Message = "Latest version for {PackageId}: {Version}")]
    private partial void LogLatestVersion(string packageId, string version);

    [LoggerMessage(EventId = 3605, Level = LogLevel.Error,
        Message = "Error getting latest version for package {PackageId}")]
    private partial void LogGetLatestError(Exception exception, string packageId);
}
