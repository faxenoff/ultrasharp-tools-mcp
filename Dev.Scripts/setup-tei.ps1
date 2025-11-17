#!/usr/bin/env pwsh
# TEI (Text Embeddings Inference) Setup Script
# Installs and configures HuggingFace TEI with GPU support

$ErrorActionPreference = "Stop"

Write-Host "========================================"
Write-Host "TEI (Text Embeddings Inference) Setup"
Write-Host "========================================"
Write-Host ""

# Configuration
$containerName = "tei-server"
$model = "ibm-granite/granite-embedding-125m-english"
$port = 8080
$baseImage = "ghcr.io/huggingface/text-embeddings-inference"
$version = "1.8.3"

# Check if container already exists
$existingContainer = docker ps -a --filter "name=$containerName" --format "{{.Names}}" 2>$null
if ($existingContainer -eq $containerName) {
    Write-Host "[INFO] Container '$containerName' already exists" -ForegroundColor Yellow
    Write-Host ""

    $action = Read-Host "What to do? [1=Restart, 2=Remove and reinstall, 3=Cancel]"

    switch ($action) {
        "1" {
            Write-Host "[INFO] Restarting existing container..." -ForegroundColor Cyan
            docker restart $containerName

            if ($LASTEXITCODE -eq 0) {
                Write-Host "[OK] Container restarted successfully" -ForegroundColor Green
                Write-Host ""
                Write-Host "TEI is now running on http://127.0.0.1:$port" -ForegroundColor Cyan
                exit 0
            }
            else {
                Write-Host "[ERROR] Failed to restart container" -ForegroundColor Red
                exit 1
            }
        }

        "2" {
            Write-Host "[INFO] Removing existing container..." -ForegroundColor Cyan
            docker stop $containerName 2>$null
            docker rm $containerName 2>$null
            Write-Host "[OK] Container removed" -ForegroundColor Green
        }

        "3" {
            Write-Host "[INFO] Installation cancelled" -ForegroundColor Yellow
            exit 0
        }

        default {
            Write-Host "[ERROR] Invalid choice" -ForegroundColor Red
            exit 1
        }
    }
}

# Architecture selection
Write-Host "========================================"
Write-Host "GPU Architecture Selection"
Write-Host "========================================"
Write-Host ""
Write-Host "Select your GPU architecture:"
Write-Host "  1) CPU only (slowest, but works everywhere)"
Write-Host "  2) NVIDIA Turing (RTX 2000 series, T4)"
Write-Host "  3) NVIDIA Ampere A100/A30 (default, best compatibility)"
Write-Host "  4) NVIDIA Ampere A10/A40"
Write-Host "  5) NVIDIA Ada Lovelace (RTX 4000 series)"
Write-Host "  6) NVIDIA Hopper (H100)"
Write-Host "  7) NVIDIA Blackwell (RTX 5000 series) - experimental"
Write-Host ""

$architecture = Read-Host "Enter choice [1-7] (default: 3)"
if ([string]::IsNullOrWhiteSpace($architecture)) { $architecture = "3" }

switch ($architecture) {
    "1" {
        $imageTag = "${baseImage}:cpu-${version}"
        $archName = "CPU"
        $useGpu = $false
    }
    "2" {
        $imageTag = "${baseImage}:turing-${version}"
        $archName = "Turing (RTX 2000/T4)"
        $useGpu = $true
    }
    "3" {
        $imageTag = "${baseImage}:${version}"
        $archName = "Ampere A100/A30"
        $useGpu = $true
    }
    "4" {
        $imageTag = "${baseImage}:86-${version}"
        $archName = "Ampere A10/A40"
        $useGpu = $true
    }
    "5" {
        $imageTag = "${baseImage}:89-${version}"
        $archName = "Ada Lovelace (RTX 4000)"
        $useGpu = $true
    }
    "6" {
        $imageTag = "${baseImage}:hopper-${version}"
        $archName = "Hopper (H100)"
        $useGpu = $true
    }
    "7" {
        $imageTag = "${baseImage}:${version}"
        $archName = "Blackwell (RTX 5000) - trying default image"
        $useGpu = $true
        Write-Host ""
        Write-Host "[WARNING] Blackwell not officially supported yet (PR #735 pending)" -ForegroundColor Yellow
        Write-Host "          Using default image - if it fails, try option 1 (CPU) or 5 (Ada)" -ForegroundColor Yellow
    }
    default {
        Write-Host "[ERROR] Invalid choice" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "[INFO] Selected: $archName" -ForegroundColor Cyan
Write-Host ""

# Pull TEI image
Write-Host "[INFO] Pulling TEI Docker image..." -ForegroundColor Cyan
Write-Host "       Image: $imageTag" -ForegroundColor DarkGray
Write-Host "       This may take a few minutes (first time only)..." -ForegroundColor DarkGray
Write-Host ""

docker pull $imageTag

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Failed to pull TEI image" -ForegroundColor Red
    exit 1
}

Write-Host "[OK] Image downloaded" -ForegroundColor Green
Write-Host ""

# Create and run container
Write-Host "[INFO] Creating TEI container..." -ForegroundColor Cyan
Write-Host "       Container name: $containerName" -ForegroundColor DarkGray
Write-Host "       Model: $model" -ForegroundColor DarkGray
Write-Host "       Port: $port" -ForegroundColor DarkGray
Write-Host "       Architecture: $archName" -ForegroundColor DarkGray
Write-Host ""

# Prepare docker arguments
$dockerArgs = @(
    "run",
    "-d",
    "--name", $containerName,
    "-p", "${port}:80",
    "-v", "$HOME/.cache/huggingface:/data",
    "--restart", "unless-stopped"
)

# Add GPU support if selected
if ($useGpu) {
    Write-Host "[INFO] Starting with GPU support..." -ForegroundColor Cyan
    $dockerArgs += "--gpus"
    $dockerArgs += "all"
}
else {
    Write-Host "[INFO] Starting in CPU mode..." -ForegroundColor Cyan
}

$dockerArgs += $imageTag
$dockerArgs += "--model-id"
$dockerArgs += $model
$dockerArgs += "--max-concurrent-requests"
$dockerArgs += "512"

docker @dockerArgs

if ($LASTEXITCODE -eq 0) {
    if ($useGpu) {
        Write-Host "[OK] TEI started with GPU acceleration" -ForegroundColor Green
    }
    else {
        Write-Host "[OK] TEI started in CPU mode" -ForegroundColor Green
    }
}
else {
    Write-Host "[ERROR] Failed to start TEI container" -ForegroundColor Red
    if ($useGpu) {
        Write-Host ""
        Write-Host "[HINT] If GPU failed, try running the script again and select option 1 (CPU)" -ForegroundColor Yellow
    }
    exit 1
}

Write-Host ""

# Wait for container to be ready
Write-Host "[INFO] Waiting for TEI to initialize..." -ForegroundColor Cyan
$maxWaitSeconds = 120
$waitedSeconds = 0

while ($waitedSeconds -lt $maxWaitSeconds) {
    Start-Sleep -Seconds 2
    $waitedSeconds += 2

    try {
        $response = Invoke-WebRequest -Uri "http://127.0.0.1:${port}/health" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            Write-Host "[OK] TEI is ready!" -ForegroundColor Green
            break
        }
    }
    catch {
        Write-Host "." -NoNewline
    }
}

if ($waitedSeconds -ge $maxWaitSeconds) {
    Write-Host ""
    Write-Host "[WARNING] TEI health check timed out after $maxWaitSeconds seconds" -ForegroundColor Yellow
    Write-Host "          Container might still be initializing. Check logs:" -ForegroundColor Yellow
    Write-Host "          docker logs $containerName" -ForegroundColor Cyan
}

Write-Host ""
Write-Host "========================================"
Write-Host "✅ TEI Setup Complete"
Write-Host "========================================"
Write-Host ""
Write-Host "Container: $containerName" -ForegroundColor Cyan
Write-Host "Endpoint:  http://127.0.0.1:$port" -ForegroundColor Cyan
Write-Host "Model:     $model" -ForegroundColor Cyan
Write-Host ""
Write-Host "Management commands:"
Write-Host "  docker logs $containerName        # View logs"
Write-Host "  docker stop $containerName        # Stop container"
Write-Host "  docker start $containerName       # Start container"
Write-Host "  docker restart $containerName     # Restart container"
Write-Host ""
