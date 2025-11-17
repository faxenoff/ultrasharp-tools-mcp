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
$imageTag = "ghcr.io/huggingface/text-embeddings-inference:1.2-cuda"

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
Write-Host ""

# Try with GPU first
Write-Host "[INFO] Attempting to start with GPU support..." -ForegroundColor Cyan
$gpuArgs = @(
    "run",
    "-d",
    "--name", $containerName,
    "--gpus", "all",
    "-p", "${port}:80",
    "-v", "$HOME/.cache/huggingface:/data",
    "--restart", "unless-stopped",
    $imageTag,
    "--model-id", $model,
    "--max-concurrent-requests", "512",
    "--max-input-length", "8192"
)

docker @gpuArgs 2>&1 | Out-Null

if ($LASTEXITCODE -eq 0) {
    Write-Host "[OK] TEI started with GPU acceleration" -ForegroundColor Green
}
else {
    Write-Host "[WARNING] GPU start failed, trying CPU mode..." -ForegroundColor Yellow

    # Remove failed container
    docker rm $containerName 2>$null

    # Try CPU mode
    $cpuArgs = @(
        "run",
        "-d",
        "--name", $containerName,
        "-p", "${port}:80",
        "-v", "$HOME/.cache/huggingface:/data",
        "--restart", "unless-stopped",
        $imageTag,
        "--model-id", $model,
        "--max-concurrent-requests", "512",
        "--max-input-length", "8192"
    )

    docker @cpuArgs

    if ($LASTEXITCODE -eq 0) {
        Write-Host "[OK] TEI started in CPU mode" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "[WARNING] Running on CPU - performance will be slower" -ForegroundColor Yellow
        Write-Host "          Consider installing nvidia-container-toolkit for GPU support" -ForegroundColor DarkGray
    }
    else {
        Write-Host "[ERROR] Failed to start TEI container" -ForegroundColor Red
        exit 1
    }
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
