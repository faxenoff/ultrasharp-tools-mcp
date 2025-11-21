#!/usr/bin/env pwsh
# Setup script for CodeQL analysis

$ErrorActionPreference = "Stop"

Write-Host "=== CodeQL Setup for UltrasharpTools ===" -ForegroundColor Cyan
Write-Host ""

# Check if codeql CLI is installed
Write-Host "Checking CodeQL CLI..." -ForegroundColor Yellow
$codeqlPath = Get-Command codeql -ErrorAction SilentlyContinue

if (-not $codeqlPath) {
    Write-Host "❌ CodeQL CLI not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please install CodeQL CLI:" -ForegroundColor Yellow
    Write-Host "  1. Download from: https://github.com/github/codeql-cli-binaries/releases" -ForegroundColor Gray
    Write-Host "  2. Extract to a folder (e.g., C:\codeql)" -ForegroundColor Gray
    Write-Host "  3. Add to PATH: C:\codeql\codeql" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Or install via Chocolatey:" -ForegroundColor Yellow
    Write-Host "  choco install codeql" -ForegroundColor Gray
    exit 1
}

Write-Host "✓ CodeQL CLI found: $($codeqlPath.Source)" -ForegroundColor Green
codeql version

# Check if codeql-repo exists
$codeqlRepoPath = "D:\github\codeql-repo"
if (-not (Test-Path $codeqlRepoPath)) {
    Write-Host ""
    Write-Host "Cloning CodeQL standard queries..." -ForegroundColor Yellow
    git clone https://github.com/github/codeql.git $codeqlRepoPath
}

Write-Host "✓ CodeQL queries found at: $codeqlRepoPath" -ForegroundColor Green

# Create CodeQL database
$dbPath = "$PSScriptRoot\.codeql\database"
Write-Host ""
Write-Host "Creating CodeQL database..." -ForegroundColor Yellow
Write-Host "  Database path: $dbPath" -ForegroundColor Gray
Write-Host "  This may take several minutes..." -ForegroundColor Gray
Write-Host ""

if (Test-Path $dbPath) {
    Write-Host "⚠️  Database already exists. Delete it? (Y/N)" -ForegroundColor Yellow
    $response = Read-Host
    if ($response -eq 'Y' -or $response -eq 'y') {
        Remove-Item -Recurse -Force $dbPath
    } else {
        Write-Host "Using existing database." -ForegroundColor Gray
        exit 0
    }
}

# Build the project first
Write-Host "Building project..." -ForegroundColor Yellow
dotnet build -c Release

# Create CodeQL database for C#
codeql database create $dbPath `
    --language=csharp `
    --source-root="$PSScriptRoot" `
    --overwrite

Write-Host ""
Write-Host "✓ CodeQL database created successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Open VSCode" -ForegroundColor Gray
Write-Host "  2. Press Ctrl+Shift+P" -ForegroundColor Gray
Write-Host "  3. Type 'CodeQL: Choose Database from Folder'" -ForegroundColor Gray
Write-Host "  4. Select: $dbPath" -ForegroundColor Gray
Write-Host "  5. Run queries from CodeQL extension panel" -ForegroundColor Gray
