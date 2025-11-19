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

# Determine config path - parent directory (Config)
# When running from Config/Scripts/, parent is Config/
$configDir = Split-Path -Parent $PSScriptRoot
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
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "Setting Up Embedding Service" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

if ($platform -eq "tei") {
    Write-Host "[TEI Setup]" -ForegroundColor Yellow
    Write-Host ""

    $setupTei = Join-Path $PSScriptRoot "setup-tei.ps1"
    if (Test-Path $setupTei) {
        Write-Host "Launching TEI setup..." -ForegroundColor Cyan
        Write-Host ""

        $choice = Read-Host "Start TEI server now? [y/N]"
        if ($choice -eq "y" -or $choice -eq "Y") {
            & $setupTei -Architecture $architecture
            Write-Host ""
            Write-Host "✓ TEI server setup completed" -ForegroundColor Green
        } else {
            Write-Host "Skipped. Run manually: .\Scripts\setup-tei.ps1 -Architecture $architecture" -ForegroundColor Yellow
        }
    } else {
        Write-Host "⚠ setup-tei.ps1 not found" -ForegroundColor Yellow
        Write-Host "Manual setup required for TEI" -ForegroundColor Gray
    }

} elseif ($platform -eq "ollama") {
    Write-Host "[Ollama Setup]" -ForegroundColor Yellow
    Write-Host ""

    # Check if Ollama is installed
    try {
        $ollamaVersion = ollama --version 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ Ollama is installed: $ollamaVersion" -ForegroundColor Green
        } else {
            throw "Ollama not found"
        }
    } catch {
        Write-Host "✗ Ollama is not installed" -ForegroundColor Red
        Write-Host ""
        Write-Host "Please install Ollama:" -ForegroundColor Yellow
        Write-Host "  1. Visit: https://ollama.ai" -ForegroundColor Cyan
        Write-Host "  2. Download and install for your OS" -ForegroundColor Cyan
        Write-Host "  3. Re-run this setup script" -ForegroundColor Cyan
        Write-Host ""
        exit 1
    }

    Write-Host ""

    # Check if model is available
    $modelName = "granite-embedding"
    Write-Host "Checking for model: $modelName..." -ForegroundColor Cyan

    try {
        $models = ollama list 2>&1
        if ($models -match $modelName) {
            Write-Host "✓ Model '$modelName' is already available" -ForegroundColor Green
        } else {
            Write-Host "Model '$modelName' not found" -ForegroundColor Yellow
            Write-Host ""

            $pullChoice = Read-Host "Download $modelName model now? (requires ~250MB) [y/N]"
            if ($pullChoice -eq "y" -or $pullChoice -eq "Y") {
                Write-Host ""
                Write-Host "Pulling model (this may take a few minutes)..." -ForegroundColor Cyan
                ollama pull $modelName

                if ($LASTEXITCODE -eq 0) {
                    Write-Host ""
                    Write-Host "✓ Model pulled successfully" -ForegroundColor Green
                } else {
                    Write-Host ""
                    Write-Host "✗ Failed to pull model" -ForegroundColor Red
                    Write-Host "Try manually: ollama pull $modelName" -ForegroundColor Yellow
                }
            } else {
                Write-Host "Skipped. Run manually: ollama pull $modelName" -ForegroundColor Yellow
            }
        }
    } catch {
        Write-Host "⚠ Could not check Ollama models" -ForegroundColor Yellow
    }

} elseif ($platform -eq "memory") {
    Write-Host "[Memory Setup]" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "✓ No additional setup required for in-memory embeddings" -ForegroundColor Green
}

Write-Host ""
Write-Host "============================================================" -ForegroundColor Green
Write-Host "✓ Setup Complete!" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host ""
Write-Host "Configuration: $configPath" -ForegroundColor Cyan
Write-Host "Platform: $platform" -ForegroundColor Cyan
Write-Host "Architecture: $architecture" -ForegroundColor Cyan
Write-Host ""
Write-Host "Environment variables can override config:" -ForegroundColor Gray
Write-Host "  SEMANTIC_PLATFORM, SEMANTIC_ARCHITECTURE" -ForegroundColor DarkGray
Write-Host "  TEI_ENDPOINT, OLLAMA_ENDPOINT" -ForegroundColor DarkGray
Write-Host ""
