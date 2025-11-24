#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build UltrasharpTools for multiple platforms and create release archives.

.DESCRIPTION
    Builds all components with shared runtime architecture:
    - shared/: Common .NET runtime (~70MB)
    - Droid: Framework-dependent
    - VectorDB: Native AOT (included in Droid/)
    - Comm: Trimmed SingleFile

    Note: Overlord собирается отдельно через Dockerfile.

.PARAMETER Version
    Version string (default: auto-detect from .csproj)

.PARAMETER Configuration
    Build configuration (default: Release)

.PARAMETER OutputDir
    Output directory for release archives (default: Run.Publish/Releases)

.EXAMPLE
    .\build-releases.ps1
    .\build-releases.ps1 -Version "3.0.8"
#>

param(
    [string]$Version = "",
    [string]$Configuration = "Release",
    [string]$OutputDir = "Run.Publish/Releases"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Find project root
function Find-ProjectRoot {
    $current = $PSScriptRoot
    if (-not $current) { $current = Get-Location }

    while ($current) {
        if (Test-Path (Join-Path $current "*.sln")) {
            return $current
        }
        $parent = Split-Path -Parent $current
        if ($parent -eq $current) { break }
        $current = $parent
    }

    throw "Could not find project root (directory with .sln file)"
}

$ProjectRoot = Find-ProjectRoot
Set-Location $ProjectRoot

# Colors
function Write-Header { param([string]$Text) Write-Host "`n=== $Text ===" -ForegroundColor Cyan }
function Write-Success { param([string]$Text) Write-Host "[OK] $Text" -ForegroundColor Green }
function Write-Info { param([string]$Text) Write-Host "    $Text" -ForegroundColor Yellow }
function Write-Err { param([string]$Text) Write-Host "[X] $Text" -ForegroundColor Red }

Write-Header "UltrasharpTools Multi-Platform Release Builder"

# Auto-detect version if not specified
if (-not $Version) {
    $csprojPath = "UltrasharpTools.Droid/UltrasharpTools.Droid.csproj"
    if (Test-Path $csprojPath) {
        $csproj = [xml](Get-Content $csprojPath)
        $Version = $csproj.Project.PropertyGroup.Version
        if (-not $Version) {
            $Version = $csproj.Project.PropertyGroup.VersionPrefix
        }
    }
    if (-not $Version) {
        $Version = "3.0.8"
        Write-Info "Version auto-detection failed, using default: $Version"
    } else {
        Write-Info "Auto-detected version: $Version"
    }
}

# Create output directory
$ReleasesDir = Join-Path $ProjectRoot $OutputDir
if (Test-Path $ReleasesDir) {
    Write-Info "Cleaning existing releases directory..."
    Remove-Item $ReleasesDir -Recurse -Force
}
New-Item -ItemType Directory -Path $ReleasesDir -Force | Out-Null
Write-Success "Created: $ReleasesDir"

# Define target platforms
$Platforms = @(
    @{ RID = "win-x64";     OS = "windows"; Arch = "x64";   Archive = "zip" }
    @{ RID = "win-arm64";   OS = "windows"; Arch = "arm64"; Archive = "zip" }
    @{ RID = "osx-x64";     OS = "macos";   Arch = "x64";   Archive = "tar.gz" }
    @{ RID = "osx-arm64";   OS = "macos";   Arch = "arm64"; Archive = "tar.gz" }
    @{ RID = "linux-x64";   OS = "linux";   Arch = "x64";   Archive = "tar.gz" }
    @{ RID = "linux-arm64"; OS = "linux";   Arch = "arm64"; Archive = "tar.gz" }
)

$SuccessCount = 0
$FailCount = 0

foreach ($Platform in $Platforms) {
    $rid = $Platform.RID
    $os = $Platform.OS
    $arch = $Platform.Arch
    $archiveType = $Platform.Archive

    Write-Header "Building for $rid ($os / $arch)"

    try {
        # Build using build-release.ps1 from project root
        $buildScript = Join-Path $ProjectRoot "build-release.ps1"
        Write-Info "Running: build-release.ps1 -RuntimeIdentifier $rid -Clean"

        & $buildScript -RuntimeIdentifier $rid -Clean

        if ($LASTEXITCODE -ne 0) {
            throw "Build failed with exit code $LASTEXITCODE"
        }

        # Find output directory
        $buildOutput = Join-Path $ProjectRoot "Run.Publish"
        if (-not (Test-Path $buildOutput)) {
            throw "Build output not found at: $buildOutput"
        }

        # Create archive name
        $archiveName = "ultrasharp-tools-v$Version-$os-$arch"

        Write-Info "Creating archive: $archiveName.$archiveType"

        if ($archiveType -eq "zip") {
            # Windows: ZIP archive
            $zipPath = Join-Path $ReleasesDir "$archiveName.zip"
            Compress-Archive -Path "$buildOutput/*" -DestinationPath $zipPath -Force
            $fileSize = (Get-Item $zipPath).Length / 1MB
            Write-Success "Created: $archiveName.zip ($([math]::Round($fileSize, 2)) MB)"
        }
        else {
            # Linux/macOS: tar.gz archive
            $tarPath = Join-Path $ReleasesDir "$archiveName.tar.gz"

            Push-Location $buildOutput
            try {
                # Create tar.gz (requires tar in PATH)
                tar -czf $tarPath *
                if ($LASTEXITCODE -ne 0) {
                    throw "tar command failed"
                }
                $fileSize = (Get-Item $tarPath).Length / 1MB
                Write-Success "Created: $archiveName.tar.gz ($([math]::Round($fileSize, 2)) MB)"
            }
            finally {
                Pop-Location
            }
        }

        $SuccessCount++
    }
    catch {
        Write-Err "Failed to build ${rid}: ${_}"
        $FailCount++
    }
}

# Summary
Write-Header "Build Summary"
Write-Host "Total platforms: $($Platforms.Count)" -ForegroundColor White
Write-Host "[OK] Successful: $SuccessCount" -ForegroundColor Green
if ($FailCount -gt 0) {
    Write-Host "[X] Failed: $FailCount" -ForegroundColor Red
}

Write-Host "`nRelease archives location:" -ForegroundColor Cyan
Write-Host "  $ReleasesDir" -ForegroundColor White

if ($SuccessCount -gt 0) {
    Write-Host "`nGenerated files:" -ForegroundColor Cyan
    Get-ChildItem $ReleasesDir | ForEach-Object {
        $size = $_.Length / 1MB
        Write-Host "  $($_.Name) - $([math]::Round($size, 2)) MB" -ForegroundColor Gray
    }

    Write-Host "`nNext steps for GitHub Release:" -ForegroundColor Yellow
    Write-Host "1. Create a new release on GitHub:" -ForegroundColor White
    Write-Host "   gh release create v$Version --title `"Release v$Version`" --notes `"Release notes here`"" -ForegroundColor Gray
    Write-Host "`n2. Upload release archives:" -ForegroundColor White
    Write-Host "   gh release upload v$Version $ReleasesDir/*" -ForegroundColor Gray
    Write-Host "`n   OR manually at: https://github.com/your-repo/releases/new" -ForegroundColor Gray
}

if ($FailCount -gt 0) {
    exit 1
}

Write-Success "`nAll builds completed successfully!"
