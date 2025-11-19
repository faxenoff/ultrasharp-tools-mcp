#!/usr/bin/env pwsh
# Ollama Setup Script
# Installs Ollama and downloads embedding model

param(
    [string]$ModelId = $null
)

$ErrorActionPreference = "Stop"

Write-Host "========================================"
Write-Host "Ollama Setup"
Write-Host "========================================"
Write-Host ""

# Check if Ollama is installed
Write-Host "[1/3] Checking Ollama installation..." -ForegroundColor Cyan
Write-Host ""

$ollamaInstalled = $false
try {
    $version = ollama --version 2>$null
    if ($LASTEXITCODE -eq 0) {
        $ollamaInstalled = $true
        Write-Host "✓ Ollama is already installed: $version" -ForegroundColor Green
    }
}
catch {
    # Ollama not found
}

if (!$ollamaInstalled) {
    Write-Host "✗ Ollama not found" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Install Ollama now? [Y/n]" -ForegroundColor Cyan
    $install = Read-Host

    if ($install -eq "n" -or $install -eq "N") {
        Write-Host ""
        Write-Host "[INFO] Setup cancelled" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "To install Ollama manually:" -ForegroundColor Cyan
        Write-Host "  1. Download from: https://ollama.ai/download" -ForegroundColor White
        Write-Host "  2. Run the installer" -ForegroundColor White
        Write-Host "  3. Run this script again" -ForegroundColor White
        Write-Host ""
        exit 0
    }

    Write-Host ""
    Write-Host "[INFO] Downloading Ollama installer..." -ForegroundColor Cyan

    $installerPath = "$env:TEMP\OllamaSetup.exe"
    $downloadUrl = "https://ollama.ai/download/OllamaSetup.exe"

    try {
        Invoke-WebRequest -Uri $downloadUrl -OutFile $installerPath -UseBasicParsing
        Write-Host "✓ Downloaded installer" -ForegroundColor Green
        Write-Host ""

        Write-Host "[INFO] Running Ollama installer..." -ForegroundColor Cyan
        Write-Host "       Please follow the installation wizard" -ForegroundColor DarkGray
        Write-Host ""

        Start-Process -FilePath $installerPath -Wait

        Write-Host ""
        Write-Host "✓ Installation complete" -ForegroundColor Green
        Write-Host ""
        Write-Host "⚠ Please restart this script to continue" -ForegroundColor Yellow
        Write-Host ""

        Remove-Item -Path $installerPath -Force -ErrorAction SilentlyContinue
        exit 0
    }
    catch {
        Write-Host ""
        Write-Host "[ERROR] Failed to download or install Ollama" -ForegroundColor Red
        Write-Host "        Error: $_" -ForegroundColor Red
        Write-Host ""
        Write-Host "Please install manually from: https://ollama.ai/download" -ForegroundColor Yellow
        exit 1
    }
}

Write-Host ""

# Check if Ollama service is running
Write-Host "[2/3] Checking Ollama service..." -ForegroundColor Cyan
Write-Host ""

try {
    $response = Invoke-WebRequest -Uri "http://127.0.0.1:11434/" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
    Write-Host "✓ Ollama service is running" -ForegroundColor Green
}
catch {
    Write-Host "✗ Ollama service not responding" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "[INFO] Starting Ollama service..." -ForegroundColor Cyan

    # Start Ollama in background
    Start-Process -FilePath "ollama" -ArgumentList "serve" -WindowStyle Hidden

    Write-Host "       Waiting for service to start..." -ForegroundColor DarkGray
    Start-Sleep -Seconds 3

    try {
        $response = Invoke-WebRequest -Uri "http://127.0.0.1:11434/" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        Write-Host "✓ Ollama service started" -ForegroundColor Green
    }
    catch {
        Write-Host "[ERROR] Failed to start Ollama service" -ForegroundColor Red
        Write-Host "        Please start manually: ollama serve" -ForegroundColor Yellow
        exit 1
    }
}

Write-Host ""

# Pull model if specified
if ([string]::IsNullOrWhiteSpace($ModelId)) {
    Write-Host "[3/3] Skipping model download (no model specified)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "To download a model manually:" -ForegroundColor Cyan
    Write-Host "  ollama pull <model-name>" -ForegroundColor White
    Write-Host ""
}
else {
    Write-Host "[3/3] Downloading model: $ModelId" -ForegroundColor Cyan
    Write-Host ""

    # Check if model already exists
    $existingModels = ollama list 2>$null | Select-String -Pattern "^$ModelId\s"
    if ($existingModels) {
        Write-Host "✓ Model '$ModelId' is already downloaded" -ForegroundColor Green
        Write-Host ""
    }
    else {
        Write-Host "[INFO] Pulling model (this may take several minutes)..." -ForegroundColor Cyan
        Write-Host ""

        ollama pull $ModelId

        if ($LASTEXITCODE -eq 0) {
            Write-Host ""
            Write-Host "✓ Model downloaded successfully" -ForegroundColor Green
            Write-Host ""
        }
        else {
            Write-Host ""
            Write-Host "[ERROR] Failed to download model: $ModelId" -ForegroundColor Red
            Write-Host "        You can download it manually: ollama pull $ModelId" -ForegroundColor Yellow
            Write-Host ""
            exit 1
        }
    }
}

Write-Host "========================================"
Write-Host "✅ Ollama Setup Complete"
Write-Host "========================================"
Write-Host ""
Write-Host "Service:  http://127.0.0.1:11434" -ForegroundColor Cyan
if (![string]::IsNullOrWhiteSpace($ModelId)) {
    Write-Host "Model:    $ModelId" -ForegroundColor Cyan
}
Write-Host ""
Write-Host "Management commands:"
Write-Host "  ollama list           # List downloaded models"
Write-Host "  ollama pull <model>   # Download model"
Write-Host "  ollama rm <model>     # Remove model"
Write-Host "  ollama serve          # Start service manually"
Write-Host ""
