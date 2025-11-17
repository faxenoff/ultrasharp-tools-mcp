#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Convert slow tokenizer to fast tokenizer for TEI compatibility
.PARAMETER ModelId
    HuggingFace model ID (e.g., 'ibm-granite/granite-embedding-125m-english')
.PARAMETER OutputDir
    Output directory for fast tokenizer (optional)
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$ModelId,

    [Parameter(Mandatory=$false)]
    [string]$OutputDir = $null
)

$ErrorActionPreference = "Stop"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "Fast Tokenizer Converter for TEI" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

# Check if Python is available
try {
    $pythonVersion = python --version 2>&1
    Write-Host "✓ Python found: $pythonVersion" -ForegroundColor Green
} catch {
    Write-Host "✗ Python not found. Please install Python 3.8+" -ForegroundColor Red
    exit 1
}

# Check if transformers is installed
Write-Host "Checking dependencies..." -ForegroundColor Yellow
$transformersCheck = python -c "import transformers; print(transformers.__version__)" 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ transformers library not found" -ForegroundColor Red
    Write-Host ""
    $install = Read-Host "Install transformers? [Y/n]"
    if ($install -eq "" -or $install -eq "Y" -or $install -eq "y") {
        Write-Host "Installing transformers..." -ForegroundColor Yellow
        pip install transformers
        if ($LASTEXITCODE -ne 0) {
            Write-Host "✗ Failed to install transformers" -ForegroundColor Red
            exit 1
        }
    } else {
        exit 1
    }
} else {
    Write-Host "✓ transformers $transformersCheck installed" -ForegroundColor Green
}

Write-Host ""

# Run conversion script
$scriptPath = Join-Path $PSScriptRoot "convert-tokenizer-to-fast.py"

if ($OutputDir) {
    python $scriptPath $ModelId --output $OutputDir
} else {
    python $scriptPath $ModelId
}

exit $LASTEXITCODE
