#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Organize published files into clean structure
.DESCRIPTION
    Displays final directory structure after publishing
.PARAMETER PublishDir
    Path to publish directory (default: Run.Publish/Droid)
#>

param(
    [string]$PublishDir = "Run.Publish\Droid"
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

$hasConfig = Test-Path $configDir

if ($hasConfig) {
    Write-Host "✓ Config/ directory exists" -ForegroundColor Green
}

Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host "Organization Complete!" -ForegroundColor Green
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host ""

Write-Host "Root directory structure:" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Droid/" -ForegroundColor White
Write-Host "  ├── UltrasharpTools.Droid.exe           (Main executable)" -ForegroundColor Green
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
Write-Host "  └── Config/                             (Setup scripts & configs)" -ForegroundColor White
Write-Host "      ├── setup-tei.ps1                   (TEI setup)" -ForegroundColor DarkGray
Write-Host "      ├── setup-ollama.ps1                (Ollama setup)" -ForegroundColor DarkGray
Write-Host "      ├── detect-gpu-architecture.ps1     (GPU detection)" -ForegroundColor DarkGray
Write-Host "      ├── setup-embeddings-interactive.sh (Interactive setup)" -ForegroundColor DarkGray
Write-Host "      ├── embedding-models.json           (Model configs)" -ForegroundColor DarkGray
Write-Host "      └── semantic-*.json                 (Semantic configs)" -ForegroundColor DarkGray
Write-Host ""
Write-Host "First Time Setup:" -ForegroundColor Yellow
Write-Host "  Double-click: setup-semantic-embedding.cmd" -ForegroundColor Green
Write-Host ""
