#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Auto-detect GPU architecture for semantic embedding configuration
.DESCRIPTION
    Detects NVIDIA GPU compute capability and recommends TEI architecture tag
#>

$ErrorActionPreference = "Stop"

Write-Host "=== GPU Architecture Detection ===" -ForegroundColor Cyan
Write-Host ""

# Check if nvidia-smi is available
try {
    $nvidiaSmi = Get-Command nvidia-smi -ErrorAction Stop
    Write-Host "✓ nvidia-smi found" -ForegroundColor Green
} catch {
    Write-Host "✗ nvidia-smi not found - No NVIDIA GPU detected" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Recommendation: architecture: cpu" -ForegroundColor Cyan
    Write-Output "cpu"
    exit 0
}

# Get GPU information
Write-Host "Detecting GPU..." -ForegroundColor Yellow

try {
    $gpuInfo = nvidia-smi --query-gpu=name,compute_cap --format=csv,noheader 2>&1

    if ($LASTEXITCODE -ne 0) {
        throw "nvidia-smi query failed"
    }

    # Convert to string if it's an array (PowerShell 2>&1 can return ErrorRecord objects)
    $gpuInfoText = if ($gpuInfo -is [Array]) {
        ($gpuInfo | Where-Object { $_ -is [string] }) -join "`n"
    } else {
        $gpuInfo.ToString()
    }

    $lines = $gpuInfoText -split "`n" | Where-Object { $_.Trim() -ne "" }

    if ($lines.Count -eq 0) {
        Write-Host "✗ No NVIDIA GPUs detected" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Recommendation: architecture: cpu" -ForegroundColor Cyan
        Write-Output "cpu"
        exit 0
    }

    # Parse first GPU
    $firstLine = $lines[0].ToString().Trim()
    if (-not $firstLine) {
        Write-Host "✗ No GPU data returned" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Recommendation: architecture: cpu" -ForegroundColor Cyan
        Write-Output "cpu"
        exit 0
    }

    $parts = $firstLine -split "," | ForEach-Object { $_.Trim() }
    if ($parts.Count -lt 2) {
        Write-Host "✗ Invalid GPU data format: '$firstLine'" -ForegroundColor Yellow
        Write-Host "  Parts count: $($parts.Count)" -ForegroundColor DarkGray
        Write-Host ""
        Write-Host "Recommendation: architecture: cpu" -ForegroundColor Cyan
        Write-Output "cpu"
        exit 0
    }

    $gpuName = $parts[0].Trim()
    $computeCap = if ($parts.Count -ge 2) { $parts[1].Trim() } else { "" }

    if (-not $computeCap) {
        Write-Host "✗ No compute capability data" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Recommendation: architecture: cpu" -ForegroundColor Cyan
        Write-Output "cpu"
        exit 0
    }

    Write-Host "✓ Detected: $gpuName" -ForegroundColor Green
    Write-Host "  Compute Capability: $computeCap" -ForegroundColor DarkGray
    Write-Host ""

    # Map compute capability to architecture
    $arch = switch ($computeCap) {
        # Turing (75)
        { $_ -eq "7.5" } { "turing" }

        # Ampere (80, 86, 87)
        { $_ -eq "8.0" } { "ampere-80" }
        { $_ -eq "8.6" } { "ampere-86" }
        { $_ -eq "8.7" } { "ampere-86" }  # Same as 8.6

        # Ada Lovelace (89)
        { $_ -eq "8.9" } { "ada" }

        # Hopper (90)
        { $_ -eq "9.0" } { "hopper" }

        # Blackwell (100, 102) - RTX 5000 series
        { $_ -eq "10.0" } { "blackwell" }
        { $_ -eq "10.2" } { "blackwell" }

        # Future architectures (120+) - experimental
        { $_ -eq "12.0" } { "blackwell-experimental" }

        default {
            if ([double]$_ -lt 7.5) {
                "cpu"  # Too old, use CPU
            } else {
                "cpu"  # Unknown, use CPU for safety
            }
        }
    }

    # Architecture info
    $archInfo = switch ($arch) {
        "turing" { "Turing (RTX 2000 series, T4)" }
        "ampere-80" { "Ampere A100/A30 (CC 8.0)" }
        "ampere-86" { "Ampere A10/A40 (CC 8.6)" }
        "ada" { "Ada Lovelace (RTX 4000 series)" }
        "hopper" { "Hopper (H100)" }
        "blackwell-experimental" { "Blackwell (RTX 5000 series) - EXPERIMENTAL" }
        "cpu" { "CPU mode (GPU too old or unsupported)" }
    }

    Write-Host "Architecture: $archInfo" -ForegroundColor Cyan
    Write-Host ""

    if ($arch -eq "blackwell-experimental") {
        Write-Host "⚠ WARNING: Blackwell support is experimental!" -ForegroundColor Yellow
        Write-Host "   TEI may not work with RTX 5000 series GPUs yet." -ForegroundColor Yellow
        Write-Host "   Recommendation: Use 'ada' (Ada Lovelace) or 'cpu' mode" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Falling back to: cpu" -ForegroundColor Cyan
        Write-Output "cpu"
    } else {
        Write-Host "Recommendation: architecture: $arch" -ForegroundColor Cyan
        Write-Output $arch
    }

} catch {
    Write-Host "✗ Error detecting GPU: $_" -ForegroundColor Red
    Write-Host ""
    Write-Host "Recommendation: architecture: cpu" -ForegroundColor Cyan
    Write-Output "cpu"
    exit 0
}
