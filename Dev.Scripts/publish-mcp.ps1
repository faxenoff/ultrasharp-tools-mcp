#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build only Droid for Claude Code / MCP clients
.PARAMETER Configuration
    Build configuration (default: Release)
.PARAMETER Runtime
    Target runtime identifier (default: win-x64)
#>

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Find project root (directory containing .sln files)
function Find-ProjectRoot {
    $current = $PSScriptRoot
    if (-not $current) { $current = Get-Location }

    while ($current) {
        if (Test-Path (Join-Path $current "*.sln")) {
            return $current
        }
        $parent = Split-Path -Parent $current
        if ($parent -eq $current) { break }  # Reached filesystem root
        $current = $parent
    }

    throw "Could not find project root (directory with .sln file)"
}

$ProjectRoot = Find-ProjectRoot
Set-Location $ProjectRoot
Write-Host "→ Working from: $ProjectRoot" -ForegroundColor DarkGray

# Colors
function Write-Header { param([string]$Text) Write-Host "`n=== $Text ===" -ForegroundColor Cyan }
function Write-Success { param([string]$Text) Write-Host "✓ $Text" -ForegroundColor Green }
function Write-Info { param([string]$Text) Write-Host "→ $Text" -ForegroundColor Yellow }
function Write-Error { param([string]$Text) Write-Host "✗ $Text" -ForegroundColor Red }

Write-Header "UltrasharpTools Droid Build (MCP)"
Write-Info "Configuration: $Configuration"
Write-Info "Runtime: $Runtime"

# Configure R2R based on configuration
$useR2R = $Configuration -ne "Debug"
if ($useR2R) {
    Write-Info "ReadyToRun: Enabled (non-composite)"
} else {
    Write-Info "ReadyToRun: Disabled (Debug mode)"
    Write-Info "PDB Files: Will be kept for debugging"
}

# Prepare output directory
Write-Header "Preparing output directory"
$publishDir = "Run.Publish"
$mcpOutput = Join-Path $publishDir "Droid"

if (-not (Test-Path $publishDir)) {
    New-Item -ItemType Directory -Path $publishDir | Out-Null
    Write-Success "Created $publishDir directory"
}

# Publish Droid (automatically builds UltrasharpTools.Tools)
if ($useR2R) {
    Write-Header "Publishing Droid with R2R"
} else {
    Write-Header "Publishing Droid (Debug, no R2R)"
}

dotnet publish UltrasharpTools.Droid/UltrasharpTools.Droid.csproj `
    -c $Configuration `
    -r $Runtime `
    --self-contained false `
    -o $mcpOutput `
    -p:PublishReadyToRun=$useR2R `
    -p:PublishReadyToRunComposite=false

if ($LASTEXITCODE -eq 0) {
    # Clean up PDB files from publish output (only for Release builds)
    if ($Configuration -ne "Debug") {
        $pdbFiles = Get-ChildItem -Path $mcpOutput -Filter "*.pdb" -Recurse
        $pdbCount = $pdbFiles.Count
        if ($pdbCount -gt 0) {
            $pdbFiles | Remove-Item -Force
            Write-Success "Cleaned up PDB files from publish output"
        }
    } else {
        $pdbFiles = Get-ChildItem -Path $mcpOutput -Filter "*.pdb" -Recurse
        $pdbCount = $pdbFiles.Count
        Write-Success "Kept $pdbCount PDB files for debugging"
    }

    # Verify BuildHost directories exist (required for MSBuild)
    $netcoreBuildHost = Join-Path $mcpOutput "BuildHost-netcore"
    $net472BuildHost = Join-Path $mcpOutput "BuildHost-net472"

    if (Test-Path $netcoreBuildHost) {
        Write-Success "Kept: BuildHost-netcore (for .NET Core/5+/10 projects)"
    } else {
        Write-Warning "BuildHost-netcore not found - modern .NET project loading may fail!"
    }

    if (Test-Path $net472BuildHost) {
        Write-Success "Kept: BuildHost-net472 (for .NET Framework projects)"
    } else {
        Write-Warning "BuildHost-net472 not found - .NET Framework project loading may fail!"
    }

    $mcpSize = (Get-ChildItem $mcpOutput -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
    Write-Success "Droid published successfully ($([math]::Round($mcpSize, 2)) MB)"
} else {
    Write-Error "Droid publish failed"
    exit 1
}

# Copy Config directory to publish
Write-Header "Organizing Documentation and Scripts"

$runConfigDir = Join-Path $ProjectRoot "Run.Config"
$sourceConfigDir = Join-Path $runConfigDir "Config"

# Copy entire Config directory structure
$targetConfigDir = Join-Path $mcpOutput "Config"
if (Test-Path $sourceConfigDir) {
    # Remove old Config if exists
    if (Test-Path $targetConfigDir) {
        Remove-Item -Path $targetConfigDir -Recurse -Force
    }

    # Copy Config directory recursively
    Copy-Item -Path $sourceConfigDir -Destination $targetConfigDir -Recurse -Force
    Write-Success "Copied Config/ directory with all contents"

    # List what was copied
    $copiedFiles = Get-ChildItem -Path $targetConfigDir -Recurse -File
    foreach ($file in $copiedFiles) {
        $relativePath = $file.FullName.Substring($targetConfigDir.Length + 1)
        Write-Success "  → Config/$relativePath"
    }
}

# Copy setup guide if exists
$setupGuide = Join-Path $ProjectRoot "SEMANTIC_SETUP_GUIDE.md"
if (Test-Path $setupGuide) {
    Copy-Item -Path $setupGuide -Destination $targetConfigDir -Force
    Write-Success "  → Config/SEMANTIC_SETUP_GUIDE.md"
}

# Create Read.me subdirectory for documentation
$readmeDir = Join-Path $mcpOutput "Read.me"
if (!(Test-Path $readmeDir)) {
    New-Item -ItemType Directory -Path $readmeDir | Out-Null
    Write-Success "Created: Read.me/ directory"
}

# Copy README files to Read.me/
$publishReadme = Join-Path $ProjectRoot "Run.Docs/PUBLISH_README.md"
if (Test-Path $publishReadme) {
    # Main README.md in Read.me/
    Copy-Item -Path $publishReadme -Destination (Join-Path $readmeDir "README.md") -Force
    Write-Success "Copied to Read.me/: README.md"

    # Also keep PUBLISH_README.md for reference
    Copy-Item -Path $publishReadme -Destination (Join-Path $readmeDir "PUBLISH_README.md") -Force
    Write-Success "Copied to Read.me/: PUBLISH_README.md"
}

# Copy main README.md from root to Read.me/
$mainReadme = Join-Path $ProjectRoot "README.md"
if (Test-Path $mainReadme) {
    Copy-Item -Path $mainReadme -Destination (Join-Path $readmeDir "README_FULL.md") -Force
    Write-Success "Copied to Read.me/: README_FULL.md"
}

# Organize published files
Write-Header "Organizing Published Files"
$organizeScript = Join-Path $ProjectRoot "Dev.Scripts/organize-publish.ps1"
if (Test-Path $organizeScript) {
    & $organizeScript -PublishDir $mcpOutput
} else {
    Write-Info "Organize script not found, skipping file organization"
}

# Summary
Write-Header "Build Summary"
Write-Info "Output: $mcpOutput"
Write-Info "Executable: $mcpOutput\UltrasharpTools.Droid.exe"
Write-Success "MCP server build completed!"

# Display info
Write-Header "Usage"
Write-Info "Add to Claude Code config:"
Write-Host @"

{
  "mcpServers": {
    "SharpTools": {
      "command": "$(Resolve-Path $mcpOutput)\UltrasharpTools.Droid.exe",
      "args": [
        "--log-directory",
        "$(Resolve-Path .)\Run.Logs",
        "--log-level",
        "Information"
      ]
    }
  }
}
"@ -ForegroundColor Gray

Write-Host ""
Write-Header "First Time Setup"
Write-Info "Configure semantic embedding (required for semantic search):"
Write-Host "  cd $(Resolve-Path $mcpOutput)" -ForegroundColor Cyan
Write-Host "  .\Config\setup-semantic-embedding.cmd" -ForegroundColor Cyan
Write-Host ""
Write-Info "Or double-click: Config\setup-semantic-embedding.cmd in $mcpOutput" -ForegroundColor Yellow
Write-Host ""
Write-Info "Documentation: Read.me\README.md" -ForegroundColor Gray
