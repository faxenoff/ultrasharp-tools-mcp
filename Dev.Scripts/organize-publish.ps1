#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Organize published files into clean structure
.DESCRIPTION
    Displays final directory structure after publishing
.PARAMETER PublishDir
    Path to publish directory (default: Run.Publish/MCPServer)
#>

param(
    [string]$PublishDir = "Run.Publish\MCPServer"
)

$ErrorActionPreference = "Stop"

Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Organizing Published Files" -ForegroundColor Cyan
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Resolve publish directory
if (!(Test-Path $PublishDir)) {
    Write-Host "✗ Publish directory not found: $PublishDir" -ForegroundColor Red
    exit 1
}

$PublishDir = Resolve-Path $PublishDir
Write-Host "✓ Publish directory: $PublishDir" -ForegroundColor Green
Write-Host ""

# Verify structure
$configDir = Join-Path $PublishDir "Config"
$scriptsDir = Join-Path $PublishDir "Scripts"

$hasConfig = Test-Path $configDir
$hasScripts = Test-Path $scriptsDir

if ($hasConfig) {
    Write-Host "✓ Config/ directory exists" -ForegroundColor Green
}
if ($hasScripts) {
    Write-Host "✓ Scripts/ directory exists" -ForegroundColor Green
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host "Organization Complete!" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host ""

Write-Host "Root directory structure:" -ForegroundColor Cyan
Write-Host ""
Write-Host "  MCPServer/" -ForegroundColor White
Write-Host "  ├── UltrasharpTools.MCPServer.exe      (Main executable)" -ForegroundColor Green
Write-Host "  ├── *.dll, *.pdb                        (Runtime files)" -ForegroundColor DarkGray
Write-Host "  │" -ForegroundColor DarkGray
Write-Host "  ├── setup-semantic-embedding.ps1        (Setup script)" -ForegroundColor Yellow
Write-Host "  ├── setup-semantic-embedding.cmd        (Double-click to setup!)" -ForegroundColor Yellow
Write-Host "  ├── validate-semantic-config.cmd        (Check configuration)" -ForegroundColor Yellow
Write-Host "  │" -ForegroundColor DarkGray
Write-Host "  ├── README.md                           (Quick start guide)" -ForegroundColor Cyan
Write-Host "  ├── SEMANTIC_SETUP_GUIDE.md             (Detailed guide)" -ForegroundColor Cyan
Write-Host "  │" -ForegroundColor DarkGray
Write-Host "  ├── semantic-config.json                (Created by setup)" -ForegroundColor Magenta
Write-Host "  │" -ForegroundColor DarkGray
Write-Host "  ├── Scripts/                            (Support scripts)" -ForegroundColor White
Write-Host "  │   ├── setup-tei.ps1" -ForegroundColor DarkGray
Write-Host "  │   ├── detect-gpu-architecture.ps1" -ForegroundColor DarkGray
Write-Host "  │   ├── validate-semantic-config.ps1" -ForegroundColor DarkGray
Write-Host "  │   └── ..." -ForegroundColor DarkGray
Write-Host "  │" -ForegroundColor DarkGray
Write-Host "  └── Config/                             (Example config)" -ForegroundColor White
Write-Host "      └── semantic-config.yaml" -ForegroundColor DarkGray
Write-Host ""
Write-Host "First Time Setup:" -ForegroundColor Yellow
Write-Host "  Double-click: setup-semantic-embedding.cmd" -ForegroundColor Green
Write-Host ""
