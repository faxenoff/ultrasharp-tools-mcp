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

# Publish MCPServer (автоматически соберёт UltrasharpTools.Tools)
Write-Header "Publishing MCPServer with R2R"
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
