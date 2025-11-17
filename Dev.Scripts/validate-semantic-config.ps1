#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validate and test semantic embedding configuration
.DESCRIPTION
    Checks if embedding services are available and configuration is valid
#>

param(
    [switch]$Fix,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

if ($Verbose) {
    $VerbosePreference = "Continue"
}

function Write-Header {
    param([string]$Text)
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host $Text -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
}

function Write-Success {
    param([string]$Text)
    Write-Host "✓ $Text" -ForegroundColor Green
}

function Write-Failure {
    param([string]$Text)
    Write-Host "✗ $Text" -ForegroundColor Red
}

function Write-Warning {
    param([string]$Text)
    Write-Host "⚠ $Text" -ForegroundColor Yellow
}

function Write-Info {
    param([string]$Text)
    Write-Host "→ $Text" -ForegroundColor White
}

function Test-ServiceHealth {
    param(
        [string]$ServiceName,
        [string]$Url,
        [int]$TimeoutSeconds = 5
    )

    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec $TimeoutSeconds -ErrorAction Stop
        if ($response.StatusCode -eq 200) {
            return @{ Success = $true; Message = "Available" }
        }
        return @{ Success = $false; Message = "Status: $($response.StatusCode)" }
    }
    catch [System.Net.WebException] {
        return @{ Success = $false; Message = "Connection refused or timeout" }
    }
    catch {
        return @{ Success = $false; Message = $_.Exception.Message }
    }
}

Write-Header "Semantic Embedding Configuration Validator"

# Step 1: Check if config exists
Write-Host "[1/5] Checking configuration file..." -ForegroundColor Yellow
Write-Host ""

$configPath = Join-Path $PSScriptRoot "semantic-config.json"

if (Test-Path $configPath) {
    Write-Success "Configuration file found: $configPath"

    try {
        $config = Get-Content $configPath -Raw | ConvertFrom-Json
        Write-Success "Configuration is valid JSON"
    }
    catch {
        Write-Failure "Configuration file is invalid JSON"
        Write-Info "Error: $($_.Exception.Message)"
        if ($Fix) {
            Write-Warning "Run: .\setup-semantic-embedding.ps1 to recreate config"
        }
        exit 1
    }
}
else {
    Write-Warning "Configuration file not found"
    Write-Info "Location: $configPath"

    if ($Fix) {
        Write-Host ""
        Write-Info "Running auto-configuration..."
        & (Join-Path $PSScriptRoot "setup-semantic-embedding.ps1")
        exit 0
    }
    else {
        Write-Host ""
        Write-Info "Run with --Fix flag to create configuration:"
        Write-Info "  .\validate-semantic-config.ps1 -Fix"
        exit 1
    }
}

# Step 2: Check platform configuration
Write-Host ""
Write-Host "[2/5] Checking platform configuration..." -ForegroundColor Yellow
Write-Host ""

$platform = $config.embedding.platform
Write-Info "Platform: $platform"

if ($platform -notin @("tei", "ollama", "memory")) {
    Write-Failure "Unknown platform: $platform"
    Write-Info "Valid platforms: tei, ollama, memory"
    exit 1
}

Write-Success "Platform is valid"

# Step 3: Check architecture
Write-Host ""
Write-Host "[3/5] Checking GPU architecture..." -ForegroundColor Yellow
Write-Host ""

$architecture = $config.embedding.architecture
Write-Info "Architecture: $architecture"

if ($architecture -eq "auto") {
    Write-Warning "Architecture is set to 'auto' - will be detected at runtime"
}
elseif ($architecture -in @("cpu", "turing", "ampere-80", "ampere-86", "ada", "hopper", "blackwell")) {
    Write-Success "Architecture is valid"

    if ($architecture -eq "blackwell") {
        Write-Warning "Blackwell support is experimental - TEI may not work"
        Write-Info "Consider using 'cpu' or 'ollama' platform"
    }
}
else {
    Write-Warning "Unknown architecture: $architecture"
    Write-Info "Valid: auto, cpu, turing, ampere-80, ampere-86, ada, hopper, blackwell"
}

# Step 4: Test service availability
Write-Host ""
Write-Host "[4/5] Testing service availability..." -ForegroundColor Yellow
Write-Host ""

$serviceAvailable = $false

switch ($platform) {
    "tei" {
        $endpoint = $config.embedding.tei.endpoint
        Write-Info "Testing TEI at: $endpoint"

        $healthUrl = "$($endpoint.TrimEnd('/'))/health"
        $result = Test-ServiceHealth -ServiceName "TEI" -Url $healthUrl

        if ($result.Success) {
            Write-Success "TEI is available"
            $serviceAvailable = $true
        }
        else {
            Write-Failure "TEI is not available"
            Write-Info "  Error: $($result.Message)"
            Write-Host ""
            Write-Warning "TEI server is not running"
            Write-Info "Start TEI server:"
            Write-Info "  .\Dev.Scripts\setup-tei.ps1"
            Write-Host ""
            Write-Info "Or switch to Ollama:"
            Write-Info "  1. Install Ollama: https://ollama.ai"
            Write-Info "  2. Pull model: ollama pull granite-embedding"
            Write-Info "  3. Update config: platform = 'ollama'"
        }
    }

    "ollama" {
        $endpoint = $config.embedding.ollama.endpoint
        Write-Info "Testing Ollama at: $endpoint"

        $tagsUrl = "$($endpoint.TrimEnd('/'))/api/tags"
        $result = Test-ServiceHealth -ServiceName "Ollama" -Url $tagsUrl

        if ($result.Success) {
            Write-Success "Ollama is available"
            $serviceAvailable = $true

            # Check models
            try {
                $response = Invoke-RestMethod -Uri $tagsUrl -TimeoutSec 5
                $modelCount = $response.models.Count
                Write-Success "Found $modelCount model(s)"

                $selectedModel = $config.embedding.ollama.selected_model
                $hasModel = $response.models | Where-Object { $_.name -eq $selectedModel }

                if ($hasModel) {
                    Write-Success "Selected model is available: $selectedModel"
                }
                else {
                    Write-Warning "Selected model not found: $selectedModel"
                    Write-Info "Install model:"
                    Write-Info "  ollama pull $selectedModel"
                }
            }
            catch {
                Write-Warning "Could not check Ollama models"
            }
        }
        else {
            Write-Failure "Ollama is not available"
            Write-Info "  Error: $($result.Message)"
            Write-Host ""
            Write-Warning "Ollama is not installed or not running"
            Write-Info "Install Ollama:"
            Write-Info "  1. Visit: https://ollama.ai"
            Write-Info "  2. Download and install"
            Write-Info "  3. Pull embedding model: ollama pull granite-embedding"
        }
    }

    "memory" {
        Write-Warning "Memory platform is experimental"
        Write-Info "Consider using TEI or Ollama for better performance"
        $serviceAvailable = $true
    }
}

# Step 5: Test embedding generation
Write-Host ""
Write-Host "[5/5] Testing embedding generation..." -ForegroundColor Yellow
Write-Host ""

if ($serviceAvailable) {
    Write-Info "Generating test embedding..."

    $testText = "Hello, this is a test"
    $success = $false

    try {
        switch ($platform) {
            "tei" {
                $endpoint = $config.embedding.tei.endpoint
                $embedUrl = "$($endpoint.TrimEnd('/'))/embed"
                $body = @{ inputs = $testText } | ConvertTo-Json
                $response = Invoke-RestMethod -Uri $embedUrl -Method Post -Body $body -ContentType "application/json" -TimeoutSec 10
                if ($response -and $response[0] -and $response[0].Count -gt 0) {
                    Write-Success "Embedding generated successfully (${$response[0].Count} dimensions)"
                    $success = $true
                }
            }

            "ollama" {
                $endpoint = $config.embedding.ollama.endpoint
                $model = $config.embedding.ollama.selected_model
                $embedUrl = "$($endpoint.TrimEnd('/'))/api/embeddings"
                $body = @{ model = $model; prompt = $testText } | ConvertTo-Json
                $response = Invoke-RestMethod -Uri $embedUrl -Method Post -Body $body -ContentType "application/json" -TimeoutSec 10
                if ($response.embedding -and $response.embedding.Count -gt 0) {
                    Write-Success "Embedding generated successfully ($($response.embedding.Count) dimensions)"
                    $success = $true
                }
            }

            "memory" {
                Write-Warning "Cannot test memory platform without runtime"
                $success = $true
            }
        }
    }
    catch {
        Write-Failure "Failed to generate embedding"
        Write-Info "Error: $($_.Exception.Message)"
        $success = $false
    }

    if (!$success -and $platform -ne "memory") {
        Write-Host ""
        Write-Warning "Service is available but embedding generation failed"
        Write-Info "Check service logs for details"
    }
}
else {
    Write-Warning "Skipping embedding test - service not available"
}

# Summary
Write-Header "Validation Summary"

if ($serviceAvailable) {
    Write-Host "Status:           " -NoNewline
    Write-Host "READY" -ForegroundColor Green
    Write-Host "Platform:         $platform"
    Write-Host "Architecture:     $architecture"
    Write-Host "Configuration:    $configPath"
    Write-Host ""
    Write-Success "Semantic embedding is configured and ready to use!"
}
else {
    Write-Host "Status:           " -NoNewline
    Write-Host "NOT READY" -ForegroundColor Red
    Write-Host "Platform:         $platform"
    Write-Host "Architecture:     $architecture"
    Write-Host "Configuration:    $configPath"
    Write-Host ""
    Write-Failure "Semantic embedding is not ready"
    Write-Host ""
    Write-Info "Follow the instructions above to fix the issues"

    if ($Fix) {
        Write-Host ""
        $runSetup = Read-Host "Run setup wizard now? [Y/n]"
        if ($runSetup -ne "n" -and $runSetup -ne "N") {
            & (Join-Path $PSScriptRoot "setup-semantic-embedding.ps1")
        }
    }
}

Write-Host "═══════════════════════════════════════════════════════════"
Write-Host ""

exit $(if ($serviceAvailable) { 0 } else { 1 })
