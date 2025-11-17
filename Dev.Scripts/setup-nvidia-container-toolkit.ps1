#!/usr/bin/env pwsh
# Automatic NVIDIA Container Toolkit Setup for Docker Desktop (WSL2)
# This script installs nvidia-container-toolkit in WSL2 Ubuntu

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "NVIDIA Container Toolkit Setup (WSL2)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if WSL is installed
Write-Host "[INFO] Checking WSL installation..." -ForegroundColor Cyan
try {
    $wslList = wsl --list --verbose 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] WSL not found!" -ForegroundColor Red
        Write-Host ""
        Write-Host "Please install WSL2 first:" -ForegroundColor Yellow
        Write-Host "  wsl --install" -ForegroundColor White
        Write-Host ""
        exit 1
    }
}
catch {
    Write-Host "[ERROR] WSL not available!" -ForegroundColor Red
    Write-Host "Please install WSL2: wsl --install" -ForegroundColor Yellow
    exit 1
}

Write-Host "[OK] WSL is installed" -ForegroundColor Green
Write-Host ""

# Find Ubuntu distribution
Write-Host "[INFO] Looking for Ubuntu WSL distribution..." -ForegroundColor Cyan
$ubuntuDistro = $null

# Try common Ubuntu distro names
$possibleNames = @("Ubuntu", "Ubuntu-24.04", "Ubuntu-22.04", "Ubuntu-20.04")
foreach ($name in $possibleNames) {
    $test = wsl -d $name echo "OK" 2>$null
    if ($LASTEXITCODE -eq 0) {
        $ubuntuDistro = $name
        break
    }
}

if (-not $ubuntuDistro) {
    Write-Host "[ERROR] Ubuntu WSL distribution not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Available distributions:" -ForegroundColor Yellow
    wsl --list --verbose
    Write-Host ""
    Write-Host "Please install Ubuntu:" -ForegroundColor Yellow
    Write-Host "  wsl --install -d Ubuntu-24.04" -ForegroundColor White
    Write-Host ""
    exit 1
}

Write-Host "[OK] Found Ubuntu: $ubuntuDistro" -ForegroundColor Green
Write-Host ""

# Check NVIDIA driver
Write-Host "[INFO] Checking NVIDIA driver..." -ForegroundColor Cyan
try {
    $nvidiaCheck = nvidia-smi 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "[OK] NVIDIA driver detected" -ForegroundColor Green
    }
    else {
        Write-Host "[WARNING] nvidia-smi failed - GPU may not be available" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "[WARNING] NVIDIA driver check failed" -ForegroundColor Yellow
}
Write-Host ""

# Install nvidia-container-toolkit in WSL
Write-Host "[INFO] Installing nvidia-container-toolkit in WSL..." -ForegroundColor Cyan
Write-Host "       This may take a few minutes..." -ForegroundColor DarkGray
Write-Host ""

# Create installation script
$installScript = @'
#!/bin/bash
set -e

echo "========================================="
echo "Installing nvidia-container-toolkit..."
echo "========================================="
echo ""

# Add NVIDIA Container Toolkit repository
echo "[1/5] Adding NVIDIA repository..."
curl -fsSL https://nvidia.github.io/libnvidia-container/gpgkey | sudo gpg --dearmor -o /usr/share/keyrings/nvidia-container-toolkit-keyring.gpg

curl -s -L https://nvidia.github.io/libnvidia-container/stable/deb/nvidia-container-toolkit.list | \
    sed 's#deb https://#deb [signed-by=/usr/share/keyrings/nvidia-container-toolkit-keyring.gpg] https://#g' | \
    sudo tee /etc/apt/sources.list.d/nvidia-container-toolkit.list

# Update package list
echo ""
echo "[2/5] Updating package list..."
sudo apt-get update -qq

# Install nvidia-container-toolkit
echo ""
echo "[3/5] Installing nvidia-container-toolkit..."
sudo apt-get install -y nvidia-container-toolkit

# Configure Docker runtime
echo ""
echo "[4/5] Configuring Docker runtime..."
sudo nvidia-ctk runtime configure --runtime=docker

# Verify installation
echo ""
echo "[5/5] Verifying installation..."
if command -v nvidia-ctk &> /dev/null; then
    echo "✅ nvidia-container-toolkit installed successfully"
    nvidia-ctk --version
else
    echo "❌ Installation verification failed"
    exit 1
fi

echo ""
echo "========================================="
echo "✅ Installation complete!"
echo "========================================="
'@

# Save script to temp file
$tempScript = [System.IO.Path]::GetTempFileName() + ".sh"
$installScript | Out-File -FilePath $tempScript -Encoding UTF8 -NoNewline

# Copy script to WSL and execute
try {
    # Copy to WSL
    wsl -d $ubuntuDistro bash -c "cat > /tmp/install-nvidia-toolkit.sh" < $tempScript

    # Make executable and run
    wsl -d $ubuntuDistro bash -c "chmod +x /tmp/install-nvidia-toolkit.sh && /tmp/install-nvidia-toolkit.sh"

    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] Installation failed in WSL" -ForegroundColor Red
        exit 1
    }
}
finally {
    # Cleanup temp file
    Remove-Item -Path $tempScript -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "[INFO] Restarting Docker Desktop..." -ForegroundColor Cyan
Write-Host "       This is required for changes to take effect" -ForegroundColor DarkGray
Write-Host ""

# Try to restart Docker Desktop
try {
    # Stop Docker Desktop
    Stop-Process -Name "Docker Desktop" -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 3

    # Start Docker Desktop
    Start-Process "C:\Program Files\Docker\Docker\Docker Desktop.exe" -ErrorAction SilentlyContinue

    Write-Host "[INFO] Waiting for Docker to start..." -ForegroundColor Cyan
    Start-Sleep -Seconds 10

    # Wait for Docker to be ready
    $maxWait = 30
    $waited = 0
    while ($waited -lt $maxWait) {
        try {
            docker info | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Write-Host "[OK] Docker is ready" -ForegroundColor Green
                break
            }
        }
        catch { }

        Start-Sleep -Seconds 2
        $waited += 2
        Write-Host "." -NoNewline
    }

    if ($waited -ge $maxWait) {
        Write-Host ""
        Write-Host "[WARNING] Docker startup timeout" -ForegroundColor Yellow
        Write-Host "          Please restart Docker Desktop manually" -ForegroundColor Yellow
    }
}
catch {
    Write-Host "[WARNING] Could not restart Docker automatically" -ForegroundColor Yellow
    Write-Host "          Please restart Docker Desktop manually" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Testing GPU access in Docker..." -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Start-Sleep -Seconds 5

# Test GPU access
Write-Host "[INFO] Running GPU test..." -ForegroundColor Cyan
try {
    docker run --rm --gpus all nvidia/cuda:12.0-base-ubuntu20.04 nvidia-smi

    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Green
        Write-Host "✅ SUCCESS!" -ForegroundColor Green
        Write-Host "========================================" -ForegroundColor Green
        Write-Host ""
        Write-Host "Docker can now access your GPU!" -ForegroundColor Green
        Write-Host ""
        Write-Host "You can now run TEI with GPU acceleration:" -ForegroundColor Cyan
        Write-Host "  .\setup-embeddings-interactive.ps1" -ForegroundColor White
        Write-Host ""
    }
    else {
        Write-Host ""
        Write-Host "[ERROR] GPU test failed" -ForegroundColor Red
        Write-Host ""
        Write-Host "Possible solutions:" -ForegroundColor Yellow
        Write-Host "  1. Restart your computer" -ForegroundColor White
        Write-Host "  2. Make sure Docker Desktop settings:" -ForegroundColor White
        Write-Host "     - Settings → General → Use WSL 2 based engine (enabled)" -ForegroundColor White
        Write-Host "     - Settings → Resources → WSL Integration → Enable Ubuntu (enabled)" -ForegroundColor White
        Write-Host ""
    }
}
catch {
    Write-Host ""
    Write-Host "[ERROR] Could not test GPU access" -ForegroundColor Red
    Write-Host "       Docker might not be running" -ForegroundColor Yellow
    Write-Host ""
}

Write-Host "For manual verification, run:" -ForegroundColor Cyan
Write-Host "  docker run --rm --gpus all nvidia/cuda:12.0-base nvidia-smi" -ForegroundColor White
Write-Host ""
