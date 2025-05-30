using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Evaluation;

namespace SharpTools.Tools.Services;

/// <summary>
/// Comprehensive NuGet package reader supporting both PackageReference and packages.config
/// </summary>
public class LegacyNuGetPackageReader {
    public class PackageReference {
        public string PackageId { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string? TargetFramework { get; set; }
        public bool IsDevelopmentDependency { get; set; }
        public PackageFormat Format { get; set; }
        public string? HintPath { get; set; } // For packages.config references
    }

    public enum PackageFormat {
        PackageReference,
        PackagesConfig
    }

    public class ProjectPackageInfo {
        public string ProjectPath { get; set; } = string.Empty;
        public PackageFormat Format { get; set; }
        public List<PackageReference> Packages { get; set; } = new List<PackageReference>();
        public string? PackagesConfigPath { get; set; }
    }

    /// <summary>
    /// Gets package information for a project, automatically detecting the format
    /// </summary>
    public static ProjectPackageInfo GetPackagesForProject(string projectPath) {
        var info = new ProjectPackageInfo {
            ProjectPath = projectPath,
            Format = DetectPackageFormat(projectPath)
        };

        if (info.Format == PackageFormat.PackageReference) {
            info.Packages = GetPackageReferences(projectPath);
        } else {
            info.PackagesConfigPath = GetPackagesConfigPath(projectPath);
            info.Packages = GetPackagesFromConfig(info.PackagesConfigPath);
        }

        return info;
    }

    /// <summary>
    /// Gets basic package information without using MSBuild (used as fallback)
    /// </summary>
    public static List<PackageReference> GetBasicPackageReferencesWithoutMSBuild(string projectPath) {
        var packages = new List<PackageReference>();

        try {
            if (!File.Exists(projectPath)) {
                return packages;
            }

            var xDoc = XDocument.Load(projectPath);
            var packageRefs = xDoc.Descendants("PackageReference");

            foreach (var packageRef in packageRefs) {
                string? packageId = packageRef.Attribute("Include")?.Value;
                string? version = packageRef.Attribute("Version")?.Value;

                // If version is not in attribute, check for Version element
                if (string.IsNullOrEmpty(version)) {
                    version = packageRef.Element("Version")?.Value;
                }

                if (!string.IsNullOrEmpty(packageId) && !string.IsNullOrEmpty(version)) {
                    packages.Add(new PackageReference {
                        PackageId = packageId,
                        Version = version,
                        Format = PackageFormat.PackageReference
                    });
                }
            }
        } catch (Exception) {
            // Ignore errors and return what we have
        }

        return packages;
    }

    /// <summary>
    /// Detects whether a project uses PackageReference or packages.config
    /// </summary>
    public static PackageFormat DetectPackageFormat(string projectPath) {
        if (string.IsNullOrEmpty(projectPath) || !File.Exists(projectPath)) {
            return PackageFormat.PackageReference; // Default to modern format
        }

        var projectDir = Path.GetDirectoryName(projectPath);
        if (projectDir != null) {
            var packagesConfigPath = Path.Combine(projectDir, "packages.config");

            // Check if packages.config exists
            if (File.Exists(packagesConfigPath)) {
                return PackageFormat.PackagesConfig;
            }
        }

        // Check if project file contains PackageReference items using XML parsing
        try {
            var xDoc = XDocument.Load(projectPath);
            var hasPackageReference = xDoc.Descendants("PackageReference").Any();
            return hasPackageReference ? PackageFormat.PackageReference : PackageFormat.PackagesConfig;
        } catch {
            // If we can't load the project, assume packages.config for legacy projects
            return PackageFormat.PackagesConfig;
        }
    }

    /// <summary>
    /// Gets PackageReference items from modern SDK-style projects
    /// </summary>
    public static List<PackageReference> GetPackageReferences(string projectPath) {
        // Use XML parsing approach instead of MSBuild API
        return GetBasicPackageReferencesWithoutMSBuild(projectPath);
    }

    /// <summary>
    /// Gets package information from packages.config file
    /// </summary>
    public static List<PackageReference> GetPackagesFromConfig(string? packagesConfigPath) {
        var packages = new List<PackageReference>();

        if (string.IsNullOrEmpty(packagesConfigPath) || !File.Exists(packagesConfigPath)) {
            return packages;
        }

        try {
            var doc = XDocument.Load(packagesConfigPath);
            var packageElements = doc.Root?.Elements("package");

            if (packageElements != null) {
                foreach (var packageElement in packageElements) {
                    var packageId = packageElement.Attribute("id")?.Value;
                    var version = packageElement.Attribute("version")?.Value;
                    var targetFramework = packageElement.Attribute("targetFramework")?.Value;
                    var isDevelopmentDependency = string.Equals(
                        packageElement.Attribute("developmentDependency")?.Value, "true",
                        StringComparison.OrdinalIgnoreCase);

                    if (!string.IsNullOrEmpty(packageId) && !string.IsNullOrEmpty(version)) {
                        packages.Add(new PackageReference {
                            PackageId = packageId,
                            Version = version,
                            TargetFramework = targetFramework,
                            IsDevelopmentDependency = isDevelopmentDependency,
                            Format = PackageFormat.PackagesConfig
                        });
                    }
                }
            }
        } catch {
            // Return empty list if parsing fails
        }

        return packages;
    }

    /// <summary>
    /// Gets the packages.config path for a project
    /// </summary>
    public static string GetPackagesConfigPath(string projectPath) {
        if (string.IsNullOrEmpty(projectPath)) {
            return string.Empty;
        }

        var projectDir = Path.GetDirectoryName(projectPath);
        return string.IsNullOrEmpty(projectDir) ? string.Empty : Path.Combine(projectDir, "packages.config");
    }

    /// <summary>
    /// Gets all packages from a project file regardless of format
    /// </summary>
    public static List<(string PackageId, string Version)> GetAllPackages(string projectPath) {
        if (string.IsNullOrEmpty(projectPath) || !File.Exists(projectPath)) {
            return new List<(string, string)>();
        }

        var packages = new List<(string, string)>();
        var format = DetectPackageFormat(projectPath);

        try {
            if (format == PackageFormat.PackageReference) {
                var packageRefs = GetPackageReferences(projectPath);
                packages.AddRange(packageRefs.Select(p => (p.PackageId, p.Version)));
            } else {
                var packagesConfigPath = GetPackagesConfigPath(projectPath);
                var packageRefs = GetPackagesFromConfig(packagesConfigPath);
                packages.AddRange(packageRefs.Select(p => (p.PackageId, p.Version)));
            }
        } catch {
            // Return what we have if an error occurs
        }

        return packages;
    }
    public static List<PackageReference> GetAllPackageReferences(string projectPath) {
        if (string.IsNullOrEmpty(projectPath) || !File.Exists(projectPath)) {
            return new List<PackageReference>();
        }

        var format = DetectPackageFormat(projectPath);

        try {
            if (format == PackageFormat.PackageReference) {
                return GetPackageReferences(projectPath);
            } else {
                var packagesConfigPath = GetPackagesConfigPath(projectPath);
                return GetPackagesFromConfig(packagesConfigPath);
            }
        } catch {
            // Return empty list if an error occurs
            return new List<PackageReference>();
        }
    }
}