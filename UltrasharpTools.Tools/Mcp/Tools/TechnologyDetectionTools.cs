using System.Xml.Linq;
using ModelContextProtocol;
using UltrasharpTools.Tools.Mcp;

namespace UltrasharpTools.Tools.Mcp.Tools;

/// <summary>
/// MCP tools for detecting technology stack, frameworks, and dependencies.
/// Sprint 10: detect_technology_stack
/// </summary>
public class TechnologyDetectionToolsLogCategory { }

[McpServerToolType]
public static partial class TechnologyDetectionTools
{
    /// <summary>
    /// Detect technology stack: frameworks, languages, dependencies, and build tools.
    /// </summary>
    [McpServerTool(
        Name = "detect_technology_stack",
        Idempotent = true,
        ReadOnly = true,
        Destructive = false,
        OpenWorld = false
    )]
    [Description(
        "Detect frameworks, languages, dependencies, and build tools used in the solution. "
            + "Analyzes .NET versions, NuGet packages, project types, and build configuration. "
            + "Useful for understanding unfamiliar projects and providing context to AI."
    )]
    public static async Task<object> DetectTechnologyStack(
        ISolutionManager solutionManager,
        ILogger<TechnologyDetectionToolsLogCategory> logger,
        [Description("Include detailed package versions (default: true)")]
            bool includeVersions = true,
        [Description("Group packages by category (default: true)")] bool categorizePackages = true,
        CancellationToken cancellationToken = default
    )
    {
        return await ErrorHandlingHelpers.ExecuteWithErrorHandlingAsync(
            async () =>
            {
                await ToolHelpers.EnsureSolutionLoadedOrAutoLoadAsync(
                    solutionManager,
                    logger,
                    nameof(DetectTechnologyStack),
                    cancellationToken
                );

                logger.LogInformation("Detecting technology stack for solution");

                var solution = solutionManager.CurrentWorkspace!.CurrentSolution;
                var solutionPath = solution.FilePath;

                // 1. Languages
                var languages = solution
                    .Projects.GroupBy(p => p.Language)
                    .Select(g => new
                    {
                        name = g.Key,
                        projectCount = g.Count(),
                        projects = g.Select(p => p.Name).ToList(),
                    })
                    .OrderByDescending(l => l.projectCount)
                    .ToList();

                // 2. Target Frameworks
                var frameworks = new List<object>();
                var projectInfos = new List<object>();

                foreach (var project in solution.Projects)
                {
                    var projectFilePath = project.FilePath;
                    if (string.IsNullOrEmpty(projectFilePath) || !File.Exists(projectFilePath))
                        continue;

                    try
                    {
                        var csprojDoc = await LoadProjectFileAsync(
                            projectFilePath,
                            cancellationToken
                        );
                        var targetFramework = GetTargetFramework(csprojDoc);
                        var outputType = GetOutputType(csprojDoc);
                        var nullable = GetNullableContext(csprojDoc);
                        var langVersion = GetLanguageVersion(csprojDoc);

                        projectInfos.Add(
                            new
                            {
                                name = project.Name,
                                language = project.Language,
                                targetFramework,
                                outputType,
                                nullable,
                                languageVersion = langVersion,
                                filePath = projectFilePath,
                            }
                        );

                        if (!string.IsNullOrEmpty(targetFramework))
                        {
                            frameworks.Add(
                                new { framework = targetFramework, project = project.Name }
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(
                            ex,
                            "Failed to parse project file: {ProjectPath}",
                            projectFilePath
                        );
                    }
                }

                var frameworkSummary = frameworks
                    .GroupBy(f => ((dynamic)f).framework)
                    .Select(g => new
                    {
                        framework = g.Key,
                        count = g.Count(),
                        projects = g.Select(f => ((dynamic)f).project).ToList(),
                    })
                    .OrderByDescending(f => f.count)
                    .ToList();

                // 3. Dependencies (NuGet packages)
                var allPackages = new List<PackageReference>();

                foreach (var project in solution.Projects)
                {
                    var projectFilePath = project.FilePath;
                    if (string.IsNullOrEmpty(projectFilePath) || !File.Exists(projectFilePath))
                        continue;

                    try
                    {
                        var csprojDoc = await LoadProjectFileAsync(
                            projectFilePath,
                            cancellationToken
                        );
                        var projectPackages = GetPackageReferences(csprojDoc, project.Name);
                        allPackages.AddRange(projectPackages);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(
                            ex,
                            "Failed to extract packages from: {ProjectPath}",
                            projectFilePath
                        );
                    }
                }

                // Group packages
                var packageSummary = allPackages
                    .GroupBy(p => p.Name)
                    .Select(g => new
                    {
                        name = g.Key,
                        versions = includeVersions
                            ? g.Select(p => p.Version).Distinct().ToList()
                            : null,
                        usedInProjects = g.Select(p => p.ProjectName).Distinct().ToList(),
                        category = categorizePackages ? CategorizePackage(g.Key) : null,
                    })
                    .OrderBy(p => p.name)
                    .ToList();

                var packagesByCategory = categorizePackages
                    ? packageSummary
                        .GroupBy(p => p.category ?? "Other")
                        .Select(g => new
                        {
                            category = g.Key,
                            count = g.Count(),
                            packages = g.Select(p => new
                                {
                                    p.name,
                                    p.versions,
                                    projectCount = p.usedInProjects.Count,
                                })
                                .ToList(),
                        })
                        .OrderByDescending(c => c.count)
                        .ToList()
                    : null;

                // 4. Build Tools
                var buildTools = DetectBuildTools(solutionPath);

                // 5. Solution Statistics
                var stats = new
                {
                    totalProjects = solution.Projects.Count(),
                    totalDocuments = solution.Projects.Sum(p => p.Documents.Count()),
                    totalPackages = packageSummary.Count,
                    uniqueFrameworks = frameworkSummary.Count,
                    languages = languages.Count,
                };

                // Prepare packages based on includeVersions flag
                object packages = includeVersions
                    ? packageSummary
                    : packageSummary
                        .Select(p => new
                        {
                            p.name,
                            p.usedInProjects,
                            p.category,
                        })
                        .ToList();

                return ToolHelpers.ToJson(
                    new
                    {
                        solutionPath,
                        statistics = stats,
                        languages,
                        frameworks = frameworkSummary,
                        projects = projectInfos,
                        dependencies = new
                        {
                            totalPackages = packageSummary.Count,
                            packages,
                            byCategory = packagesByCategory,
                        },
                        buildTools,
                        detectedAt = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
                    }
                );
            },
            logger,
            nameof(DetectTechnologyStack),
            cancellationToken
        );
    }

    // ==================== Helper Methods ====================

    private static async Task<XDocument> LoadProjectFileAsync(
        string projectPath,
        CancellationToken cancellationToken
    )
    {
        var content = await File.ReadAllTextAsync(projectPath, cancellationToken);
        return XDocument.Parse(content);
    }

    private static string? GetTargetFramework(XDocument csprojDoc)
    {
        // Try TargetFramework (single)
        var targetFramework = csprojDoc.Descendants("TargetFramework").FirstOrDefault()?.Value;
        if (!string.IsNullOrEmpty(targetFramework))
            return targetFramework;

        // Try TargetFrameworks (multiple)
        var targetFrameworks = csprojDoc.Descendants("TargetFrameworks").FirstOrDefault()?.Value;
        return targetFrameworks?.Split(';').FirstOrDefault();
    }

    private static string? GetOutputType(XDocument csprojDoc)
    {
        return csprojDoc.Descendants("OutputType").FirstOrDefault()?.Value;
    }

    private static string? GetNullableContext(XDocument csprojDoc)
    {
        return csprojDoc.Descendants("Nullable").FirstOrDefault()?.Value;
    }

    private static string? GetLanguageVersion(XDocument csprojDoc)
    {
        return csprojDoc.Descendants("LangVersion").FirstOrDefault()?.Value;
    }

    private static List<PackageReference> GetPackageReferences(
        XDocument csprojDoc,
        string projectName
    )
    {
        return csprojDoc
            .Descendants("PackageReference")
            .Select(pr => new PackageReference
            {
                Name = pr.Attribute("Include")?.Value ?? "",
                Version =
                    pr.Attribute("Version")?.Value ?? pr.Element("Version")?.Value ?? "unknown",
                ProjectName = projectName,
            })
            .Where(pr => !string.IsNullOrEmpty(pr.Name))
            .ToList();
    }

    private static object DetectBuildTools(string? solutionPath)
    {
        var tools = new List<string>();

        if (string.IsNullOrEmpty(solutionPath))
        {
            return new { detected = tools, primary = "Unknown" };
        }

        var solutionDir = Path.GetDirectoryName(solutionPath);
        if (string.IsNullOrEmpty(solutionDir))
        {
            return new { detected = tools, primary = "Unknown" };
        }

        // Check for common build tool indicators
        if (File.Exists(Path.Combine(solutionDir, "global.json")))
            tools.Add("dotnet CLI");

        if (File.Exists(Path.Combine(solutionDir, "Directory.Build.props")))
            tools.Add("MSBuild (Directory.Build.props)");

        if (File.Exists(Path.Combine(solutionDir, "Directory.Build.targets")))
            tools.Add("MSBuild (Directory.Build.targets)");

        if (File.Exists(Path.Combine(solutionDir, "nuget.config")))
            tools.Add("NuGet");

        if (Directory.Exists(Path.Combine(solutionDir, ".git")))
            tools.Add("Git");

        if (Directory.Exists(Path.Combine(solutionDir, ".github")))
            tools.Add("GitHub Actions");

        if (File.Exists(Path.Combine(solutionDir, "azure-pipelines.yml")))
            tools.Add("Azure Pipelines");

        if (File.Exists(Path.Combine(solutionDir, ".gitlab-ci.yml")))
            tools.Add("GitLab CI");

        if (File.Exists(Path.Combine(solutionDir, "Dockerfile")))
            tools.Add("Docker");

        // Default .NET build tool
        var primary = tools.Contains("dotnet CLI") ? "dotnet CLI" : "MSBuild";

        return new
        {
            detected = tools,
            primary,
            solutionFormat = Path.GetExtension(solutionPath)?.ToLowerInvariant() == ".sln"
                ? "Visual Studio Solution"
                : "Unknown",
        };
    }

    private static string CategorizePackage(string packageName)
    {
        var lower = packageName.ToLowerInvariant();

        // Testing
        if (
            lower.Contains("test")
            || lower.Contains("xunit")
            || lower.Contains("nunit")
            || lower.Contains("mstest")
            || lower.Contains("moq")
            || lower.Contains("fluent")
        )
            return "Testing";

        // Logging
        if (lower.Contains("log") || lower.Contains("serilog") || lower.Contains("nlog"))
            return "Logging";

        // ASP.NET / Web
        if (
            lower.Contains("aspnet")
            || lower.Contains("mvc")
            || lower.Contains("razor")
            || lower.Contains("blazor")
            || lower.Contains("signalr")
        )
            return "Web/ASP.NET";

        // Database / ORM
        if (
            lower.Contains("entity")
            || lower.Contains("dapper")
            || lower.Contains("npgsql")
            || lower.Contains("mysql")
            || lower.Contains("sqlite")
            || lower.Contains("mongodb")
        )
            return "Database/ORM";

        // JSON / Serialization
        if (lower.Contains("json") || lower.Contains("newtonsoft") || lower.Contains("serializ"))
            return "Serialization";

        // HTTP / API
        if (
            lower.Contains("http")
            || lower.Contains("rest")
            || lower.Contains("api")
            || lower.Contains("swagger")
        )
            return "HTTP/API";

        // DI / IoC
        if (lower.Contains("inject") || lower.Contains("autofac") || lower.Contains("ninject"))
            return "Dependency Injection";

        // Microsoft Core
        if (lower.StartsWith("microsoft.extensions"))
            return "Microsoft.Extensions";

        // Code Analysis
        if (
            lower.Contains("analyzer")
            || lower.Contains("roslyn")
            || lower.Contains("codeanalysis")
        )
            return "Code Analysis";

        // ML / AI
        if (lower.Contains("ml.net") || lower.Contains("tensorflow") || lower.Contains("onnx"))
            return "ML/AI";

        return "Other";
    }

    private class PackageReference
    {
        public required string Name { get; init; }
        public required string Version { get; init; }
        public required string ProjectName { get; init; }
    }
}
