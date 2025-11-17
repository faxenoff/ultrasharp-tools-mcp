using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace UltrasharpTools.Tools.Services;

/// <summary>
/// Lightweight NuGet service using direct HTTP API calls instead of heavy NuGet.Protocol library.
/// Saves ~1.6 MB by replacing NuGet.Protocol + NuGet.Packaging dependencies.
/// </summary>
public sealed class NuGetHttpService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NuGetHttpService> _logger;
    private const string NuGetApiBase = "https://api.nuget.org/v3-flatcontainer";

    public NuGetHttpService(IHttpClientFactory httpClientFactory, ILogger<NuGetHttpService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("NuGetApi");
        _httpClient.BaseAddress = new Uri(NuGetApiBase);
        _logger = logger;
    }

    /// <summary>
    /// Validates if a package exists on NuGet.org, optionally checking a specific version.
    /// </summary>
    public async Task<bool> ValidatePackageAsync(
        string packageId,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Validating package {PackageId} {Version} on NuGet.org",
                packageId, version ?? "latest");

            // Get all versions from NuGet API
            var versions = await GetPackageVersionsAsync(packageId, cancellationToken);

            if (versions == null || versions.Length == 0)
            {
                _logger.LogWarning("Package {PackageId} not found on NuGet.org", packageId);
                return false;
            }

            // If no specific version requested, package exists
            if (string.IsNullOrEmpty(version))
            {
                return true;
            }

            // Check if specific version exists
            var versionExists = versions.Any(v =>
                string.Equals(v, version, StringComparison.OrdinalIgnoreCase));

            if (!versionExists)
            {
                _logger.LogWarning("Version {Version} not found for package {PackageId}", version, packageId);
            }

            return versionExists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating NuGet package {PackageId} {Version}", packageId, version);
            return false;
        }
    }

    /// <summary>
    /// Gets the latest stable version of a package from NuGet.org.
    /// </summary>
    public async Task<string> GetLatestVersionAsync(
        string packageId,
        bool includePrerelease = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var versions = await GetPackageVersionsAsync(packageId, cancellationToken);

            if (versions == null || versions.Length == 0)
            {
                throw new InvalidOperationException($"No versions found for package '{packageId}'");
            }

            // Filter out prereleases if needed
            var filteredVersions = includePrerelease
                ? versions
                : versions.Where(v => !IsPrerelease(v)).ToArray();

            if (filteredVersions.Length == 0)
            {
                throw new InvalidOperationException($"No stable versions found for package '{packageId}'");
            }

            // Parse versions and get the latest
            var parsedVersions = filteredVersions
                .Select(v => (Original: v, Parsed: TryParseVersion(v)))
                .Where(x => x.Parsed != null)
                .OrderByDescending(x => x.Parsed)
                .ToList();

            if (parsedVersions.Count == 0)
            {
                throw new InvalidOperationException($"Could not parse any versions for package '{packageId}'");
            }

            var latest = parsedVersions[0].Original;
            _logger.LogDebug("Latest version for {PackageId}: {Version}", packageId, latest);

            return latest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest version for package {PackageId}", packageId);
            throw;
        }
    }

    /// <summary>
    /// Gets all available versions for a package from NuGet.org API.
    /// Uses the v3-flatcontainer endpoint: https://api.nuget.org/v3-flatcontainer/{packageId}/index.json
    /// </summary>
    private async Task<string[]> GetPackageVersionsAsync(
        string packageId,
        CancellationToken cancellationToken)
    {
        try
        {
            // NuGet API endpoint: https://api.nuget.org/v3-flatcontainer/{id}/index.json
            var url = $"/{packageId.ToLowerInvariant()}/index.json";

            var response = await _httpClient.GetFromJsonAsync<NuGetVersionsResponse>(
                url,
                cancellationToken);

            return response?.Versions ?? Array.Empty<string>();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Package {PackageId} not found on NuGet.org", packageId);
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Simple version parsing (Major.Minor.Patch) without full SemVer complexity.
    /// </summary>
    private static Version? TryParseVersion(string versionString)
    {
        try
        {
            // Remove prerelease suffix if present (e.g., "1.2.3-beta" -> "1.2.3")
            var dashIndex = versionString.IndexOf('-');
            var versionPart = dashIndex >= 0 ? versionString[..dashIndex] : versionString;

            // Handle 4-part versions by trimming to 3 parts
            var parts = versionPart.Split('.');
            if (parts.Length >= 3)
            {
                var trimmed = string.Join(".", parts.Take(3));
                return Version.Parse(trimmed);
            }

            return Version.Parse(versionPart);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Checks if a version string is a prerelease (contains '-' like "1.0.0-beta").
    /// </summary>
    private static bool IsPrerelease(string version)
    {
        return version.Contains('-');
    }

    // Response model for NuGet API /index.json endpoint
    private sealed class NuGetVersionsResponse
    {
        [JsonPropertyName("versions")]
        public string[] Versions { get; set; } = Array.Empty<string>();
    }
}
