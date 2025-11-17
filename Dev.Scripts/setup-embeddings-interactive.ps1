#!/usr/bin/env pwsh
# Interactive Embeddings Setup for UltrasharpTools MCP
# Automatically detects GPU and recommends optimal embedding provider

$ErrorActionPreference = "Stop"

Write-Host "========================================"
Write-Host "UltrasharpTools MCP - Embeddings Setup"
Write-Host "========================================"
Write-Host ""

# Function to check if command exists
function Test-Command {
    param($Command)
    try {
        if (Get-Command $Command -ErrorAction Stop) {
            return $true
        }
    }
    catch {
        return $false
    }
    return $false
}

# Detect GPU
Write-Host "🔍 Detecting GPU capabilities..." -ForegroundColor Cyan
Write-Host ""

$gpuDetected = $false
$gpuName = "Unknown"
$computeCapability = 0.0
$recommendedProvider = "ollama"

try {
    $nvidiaOutput = nvidia-smi --query-gpu=name,compute_cap --format=csv,noheader,nounits 2>$null
    if ($LASTEXITCODE -eq 0 -and $nvidiaOutput) {
        $gpuDetected = $true
        $parts = $nvidiaOutput -split ','
        $gpuName = $parts[0].Trim()
        $computeCapability = [float]$parts[1].Trim()

        Write-Host "🎮 GPU Detected: " -NoNewline
        Write-Host $gpuName -ForegroundColor Green
        Write-Host "   Compute Capability: " -NoNewline
        Write-Host $computeCapability -ForegroundColor Green
        Write-Host ""

        if ($computeCapability -ge 8.0) {
            $recommendedProvider = "tei"
            Write-Host "✅ RTX 30xx+ detected!" -ForegroundColor Green
            Write-Host "   TEI recommended (8192 tokens context)" -ForegroundColor Green
        }
        elseif ($computeCapability -ge 7.0) {
            $recommendedProvider = "ollama"
            Write-Host "⚠️  GTX/RTX 20xx detected" -ForegroundColor Yellow
            Write-Host "   Ollama recommended (512 tokens context)" -ForegroundColor Yellow
            Write-Host "   (TEI requires RTX 30xx+ with Compute Capability 8.0+)" -ForegroundColor DarkGray
        }
        else {
            $recommendedProvider = "ollama"
            Write-Host "⚠️  Older GPU detected (CC $computeCapability)" -ForegroundColor Yellow
            Write-Host "   Ollama recommended" -ForegroundColor Yellow
        }
    }
}
catch {
    Write-Host "⚠️  No NVIDIA GPU detected" -ForegroundColor Yellow
    Write-Host "   nvidia-smi not available" -ForegroundColor DarkGray
}

if (-not $gpuDetected) {
    Write-Host "💡 Recommendation: Use Ollama (simple installation, no Docker required)" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "Available embedding providers:" -ForegroundColor Cyan
Write-Host ""
Write-Host "1) TEI (Text Embeddings Inference)" -ForegroundColor White
if ($computeCapability -ge 8.0) {
    Write-Host "   ✅ RECOMMENDED for your GPU" -ForegroundColor Green
}
else {
    Write-Host "   ⚠️  Requires RTX 30xx/40xx (Compute Capability 8.0+)" -ForegroundColor Yellow
    Write-Host "   ❌ NOT COMPATIBLE with your GPU (CC $computeCapability)" -ForegroundColor Red
}
Write-Host "   ✅ 8192 tokens context (16x more than Ollama)"
Write-Host "   🐳 Requires Docker Desktop"
Write-Host "   📦 ~2 GB (image + model)"
Write-Host ""

Write-Host "2) Ollama" -ForegroundColor White
if ($recommendedProvider -eq "ollama") {
    Write-Host "   ✅ RECOMMENDED for your system" -ForegroundColor Green
}
Write-Host "   ✅ Simple installation (no Docker)"
Write-Host "   ⚠️  512 tokens context"
Write-Host "   📦 ~200 MB"
Write-Host ""

Write-Host "3) Skip (use Memory provider)" -ForegroundColor White
Write-Host "   ⚠️  No ML embeddings (deterministic hash)"
Write-Host "   ✅ No installation required"
Write-Host ""

# Prompt user
$choice = Read-Host "Your choice [1-3]"

switch ($choice) {
    "1" {
        Write-Host ""
        Write-Host "[INFO] Selected: TEI (Text Embeddings Inference)" -ForegroundColor Cyan
        Write-Host ""

        # Check GPU compatibility
        if ($computeCapability -lt 8.0 -and $gpuDetected) {
            Write-Host "[WARNING] Your GPU (CC $computeCapability) may not be compatible with TEI" -ForegroundColor Yellow
            Write-Host "          TEI requires RTX 30xx+ (Compute Capability 8.0+)" -ForegroundColor Yellow
            Write-Host ""
            $continue = Read-Host "Continue anyway? [y/N]"
            if ($continue -ne "y" -and $continue -ne "Y") {
                Write-Host "[INFO] Installation cancelled" -ForegroundColor Yellow
                exit 0
            }
        }

        # Check Docker
        if (-not (Test-Command "docker")) {
            Write-Host "[ERROR] Docker not found!" -ForegroundColor Red
            Write-Host ""
            Write-Host "TEI requires Docker. Please install Docker Desktop:" -ForegroundColor Yellow
            Write-Host "  Windows: https://docs.docker.com/desktop/install/windows-install/" -ForegroundColor Cyan
            Write-Host ""
            Write-Host "Or choose Ollama (option 2) when running this script again." -ForegroundColor Yellow
            exit 1
        }

        Write-Host "[OK] Docker found" -ForegroundColor Green
        docker --version
        Write-Host ""

        # Check NVIDIA Container Toolkit
        Write-Host "[INFO] Checking Docker GPU support..." -ForegroundColor Cyan
        try {
            $gpuTest = docker run --rm --gpus all nvidia/cuda:12.0-base-ubuntu20.04 nvidia-smi 2>&1
            if ($LASTEXITCODE -eq 0) {
                Write-Host "[OK] Docker has GPU access (nvidia-container-toolkit configured)" -ForegroundColor Green
            }
            else {
                Write-Host "[ERROR] Docker cannot access GPU!" -ForegroundColor Red
                Write-Host ""
                Write-Host "nvidia-container-toolkit is not installed." -ForegroundColor Yellow
                Write-Host ""
                Write-Host "Options:" -ForegroundColor Cyan
                Write-Host "  1. Auto-install nvidia-container-toolkit (recommended)" -ForegroundColor White
                Write-Host "  2. Manual installation instructions" -ForegroundColor White
                Write-Host "  3. Continue without GPU (CPU only, slower)" -ForegroundColor White
                Write-Host "  4. Cancel and use Ollama instead (no Docker needed)" -ForegroundColor White
                Write-Host ""

                $gpuChoice = Read-Host "Your choice [1-4]"

                switch ($gpuChoice) {
                    "1" {
                        Write-Host ""
                        Write-Host "[INFO] Running automatic installation..." -ForegroundColor Cyan
                        Write-Host ""

                        if (Test-Path "./setup-nvidia-container-toolkit.ps1") {
                            & ./setup-nvidia-container-toolkit.ps1

                            if ($LASTEXITCODE -ne 0) {
                                Write-Host ""
                                Write-Host "[ERROR] Automatic installation failed" -ForegroundColor Red
                                Write-Host "Please try manual installation or use Ollama" -ForegroundColor Yellow
                                exit 1
                            }

                            Write-Host ""
                            Write-Host "[OK] nvidia-container-toolkit installed!" -ForegroundColor Green
                            Write-Host "    Continuing with TEI installation..." -ForegroundColor Cyan
                        }
                        else {
                            Write-Host "[ERROR] setup-nvidia-container-toolkit.ps1 not found!" -ForegroundColor Red
                            exit 1
                        }
                    }

                    "2" {
                        Write-Host ""
                        Write-Host "Manual installation (in WSL2 Ubuntu):" -ForegroundColor Cyan
                        Write-Host ""
                        Write-Host "  wsl -d Ubuntu" -ForegroundColor White
                        Write-Host "  curl -fsSL https://nvidia.github.io/libnvidia-container/gpgkey | sudo gpg --dearmor -o /usr/share/keyrings/nvidia-container-toolkit-keyring.gpg" -ForegroundColor White
                        Write-Host "  curl -s -L https://nvidia.github.io/libnvidia-container/stable/deb/nvidia-container-toolkit.list | sed 's#deb https://#deb [signed-by=/usr/share/keyrings/nvidia-container-toolkit-keyring.gpg] https://#g' | sudo tee /etc/apt/sources.list.d/nvidia-container-toolkit.list" -ForegroundColor White
                        Write-Host "  sudo apt-get update && sudo apt-get install -y nvidia-container-toolkit" -ForegroundColor White
                        Write-Host "  sudo nvidia-ctk runtime configure --runtime=docker" -ForegroundColor White
                        Write-Host ""
                        Write-Host "Then restart this script." -ForegroundColor Yellow
                        exit 0
                    }

                    "3" {
                        Write-Host ""
                        Write-Host "[WARNING] Continuing without GPU (CPU mode)" -ForegroundColor Yellow
                        Write-Host "          TEI performance will be significantly slower" -ForegroundColor DarkGray
                    }

                    "4" {
                        Write-Host ""
                        Write-Host "[INFO] Installation cancelled" -ForegroundColor Yellow
                        Write-Host "      Please run this script again and choose Ollama (option 2)" -ForegroundColor Cyan
                        exit 0
                    }

                    default {
                        Write-Host "[ERROR] Invalid choice" -ForegroundColor Red
                        exit 1
                    }
                }
            }
        }
        catch {
            Write-Host "[WARNING] Could not test GPU access (Docker might not be running)" -ForegroundColor Yellow
            Write-Host "          TEI will attempt to use GPU, but may fail" -ForegroundColor DarkGray
        }
        Write-Host ""

        # Run TEI setup
        if (Test-Path "./setup-tei.ps1") {
            Write-Host "[INFO] Running TEI setup..." -ForegroundColor Cyan
            & ./setup-tei.ps1
        }
        else {
            Write-Host "[ERROR] setup-tei.ps1 not found!" -ForegroundColor Red
            Write-Host "Make sure you're in the SharpTools project root directory." -ForegroundColor Yellow
            exit 1
        }

        Write-Host ""
        Write-Host "================================================" -ForegroundColor Green
        Write-Host "✅ TEI installed and configured!" -ForegroundColor Green
        Write-Host ""
        Write-Host "Container 'tei-server' running on http://127.0.0.1:8080" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Configuration (already set in appsettings.json):"
        Write-Host "  Provider: tei"
        Write-Host "  Model: ibm-granite/granite-embedding-english-r2"
        Write-Host "  Context: 8192 tokens"
        Write-Host ""
        Write-Host "Management commands:"
        Write-Host "  docker logs tei-server    # View logs"
        Write-Host "  docker stop tei-server    # Stop container"
        Write-Host "  docker start tei-server   # Start container"
        Write-Host "================================================" -ForegroundColor Green
    }

    "2" {
        Write-Host ""
        Write-Host "[INFO] Selected: Ollama" -ForegroundColor Cyan
        Write-Host ""

        # Check Ollama installation
        if (-not (Test-Command "ollama")) {
            Write-Host "[INFO] Ollama not found, installing..." -ForegroundColor Cyan

            # Try winget install
            if (Test-Command "winget") {
                Write-Host "[INFO] Installing Ollama via winget..." -ForegroundColor Cyan
                winget install Ollama.Ollama
            }
            else {
                Write-Host "[WARNING] winget not available" -ForegroundColor Yellow
                Write-Host ""
                Write-Host "Please install Ollama manually:" -ForegroundColor Yellow
                Write-Host "  https://ollama.com/download/windows" -ForegroundColor Cyan
                Write-Host ""
                exit 1
            }
        }
        else {
            Write-Host "[OK] Ollama already installed" -ForegroundColor Green
        }

        # Check if Ollama is running
        try {
            $response = Invoke-WebRequest -Uri "http://127.0.0.1:11434/api/version" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
            Write-Host "[OK] Ollama service is running" -ForegroundColor Green
        }
        catch {
            Write-Host "[INFO] Starting Ollama service..." -ForegroundColor Cyan
            Start-Process "ollama" -ArgumentList "serve" -WindowStyle Hidden
            Start-Sleep -Seconds 3
        }

        # Pull granite-embedding model
        Write-Host "[INFO] Downloading granite-embedding model (~150 MB)..." -ForegroundColor Cyan
        Write-Host "       This may take a few minutes..." -ForegroundColor DarkGray
        ollama pull granite-embedding

        if ($LASTEXITCODE -eq 0) {
            Write-Host "[OK] Model downloaded successfully" -ForegroundColor Green
        }
        else {
            Write-Host "[ERROR] Failed to download model" -ForegroundColor Red
            exit 1
        }

        # Verify
        Write-Host ""
        Write-Host "[INFO] Verifying installation..." -ForegroundColor Cyan
        ollama list

        Write-Host ""
        Write-Host "================================================" -ForegroundColor Green
        Write-Host "✅ Ollama installed and configured!" -ForegroundColor Green
        Write-Host ""
        Write-Host "To use Ollama, update appsettings.json:"
        Write-Host ""
        Write-Host '  "Embedding": {' -ForegroundColor Yellow
        Write-Host '    "Provider": "ollama",' -ForegroundColor Yellow
        Write-Host '    "Enabled": true' -ForegroundColor Yellow
        Write-Host '  }' -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Management commands:"
        Write-Host "  ollama serve              # Start Ollama server"
        Write-Host "  ollama list               # List installed models"
        Write-Host "  ollama pull <model>       # Download model"
        Write-Host "================================================" -ForegroundColor Green
    }

    "3" {
        Write-Host ""
        Write-Host "[INFO] Installation skipped" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "⚠️  Memory provider will be used (no ML embeddings)" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "To configure in appsettings.json:"
        Write-Host ""
        Write-Host '  "Embedding": {' -ForegroundColor Yellow
        Write-Host '    "Provider": "memory",' -ForegroundColor Yellow
        Write-Host '    "Enabled": true' -ForegroundColor Yellow
        Write-Host '  }' -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Embeddings can be installed later by running:"
        Write-Host "  ./setup-embeddings-interactive.ps1" -ForegroundColor Cyan
    }

    default {
        Write-Host ""
        Write-Host "[ERROR] Invalid choice: $choice" -ForegroundColor Red
        Write-Host "Please choose 1, 2, or 3" -ForegroundColor Yellow
        exit 1
    }
}

Write-Host ""
Write-Host "Done! Start UltrasharpTools MCP server:" -ForegroundColor Green
Write-Host "  dotnet run --project UltrasharpTools.MCPServer" -ForegroundColor Cyan
Write-Host ""
