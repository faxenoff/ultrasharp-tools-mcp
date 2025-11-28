#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build UltrasharpTools for multiple platforms and create release archives.

.DESCRIPTION
    Builds all components for Windows, Linux, and macOS:
    - Comm: Cosmopolitan C binary (~700KB, works on all platforms)
    - Windows: Native AOT for VectorDB (~15MB)
    - Linux (via WSL): Native AOT for VectorDB (auto-installs prerequisites)
    - macOS: Self-contained single-file (~130MB total)
    - Droid: Always self-contained (uses Roslyn, not AOT-compatible)

    WSL Integration:
    - Automatically detects WSL and checks for .NET SDK + clang
    - Auto-installs prerequisites if missing (may prompt for sudo password)
    - Falls back to self-contained mode if WSL not available

    Note: Overlord собирается отдельно через Dockerfile.

.PARAMETER Version
    Version string (default: auto-detect from .csproj)

.PARAMETER Configuration
    Build configuration (default: Release)

.PARAMETER OutputDir
    Output directory for release archives (default: Run.Publish/Releases)

.PARAMETER SkipLinuxAot
    Skip Native AOT builds for Linux (use self-contained instead)

.EXAMPLE
    .\build-releases.ps1
    .\build-releases.ps1 -Version "3.0.8"
    .\build-releases.ps1 -SkipLinuxAot  # Force self-contained for Linux
#>

param(
    [string]$Version = "",
    [string]$Configuration = "Release",
    [string]$OutputDir = "Run.Publish/Releases",
    [switch]$SkipLinuxAot
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# WSL Configuration
$script:WslAvailable = $false
$script:WslDistro = ""
$script:WslDotnetReady = $false

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
function Write-Warn { param([string]$Text) Write-Host "[!] $Text" -ForegroundColor DarkYellow }
function Write-Err { param([string]$Text) Write-Host "[X] $Text" -ForegroundColor Red }

# ============================================================================
# WSL Functions for Linux NativeAOT builds
# ============================================================================

function Test-WslAvailable {
    try {
        $wslList = wsl --list --quiet 2>$null
        if ($LASTEXITCODE -eq 0 -and $wslList) {
            $distros = $wslList -split "`n" | Where-Object { $_ -match '\S' } | ForEach-Object { $_.Trim() -replace '\x00', '' }
            if ($distros.Count -gt 0) {
                $script:WslDistro = $distros[0]
                $script:WslAvailable = $true
                return $true
            }
        }
    } catch {
        # WSL not available
    }
    return $false
}

function Invoke-WslCommand {
    param(
        [string]$Command,
        [switch]$PassThru,
        [switch]$Silent
    )

    if (-not $Silent) {
        Write-Info "WSL: $Command"
    }

    if ($PassThru) {
        $result = wsl -d $script:WslDistro -- bash -c $Command 2>&1
        return $result
    } else {
        wsl -d $script:WslDistro -- bash -c $Command
        return $LASTEXITCODE -eq 0
    }
}

function Get-WslProjectPath {
    param([string]$WindowsPath)

    # Convert Windows path to WSL path
    # D:\github\project -> /mnt/d/github/project
    $path = $WindowsPath -replace '\\', '/'
    if ($path -match '^([A-Za-z]):(.*)$') {
        $drive = $Matches[1].ToLower()
        $rest = $Matches[2]
        return "/mnt/$drive$rest"
    }
    return $path
}

function Test-WslDotnetSdk {
    Write-Info "Checking .NET SDK in WSL..."

    $dotnetVersion = Invoke-WslCommand -Command "dotnet --version 2>/dev/null" -PassThru -Silent
    if ($dotnetVersion -and $dotnetVersion -match '^\d+\.\d+') {
        $majorVersion = [int]($dotnetVersion -split '\.')[0]
        if ($majorVersion -ge 9) {
            Write-Success "Found .NET SDK $dotnetVersion in WSL"
            return $true
        } else {
            Write-Warn ".NET SDK $dotnetVersion is too old (need 9.0+)"
            return $false
        }
    }

    Write-Warn ".NET SDK not found in WSL"
    return $false
}

function Test-WslAotPrerequisites {
    Write-Info "Checking NativeAOT prerequisites in WSL..."

    $checks = @{
        "clang" = "clang --version 2>/dev/null | head -1"
        "make" = "make --version 2>/dev/null | head -1"
        "zlib" = "dpkg -l zlib1g-dev 2>/dev/null | grep -q '^ii' && echo 'installed'"
    }

    $allPresent = $true
    foreach ($tool in $checks.Keys) {
        $result = Invoke-WslCommand -Command $checks[$tool] -PassThru -Silent
        if ($result) {
            Write-Success "  $tool - OK"
        } else {
            Write-Warn "  $tool - missing"
            $allPresent = $false
        }
    }

    return $allPresent
}

function Ensure-WslDns {
    # Check if DNS works (should work automatically with dnsTunneling=true in .wslconfig)
    $dnsTest = Invoke-WslCommand -Command "ping -c 1 -W 2 google.com >/dev/null 2>&1 && echo DNS_OK" -PassThru -Silent
    if ($dnsTest -match 'DNS_OK') {
        return $true
    }

    Write-Warn "WSL DNS not working. Add to %USERPROFILE%\.wslconfig:"
    Write-Info "  [experimental]"
    Write-Info "  dnsTunneling=true"
    Write-Info "Then run: wsl --shutdown"
    return $false
}

function Install-WslPrerequisites {
    Write-Header "WSL Prerequisites Missing"

    Write-Warn "Please install prerequisites manually in WSL terminal:"
    Write-Host ""
    Write-Host "  # Build tools" -ForegroundColor Gray
    Write-Host "  sudo apt-get update" -ForegroundColor White
    Write-Host "  sudo apt-get install -y clang build-essential zlib1g-dev curl" -ForegroundColor White
    Write-Host ""
    Write-Host "  # .NET 10 SDK" -ForegroundColor Gray
    Write-Host "  curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh" -ForegroundColor White
    Write-Host "  chmod +x /tmp/dotnet-install.sh" -ForegroundColor White
    Write-Host "  sudo /tmp/dotnet-install.sh --channel 10.0 --install-dir /usr/share/dotnet" -ForegroundColor White
    Write-Host "  sudo ln -sf /usr/share/dotnet/dotnet /usr/local/bin/dotnet" -ForegroundColor White
    Write-Host ""
    Write-Host "  # Verify" -ForegroundColor Gray
    Write-Host "  dotnet --version" -ForegroundColor White
    Write-Host ""

    return $false
}

function Initialize-WslForAot {
    if ($SkipLinuxAot) {
        Write-Info "Linux AOT skipped by parameter"
        return $false
    }

    Write-Header "Checking WSL for Linux NativeAOT builds"

    if (-not (Test-WslAvailable)) {
        Write-Warn "WSL not available - Linux builds will use self-contained mode"
        return $false
    }

    Write-Success "WSL available: $script:WslDistro"

    # Ensure DNS is working (needed for apt-get)
    Ensure-WslDns | Out-Null

    $dotnetReady = Test-WslDotnetSdk
    $aotReady = Test-WslAotPrerequisites

    if ($dotnetReady -and $aotReady) {
        Write-Success "WSL is ready for NativeAOT builds"
        $script:WslDotnetReady = $true
        return $true
    }

    # Auto-install missing prerequisites (may prompt for sudo password)
    Write-Info "Installing missing prerequisites (sudo may ask for password)..."
    if (Install-WslPrerequisites) {
        # Re-check after installation
        $dotnetReady = Test-WslDotnetSdk
        $aotReady = Test-WslAotPrerequisites

        if ($dotnetReady -and $aotReady) {
            $script:WslDotnetReady = $true
            return $true
        }
    }

    Write-Warn "Could not setup WSL prerequisites - Linux builds will use self-contained mode"
    return $false
}

function Invoke-WslNativeAotBuild {
    param(
        [string]$ProjectPath,
        [string]$RuntimeId,
        [string]$OutputPath
    )

    $wslProjectPath = Get-WslProjectPath -WindowsPath $ProjectPath
    $wslOutputPath = Get-WslProjectPath -WindowsPath $OutputPath

    Write-Info "Building in WSL: $wslProjectPath"

    # dotnet publish accepts full path to .csproj directly, no need to cd
    $buildCommand = "dotnet publish '$wslProjectPath' -c Release -r $RuntimeId -o '$wslOutputPath' /p:PublishAot=true"

    $result = Invoke-WslCommand -Command $buildCommand

    return $result
}

Write-Header "UltrasharpTools Multi-Platform Release Builder"

# Initialize WSL for Linux AOT builds
$UseWslForLinuxAot = Initialize-WslForAot

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

# Create releases directory
$ReleasesDir = Join-Path $ProjectRoot $OutputDir
if (Test-Path $ReleasesDir) {
    Write-Info "Cleaning existing releases directory..."
    Remove-Item $ReleasesDir -Recurse -Force
}
New-Item -ItemType Directory -Path $ReleasesDir -Force | Out-Null
Write-Success "Created: $ReleasesDir"

# Temporary directory for creating archives (avoids locking issues)
$ZipTempDir = Join-Path $ProjectRoot "Run.Publish.Zip"

# ============================================================================
# Build Comm (Cosmopolitan) once - works on all platforms
# ============================================================================
Write-Header "Building Comm (Cosmopolitan - cross-platform)"

$CommBuildScript = Join-Path $ProjectRoot "UltraSharpTools.Comm.C\build.ps1"
$CommBinary = Join-Path $ProjectRoot "UltraSharpTools.Comm.C\UltraSharpTools.com"

& $CommBuildScript -NoCopy
if ($LASTEXITCODE -ne 0) {
    Write-Err "Comm (Cosmopolitan) build failed"
    exit 1
}

if (-not (Test-Path $CommBinary)) {
    Write-Err "Comm binary not found: $CommBinary"
    exit 1
}

$CommSize = (Get-Item $CommBinary).Length / 1KB
Write-Success "Comm built: $([math]::Round($CommSize, 0)) KB (Cosmopolitan - works on all platforms)"

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
        # Each platform builds to its own directory to avoid file locking
        $platformOutput = Join-Path $ProjectRoot "Run.Publish.$rid"

        # Clean platform-specific output
        if (Test-Path $platformOutput) {
            Write-Info "Cleaning $rid output directory..."
            Remove-Item $platformOutput -Recurse -Force
        }

        # Build strategy (optimized for size and simplicity):
        # - Windows: Native AOT for VectorDB/Comm (~15MB + ~5MB)
        # - Linux (via WSL): Native AOT for VectorDB/Comm
        # - macOS/Other: Self-contained single-file (~130MB total)
        # - All components are single executable files - no shared runtime needed
        $useWindowsAot = $rid -like "win-*"
        $useLinuxAot = ($rid -like "linux-*") -and $UseWslForLinuxAot
        $useNativeAot = $useWindowsAot -or $useLinuxAot

        # Determine output paths
        $vectordbOutput = Join-Path $platformOutput "_temp_vectordb"
        $droidOutput = Join-Path $platformOutput "Droid"

        # ===== Build VectorDB =====
        if ($useLinuxAot) {
            Write-Info "Building VectorDB (Native AOT via WSL)..."
            $vectordbProject = "$ProjectRoot/UltraSharpTools.VectorDB/UltraSharpTools.VectorDB.csproj"
            $success = Invoke-WslNativeAotBuild -ProjectPath $vectordbProject -RuntimeId $rid -OutputPath $vectordbOutput
            if (-not $success) { throw "VectorDB WSL build failed" }
        } elseif ($useWindowsAot) {
            Write-Info "Building VectorDB (Native AOT)..."
            dotnet publish "$ProjectRoot\UltraSharpTools.VectorDB\UltraSharpTools.VectorDB.csproj" `
                -c Release `
                -r $rid `
                -o $vectordbOutput `
                /p:PublishAot=true | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "VectorDB build failed" }
        } else {
            Write-Info "Building VectorDB (self-contained single-file)..."
            dotnet publish "$ProjectRoot\UltraSharpTools.VectorDB\UltraSharpTools.VectorDB.csproj" `
                -c Release `
                -r $rid `
                --self-contained true `
                -o $vectordbOutput `
                /p:PublishSingleFile=true `
                /p:EnableCompressionInSingleFile=true | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "VectorDB build failed" }
        }

        # ===== Build Droid =====
        # Droid is always self-contained (uses Roslyn, not AOT-compatible)
        if ($useLinuxAot) {
            Write-Info "Building Droid (self-contained via WSL)..."
            $droidProject = "$ProjectRoot/UltrasharpTools.Droid/UltrasharpTools.Droid.csproj"
            $wslDroidProject = Get-WslProjectPath -WindowsPath $droidProject
            $wslDroidOutput = Get-WslProjectPath -WindowsPath $droidOutput
            $buildCommand = "dotnet publish '$wslDroidProject' -c Release -r $rid --self-contained true -o '$wslDroidOutput' /p:PublishSingleFile=true /p:EnableCompressionInSingleFile=true"
            $success = Invoke-WslCommand -Command $buildCommand
            if (-not $success) { throw "Droid WSL build failed" }
        } else {
            Write-Info "Building Droid (self-contained single-file)..."
            dotnet publish "$ProjectRoot\UltrasharpTools.Droid\UltrasharpTools.Droid.csproj" `
                -c Release `
                -r $rid `
                --self-contained true `
                -o $droidOutput `
                /p:PublishSingleFile=true `
                /p:EnableCompressionInSingleFile=true | Out-Null
            if ($LASTEXITCODE -ne 0) { throw "Droid build failed" }
        }

        # Copy BuildHost-netcore (required by MSBuildWorkspace for loading solutions)
        $BuildHostSource = Join-Path $ProjectRoot "UltrasharpTools.Droid\bin\Release\net10.0\$rid\BuildHost-netcore"
        if (-not (Test-Path $BuildHostSource)) {
            $BuildHostSource = Join-Path $ProjectRoot "UltrasharpTools.Droid\bin\Release\net10.0\BuildHost-netcore"
        }
        if (Test-Path $BuildHostSource) {
            Copy-Item -Path $BuildHostSource -Destination (Join-Path $droidOutput "BuildHost-netcore") -Recurse -Force
            Write-Info "Copied BuildHost-netcore"
        }

        # Copy VectorDB to Droid folder
        Copy-Item -Path (Join-Path $platformOutput "_temp_vectordb\*") -Destination $droidOutput -Recurse -Force

        # Copy Cosmopolitan Comm binary (already built once, works on all platforms)
        Copy-Item -Path $CommBinary -Destination $droidOutput -Force

        # Cleanup temp folders
        Remove-Item (Join-Path $platformOutput "_temp_vectordb") -Recurse -Force

        # Copy to archive temp directory
        Write-Info "Preparing archive..."
        if (Test-Path $ZipTempDir) {
            Remove-Item $ZipTempDir -Recurse -Force
        }
        Copy-Item -Path $droidOutput -Destination $ZipTempDir -Recurse -Force

        # Create archive name
        $archiveName = "ultrasharp-tools-v$Version-$os-$arch"

        Write-Info "Creating archive: $archiveName.$archiveType"

        if ($archiveType -eq "zip") {
            # Windows: ZIP archive
            $zipPath = Join-Path $ReleasesDir "$archiveName.zip"
            Compress-Archive -Path "$ZipTempDir/*" -DestinationPath $zipPath -Force
            $fileSize = (Get-Item $zipPath).Length / 1MB
            Write-Success "Created: $archiveName.zip ($([math]::Round($fileSize, 2)) MB)"
        }
        else {
            # Linux/macOS: tar.gz archive
            $tarPath = Join-Path $ReleasesDir "$archiveName.tar.gz"

            Push-Location $ZipTempDir
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

        # Cleanup temp directory and platform output
        Remove-Item $ZipTempDir -Recurse -Force -ErrorAction SilentlyContinue
        Remove-Item $platformOutput -Recurse -Force -ErrorAction SilentlyContinue

        $SuccessCount++
    }
    catch {
        Write-Err "Failed to build ${rid}: ${_}"
        $FailCount++

        # Cleanup on failure
        if (Test-Path $ZipTempDir) {
            Remove-Item $ZipTempDir -Recurse -Force -ErrorAction SilentlyContinue
        }
        if (Test-Path $platformOutput) {
            Remove-Item $platformOutput -Recurse -Force -ErrorAction SilentlyContinue
        }
    }
}

# Cleanup temp directory (final cleanup)
if (Test-Path $ZipTempDir) {
    Remove-Item $ZipTempDir -Recurse -Force -ErrorAction SilentlyContinue
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
