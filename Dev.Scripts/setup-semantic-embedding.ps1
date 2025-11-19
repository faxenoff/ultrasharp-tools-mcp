#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Interactive setup for Semantic Embedding configuration
.DESCRIPTION
    Walks user through configuration setup with auto-detection
#>

param(
    [switch]$SkipGpuDetection,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# Determine script location and config path
$scriptLocation = Split-Path -Leaf $PSScriptRoot

if ($scriptLocation -eq "Dev.Scripts") {
    Write-Host "============================================================" -ForegroundColor Red
    Write-Host "ERROR: Setup must be run from published build" -ForegroundColor Red
    Write-Host "============================================================" -ForegroundColor Red
    Write-Host ""
    Write-Host "This setup script should be run from:" -ForegroundColor Yellow
    Write-Host "  Publish\Droid\Scripts\setup-semantic-embedding.cmd" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Or copy the published build to another location and run from there." -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

# When in Scripts folder (Publish\Droid\Scripts), Config is at ../Config
$configDir = Join-Path $PSScriptRoot ".." "Config"
if (!(Test-Path $configDir)) {
    New-Item -ItemType Directory -Path $configDir -Force | Out-Null
}

$configPath = Join-Path $configDir "semantic-config.json"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "Semantic Embedding Configuration Setup" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

if ((Test-Path $configPath) -and !$Force) {
    Write-Host "✓ Configuration already exists: $configPath" -ForegroundColor Green
    Write-Host ""
    $overwrite = Read-Host "Overwrite existing configuration? [y/N]"
    if ($overwrite -ne "y" -and $overwrite -ne "Y") {
        Write-Host "Setup cancelled." -ForegroundColor Yellow
        exit 0
    }
}

# Step 1: Detect GPU architecture
Write-Host "[1/3] GPU Architecture Detection" -ForegroundColor Yellow
Write-Host ""

$architecture = "cpu"
if (!$SkipGpuDetection) {
    # Look for detect script in same directory (Dev.Scripts or Scripts)
    $detectScript = Join-Path $PSScriptRoot "detect-gpu-architecture.ps1"

    if (Test-Path $detectScript) {
        $architecture = & $detectScript
        if ($LASTEXITCODE -ne 0) {
            $architecture = "cpu"
        }
    } else {
        Write-Host "⚠ detect-gpu-architecture.ps1 not found, defaulting to CPU" -ForegroundColor Yellow
    }
} else {
    Write-Host "Skipping GPU detection (--SkipGpuDetection)" -ForegroundColor Gray
    $architecture = "cpu"
}

Write-Host ""

# Step 2: Select platform
Write-Host "[2/3] Platform Selection" -ForegroundColor Yellow
Write-Host ""
Write-Host "Available platforms:"
Write-Host "  1) TEI (Text Embeddings Inference) - Docker-based, high performance"
Write-Host "  2) Ollama - Easy setup, good performance"
Write-Host "  3) Memory - In-process (experimental)"
Write-Host ""

$platformChoice = Read-Host "Select platform [1-3] (default: 1)"
if ([string]::IsNullOrWhiteSpace($platformChoice)) { $platformChoice = "1" }

$platform = switch ($platformChoice) {
    "1" { "tei" }
    "2" { "ollama" }
    "3" { "memory" }
    default { "tei" }
}

Write-Host "  Selected: $platform" -ForegroundColor Cyan
Write-Host ""

# Step 3: Configure endpoints
Write-Host "[3/3] Endpoint Configuration" -ForegroundColor Yellow
Write-Host ""

$teiEndpoint = "http://localhost:8080"
$ollamaEndpoint = "http://localhost:11434"

if ($platform -eq "tei") {
    $customTei = Read-Host "TEI endpoint (default: $teiEndpoint)"
    if (![string]::IsNullOrWhiteSpace($customTei)) {
        $teiEndpoint = $customTei
    }
} elseif ($platform -eq "ollama") {
    $customOllama = Read-Host "Ollama endpoint (default: $ollamaEndpoint)"
    if (![string]::IsNullOrWhiteSpace($customOllama)) {
        $ollamaEndpoint = $customOllama
    }
}

# Create configuration
Write-Host ""
Write-Host "Creating configuration..." -ForegroundColor Yellow

$config = @{
    embedding = @{
        platform = $platform
        architecture = $architecture
        tei = @{
            endpoint = $teiEndpoint
            models = @(
                @{
                    id = "sentence-transformers/all-MiniLM-L6-v2"
                    languages = @("english")
                    vector_size = 384
                },
                @{
                    id = "sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2"
                    languages = @("multilingual")
                    vector_size = 384
                }
            )
            selected_model = "sentence-transformers/all-MiniLM-L6-v2"
        }
        ollama = @{
            endpoint = $ollamaEndpoint
            models = @(
                @{
                    id = "granite-embedding:latest"
                    languages = @("english")
                    vector_size = 384
                },
                @{
                    id = "mxbai-embed-large:latest"
                    languages = @("multilingual")
                    vector_size = 1024
                }
            )
            selected_model = "granite-embedding:latest"
        }
        memory = @{
            model_path = "./models/embedding"
            vector_size = 384
        }
    }
    auto_detection = @{
        gpu_architecture = $true
        language = $true
        codebase_size = $true
    }
}

$json = $config | ConvertTo-Json -Depth 10
Set-Content -Path $configPath -Value $json -Encoding UTF8

Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host "✓ Configuration Created Successfully!" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Configuration saved to: $configPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "Summary:" -ForegroundColor Yellow
Write-Host "  Platform: $platform" -ForegroundColor White
Write-Host "  Architecture: $architecture" -ForegroundColor White
if ($platform -eq "tei") {
    Write-Host "  TEI Endpoint: $teiEndpoint" -ForegroundColor White
} elseif ($platform -eq "ollama") {
    Write-Host "  Ollama Endpoint: $ollamaEndpoint" -ForegroundColor White
}
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow

# Script is in Dev.Scripts or Scripts directory
$scriptsFolder = Split-Path -Leaf $PSScriptRoot

if ($platform -eq "tei") {
    Write-Host "  1. Start TEI server: .\$scriptsFolder\setup-tei.ps1" -ForegroundColor White
    Write-Host "  2. Run your MCP server" -ForegroundColor White
} elseif ($platform -eq "ollama") {
    Write-Host "  1. Install Ollama: https://ollama.ai" -ForegroundColor White
    Write-Host "  2. Pull model: ollama pull granite-embedding" -ForegroundColor White
    Write-Host "  3. Run your MCP server" -ForegroundColor White
}
Write-Host ""
Write-Host "Environment variables can override config:" -ForegroundColor Gray
Write-Host "  SEMANTIC_PLATFORM, SEMANTIC_ARCHITECTURE" -ForegroundColor DarkGray
Write-Host "  TEI_ENDPOINT, OLLAMA_ENDPOINT" -ForegroundColor DarkGray
Write-Host ""
