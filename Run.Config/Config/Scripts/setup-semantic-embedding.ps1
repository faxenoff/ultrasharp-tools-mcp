#!/usr/bin/env pwsh
# ==============================================================================
# UltrasharpTools MCP - Semantic Embedding Setup (PowerShell)
# ==============================================================================
# This script provides interactive setup for local embedding providers:
#   - TEI (Text Embeddings Inference) with Docker
#   - Ollama (native installation)
#   - Memory provider (no ML, hash-based)
#
# Features:
#   - Loads models from Config/embedding-models.json
#   - Auto-detects GPU capabilities
#   - Interactive model selection with filtering by GPU/CPU
#   - Creates semantic-config.json
#
# Usage:
#   .\setup-semantic-embedding.ps1                    # Interactive mode
#   .\setup-semantic-embedding.ps1 -SkipGpuDetection  # Force CPU mode
# ==============================================================================

param(
    [switch]$SkipGpuDetection = $false
)

# Configuration
$ConfigFile = Join-Path (Split-Path -Parent $PSScriptRoot) "embedding-models.json"

# Colors
function Write-Success { param($msg) Write-Host $msg -ForegroundColor Green }
function Write-Info { param($msg) Write-Host $msg -ForegroundColor Cyan }
function Write-Warn { param($msg) Write-Host $msg -ForegroundColor Yellow }
function Write-Err { param($msg) Write-Host $msg -ForegroundColor Red }

# Banner
Write-Host ""
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "UltrasharpTools MCP - Semantic Embedding Setup" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host ""

# Check if config file exists
if (!(Test-Path $ConfigFile)) {
    Write-Err "Configuration file not found: $ConfigFile"
    exit 1
}

# Load configuration
Write-Host "[*] Loading configuration..." -ForegroundColor Gray
try {
    $cfg = Get-Content $ConfigFile -Raw | ConvertFrom-Json
} catch {
    Write-Err "Failed to parse configuration: $_"
    exit 1
}

# ==============================================================================
# Step 1: Detect GPU architecture
# ==============================================================================

Write-Host "[1/3] GPU Architecture Detection" -ForegroundColor Yellow
Write-Host ""

$architecture = "cpu"
$hasGpu = $false

if (!$SkipGpuDetection) {
    # Look for detect script in same directory
    $detectScript = Join-Path $PSScriptRoot "detect-gpu-architecture.ps1"

    if (Test-Path $detectScript) {
        Write-Host "Running GPU detection..." -ForegroundColor Gray
        $architecture = & $detectScript
        if ($LASTEXITCODE -ne 0) {
            Write-Warn "✗ GPU detection failed, defaulting to CPU"
            $architecture = "cpu"
        } else {
            if ($architecture -eq "cpu") {
                Write-Info "✓ No compatible GPU detected"
            } else {
                Write-Success "✓ Detected GPU architecture: $architecture"
                $hasGpu = $true
            }
        }
    } else {
        Write-Warn "⚠ detect-gpu-architecture.ps1 not found, defaulting to CPU"
    }
} else {
    Write-Host "Skipping GPU detection (--SkipGpuDetection)" -ForegroundColor Gray
    $architecture = "cpu"
}

Write-Host "Selected architecture: $architecture" -ForegroundColor White
Write-Host ""

# ==============================================================================
# Step 2: Select Provider
# ==============================================================================

Write-Host "[2/3] Provider Selection" -ForegroundColor Yellow
Write-Host ""

# Display providers
$providers = $cfg.providers.PSObject.Properties | ForEach-Object {
    [PSCustomObject]@{
        Id = $_.Name
        Name = $_.Value.name
        Description = $_.Value.description
    }
}

$index = 1
foreach ($provider in $providers) {
    Write-Host "$index) $($provider.Name)" -ForegroundColor White
    Write-Host "   $($provider.Description -replace '\\n', "`n   ")" -ForegroundColor Gray
    Write-Host ""
    $index++
}

do {
    $providerChoice = Read-Host "Choose provider [1-$($providers.Count)]"
    $providerIdx = [int]$providerChoice - 1
} while ($providerIdx -lt 0 -or $providerIdx -ge $providers.Count)

$selectedProvider = $providers[$providerIdx].Id

Write-Host ""
Write-Info "Selected: $($providers[$providerIdx].Name)"
Write-Host ""

# Memory provider doesn't need model selection
if ($selectedProvider -eq "memory") {
    Write-Host ""
    Write-Host "=================================================================" -ForegroundColor Yellow
    Write-Host "Memory Provider (No ML)" -ForegroundColor Yellow
    Write-Host "=================================================================" -ForegroundColor Yellow
    Write-Host ""
    Write-Warn "[WARNING] Memory provider uses deterministic hashing (no ML embeddings)"
    Write-Host ""
    Write-Host "Configuration will be created with Memory provider."
    Write-Host "No installation required."
    Write-Host ""

    # Create minimal config
    $config = @{
        embedding = @{
            platform = "memory"
            architecture = $architecture
            tei = @{
                endpoint = "http://127.0.0.1:8080"
                models = @()
                selected_model = $null
            }
            ollama = @{
                endpoint = "http://127.0.0.1:11434"
                models = @()
                selected_model = $null
            }
            memory = @{
                model_path = "./models/embedding"
                vector_size = 384
            }
        }
        auto_detection = @{
            gpu_architecture = $true
            codebase_size = $true
            language = $true
        }
    }

    # Save config
    $configDir = Split-Path -Parent $PSScriptRoot
    if (!(Test-Path $configDir)) {
        New-Item -ItemType Directory -Path $configDir -Force | Out-Null
    }
    $configPath = Join-Path $configDir "semantic-config.json"
    $config | ConvertTo-Json -Depth 10 | Set-Content $configPath -Encoding UTF8

    Write-Success "Configuration saved to: $configPath"
    Write-Host ""
    exit 0
}

# ==============================================================================
# Step 3: Select Model
# ==============================================================================

Write-Host "[3/3] Model Selection" -ForegroundColor Yellow
Write-Host ""

# Filter models by provider
$providerModels = $cfg.models | Where-Object { $_.provider -eq $selectedProvider }

if ($providerModels.Count -eq 0) {
    Write-Err "No models available for provider: $selectedProvider"
    exit 1
}

# Filter and sort by GPU architecture compatibility
# Split into GPU-compatible and CPU-only models
$gpuModels = @()
$cpuModels = @()

foreach ($model in $providerModels) {
    if ($model.gpu_architectures -contains $architecture -and $architecture -ne "cpu") {
        $gpuModels += $model
    } elseif ($model.gpu_architectures -contains "cpu") {
        $cpuModels += $model
    }
}

# Combine: GPU-compatible first, then CPU-only
$availableModels = $gpuModels + $cpuModels

if ($availableModels.Count -eq 0) {
    Write-Err "No compatible models found for architecture: $architecture"
    exit 1
}

# Display header
if ($gpuModels.Count -gt 0) {
    Write-Host "Available models for your GPU ($architecture):" -ForegroundColor White
} else {
    Write-Host "Available models (CPU mode):" -ForegroundColor White
}
Write-Host ""

# Display models
$index = 1
$displayedGpuSection = $false

foreach ($model in $availableModels) {
    # Show separator between GPU and CPU sections (only if we have GPU models)
    if (!$displayedGpuSection -and $index -gt $gpuModels.Count -and $cpuModels.Count -gt 0 -and $gpuModels.Count -gt 0) {
        Write-Host "--- CPU-only models (slower) ---" -ForegroundColor DarkGray
        Write-Host ""
        $displayedGpuSection = $true
    }

    $badge = if ($model.badge) { " $($model.badge)" } else { "" }

    # Add GPU indicator for GPU-compatible models
    if ($index -le $gpuModels.Count -and $gpuModels.Count -gt 0) {
        Write-Host "$index) $($model.name)$badge 🚀 GPU" -ForegroundColor White
    } else {
        Write-Host "$index) $($model.name)$badge" -ForegroundColor White
    }

    $langLabel = if ($model.language -eq "multi") { "Multilingual" } else { "English" }
    Write-Host "   • Language: $langLabel | Context: $($model.context_tokens) tokens | Dim: $($model.dimensions) | Size: ~$($model.size_mb) MB" -ForegroundColor Gray
    Write-Host "   • $($model.description)" -ForegroundColor Gray
    Write-Host ""
    $index++
}

do {
    $modelChoice = Read-Host "Choose model [1-$($availableModels.Count)]"
    $modelIdx = [int]$modelChoice - 1
} while ($modelIdx -lt 0 -or $modelIdx -ge $availableModels.Count)

$selectedModel = $availableModels[$modelIdx]

Write-Host ""
Write-Info "Selected: $($selectedModel.name)"
Write-Host ""

# ==============================================================================
# Create Configuration
# ==============================================================================

Write-Host "Creating configuration..." -ForegroundColor Yellow
Write-Host ""

# Prepare endpoint URLs (use 127.0.0.1 instead of localhost to avoid IPv6 delays)
$teiEndpoint = "http://127.0.0.1:$($cfg.providers.tei.default_port)"
$ollamaEndpoint = "http://127.0.0.1:$($cfg.providers.ollama.default_port)"

# Build models arrays for config
if ($selectedProvider -eq "tei") {
    $teiModels = $availableModels | ForEach-Object {
        @{
            id = $_.model_id
            languages = @($_.language)
            vector_size = $_.dimensions
        }
    }
    $ollamaModels = @()
} else {
    $teiModels = @()
    $ollamaModels = $availableModels | ForEach-Object {
        @{
            id = $_.model_id
            languages = @($_.language)
            vector_size = $_.dimensions
        }
    }
}

$config = @{
    embedding = @{
        platform = $selectedProvider
        architecture = $architecture
        tei = @{
            endpoint = $teiEndpoint
            models = $teiModels
            selected_model = if ($selectedProvider -eq "tei") { $selectedModel.model_id } else { $null }
        }
        ollama = @{
            endpoint = $ollamaEndpoint
            models = $ollamaModels
            selected_model = if ($selectedProvider -eq "ollama") { $selectedModel.model_id } else { $null }
        }
        memory = @{
            model_path = "./models/embedding"
            vector_size = 384
        }
    }
    auto_detection = @{
        gpu_architecture = $true
        codebase_size = $true
        language = $true
    }
}

# Determine config path - parent directory (Config)
$configDir = Split-Path -Parent $PSScriptRoot
if (!(Test-Path $configDir)) {
    New-Item -ItemType Directory -Path $configDir -Force | Out-Null
}

$configPath = Join-Path $configDir "semantic-config.json"

# Save configuration
$config | ConvertTo-Json -Depth 10 | Set-Content $configPath -Encoding UTF8

Write-Success "✓ Configuration saved to: $configPath"
Write-Host ""

# ==============================================================================
# Installation Instructions
# ==============================================================================

Write-Host "=================================================================" -ForegroundColor Green
Write-Host "Configuration Complete!" -ForegroundColor Green
Write-Host "=================================================================" -ForegroundColor Green
Write-Host ""

Write-Host "Summary:" -ForegroundColor White
Write-Host "  Platform: $selectedProvider" -ForegroundColor Cyan
Write-Host "  Model: $($selectedModel.name)" -ForegroundColor Cyan
Write-Host "  Architecture: $architecture" -ForegroundColor Cyan
Write-Host ""

if ($selectedProvider -eq "tei") {
    Write-Host "Installing TEI server..." -ForegroundColor Yellow
    Write-Host ""

    $setupScript = Join-Path $PSScriptRoot "setup-tei.ps1"
    if (Test-Path $setupScript) {
        & $setupScript
        if ($LASTEXITCODE -ne 0) {
            Write-Host ""
            Write-Host "⚠ TEI setup encountered issues" -ForegroundColor Yellow
            Write-Host "  You can run it manually: .\Config\Scripts\setup-tei.ps1" -ForegroundColor Gray
        }
    } else {
        Write-Host "⚠ setup-tei.ps1 not found at: $setupScript" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Please install TEI manually:" -ForegroundColor White
        Write-Host "  Docker required" -ForegroundColor Gray
        Write-Host "  Model: $($selectedModel.model_id)" -ForegroundColor Gray
    }
    Write-Host ""
    Write-Host "Next: Restart Claude Desktop to enable semantic mode" -ForegroundColor Yellow
    Write-Host ""
} elseif ($selectedProvider -eq "ollama") {
    Write-Host "Installing Ollama and downloading model..." -ForegroundColor Yellow
    Write-Host ""

    $setupScript = Join-Path $PSScriptRoot "setup-ollama.ps1"
    if (Test-Path $setupScript) {
        & $setupScript -ModelId $selectedModel.model_id
        if ($LASTEXITCODE -ne 0) {
            Write-Host ""
            Write-Host "⚠ Ollama setup encountered issues" -ForegroundColor Yellow
            Write-Host "  You can run it manually: .\Config\Scripts\setup-ollama.ps1 -ModelId $($selectedModel.model_id)" -ForegroundColor Gray
        }
    } else {
        Write-Host "⚠ setup-ollama.ps1 not found at: $setupScript" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Please install Ollama manually:" -ForegroundColor White
        Write-Host "  Download from: https://ollama.ai" -ForegroundColor Gray
        Write-Host "  Then run: ollama pull $($selectedModel.model_id)" -ForegroundColor Gray
    }
    Write-Host ""
    Write-Host "Next: Restart Claude Desktop to enable semantic mode" -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "Configuration file location:" -ForegroundColor White
Write-Host "  $configPath" -ForegroundColor Gray
Write-Host ""
