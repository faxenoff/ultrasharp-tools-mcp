#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Production build script with ReadyToRun (R2R) compilation
.DESCRIPTION
    Publishes RemoteServer and MCPServer with AOT compilation for optimal startup performance
.PARAMETER Configuration
    Build configuration (default: Release)
.PARAMETER Runtime
    Target runtime identifier (default: win-x64)
.EXAMPLE
    .\build-production.ps1
.EXAMPLE
    .\build-production.ps1 -Runtime linux-x64
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

Write-Header "SharpToolsMCP Production Build"
Write-Info "Configuration: $Configuration"
Write-Info "Runtime: $Runtime"
Write-Info "ReadyToRun: Enabled"

# Clean previous artifacts
Write-Header "Cleaning previous artifacts"
$publishDir = "Run.Publish"
if (Test-Path $publishDir) {
    Remove-Item $publishDir -Recurse -Force
    Write-Success "Removed $publishDir directory"
}

# Publish RemoteServer
Write-Header "Publishing RemoteServer with R2R"
$remoteOutput = Join-Path $publishDir "RemoteServer"
dotnet publish UltrasharpTools.RemoteServer/UltrasharpTools.RemoteServer.csproj `
    -c $Configuration `
    -r $Runtime `
    --self-contained false `
    -o $remoteOutput `
    -p:PublishReadyToRun=true `
    -p:PublishReadyToRunComposite=false

if ($LASTEXITCODE -eq 0) {
    $remoteSize = (Get-ChildItem $remoteOutput -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
    Write-Success "RemoteServer published successfully ($([math]::Round($remoteSize, 2)) MB)"
} else {
    Write-Error "RemoteServer publish failed"
    exit 1
}

# Publish MCPServer
Write-Header "Publishing MCPServer with R2R"
$mcpOutput = Join-Path $publishDir "MCPServer"
dotnet publish UltrasharpTools.MCPServer/UltrasharpTools.MCPServer.csproj `
    -c $Configuration `
    -r $Runtime `
    --self-contained false `
    -o $mcpOutput `
    -p:PublishReadyToRun=true `
    -p:PublishReadyToRunComposite=false

if ($LASTEXITCODE -eq 0) {
    $mcpSize = (Get-ChildItem $mcpOutput -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
    Write-Success "MCPServer published successfully ($([math]::Round($mcpSize, 2)) MB)"
} else {
    Write-Error "MCPServer publish failed"
    exit 1
}

# Summary
Write-Header "Build Summary"
Write-Info "Output directory: $publishDir"
Write-Info "RemoteServer: $remoteOutput"
Write-Info "MCPServer: $mcpOutput"
Write-Success "Production build completed successfully!"

# Display R2R info
Write-Header "ReadyToRun Info"
Write-Info "AOT-compiled main assemblies provide faster startup time"
Write-Info "Note: Composite mode disabled - Roslyn libraries incompatible with R2R"
Write-Info "Only project assemblies are R2R-compiled, dependencies use JIT"
