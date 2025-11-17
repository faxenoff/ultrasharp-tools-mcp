#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Build only MCPServer for Claude Code / MCP clients
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

Write-Header "UltrasharpTools MCPServer Build (MCP)"
Write-Info "Configuration: $Configuration"
Write-Info "Runtime: $Runtime"
Write-Info "ReadyToRun: Enabled (non-composite)"

# Clean previous artifacts
Write-Header "Cleaning previous artifacts"
$publishDir = "Run.Publish"
$mcpOutput = Join-Path $publishDir "MCPServer"

if (Test-Path $mcpOutput) {
    Remove-Item $mcpOutput -Recurse -Force
    Write-Success "Removed $mcpOutput directory"
}

# Publish MCPServer (automatically builds UltrasharpTools.Tools)
Write-Header "Publishing MCPServer with R2R"
dotnet publish UltrasharpTools.MCPServer/UltrasharpTools.MCPServer.csproj `
    -c $Configuration `
    -r $Runtime `
    --self-contained false `
    -o $mcpOutput `
    -p:PublishReadyToRun=true `
    -p:PublishReadyToRunComposite=false

if ($LASTEXITCODE -eq 0) {
    # Clean up BuildHost directories
    $buildHostDirs = Get-ChildItem -Path $mcpOutput -Directory -Filter "BuildHost-*"
    foreach ($dir in $buildHostDirs) {
        Remove-Item $dir.FullName -Recurse -Force
        Write-Success "Removed: $($dir.Name)"
    }

    $mcpSize = (Get-ChildItem $mcpOutput -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
    Write-Success "MCPServer published successfully ($([math]::Round($mcpSize, 2)) MB)"
} else {
    Write-Error "MCPServer publish failed"
    exit 1
}

# Copy setup scripts and configs to publish directory
Write-Header "Organizing Documentation and Scripts"

$runConfigDir = Join-Path $ProjectRoot "Run.Config"

# Create Scripts subdirectory for support scripts
$scriptsDir = Join-Path $mcpOutput "Scripts"
if (!(Test-Path $scriptsDir)) {
    New-Item -ItemType Directory -Path $scriptsDir | Out-Null
    Write-Success "Created: Scripts/ directory"
}

# Copy Dev.Scripts contents to Scripts/
$devScriptsPath = Join-Path $ProjectRoot "Dev.Scripts"
if (Test-Path $devScriptsPath) {
    Get-ChildItem -Path $devScriptsPath -File | ForEach-Object {
        Copy-Item -Path $_.FullName -Destination $scriptsDir -Force
        Write-Success "Copied to Scripts/: $($_.Name)"
    }
}

# Create Config subdirectory for setup and configs
$configDir = Join-Path $mcpOutput "Config"
if (!(Test-Path $configDir)) {
    New-Item -ItemType Directory -Path $configDir | Out-Null
    Write-Success "Created: Config/ directory"
}

# Copy setup files to Config/
$configSetupFiles = @(
    @{ Source = (Join-Path $ProjectRoot "setup-semantic-embedding.cmd"); Name = "setup-semantic-embedding.cmd" },
    @{ Source = (Join-Path $runConfigDir "validate-semantic-config.cmd"); Name = "validate-semantic-config.cmd" },
    @{ Source = (Join-Path $ProjectRoot "SEMANTIC_SETUP_GUIDE.md"); Name = "SEMANTIC_SETUP_GUIDE.md" },
    @{ Source = (Join-Path $runConfigDir "semantic-config.yaml"); Name = "semantic-config.yaml" }
)

foreach ($fileInfo in $configSetupFiles) {
    if (Test-Path $fileInfo.Source) {
        Copy-Item -Path $fileInfo.Source -Destination (Join-Path $configDir $fileInfo.Name) -Force
        Write-Success "Copied to Config/: $($fileInfo.Name)"
    }
}

# Create Read.me subdirectory for documentation
$readmeDir = Join-Path $mcpOutput "Read.me"
if (!(Test-Path $readmeDir)) {
    New-Item -ItemType Directory -Path $readmeDir | Out-Null
    Write-Success "Created: Read.me/ directory"
}

# Copy README files to Read.me/
$publishReadme = Join-Path $ProjectRoot "PUBLISH_README.md"
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
Write-Info "Executable: $mcpOutput\UltrasharpTools.MCPServer.exe"
Write-Success "MCP server build completed!"

# Display info
Write-Header "Usage"
Write-Info "Add to Claude Code config:"
Write-Host @"

{
  "mcpServers": {
    "SharpTools": {
      "command": "$(Resolve-Path $mcpOutput)\UltrasharpTools.MCPServer.exe",
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
