# Publish UltrasharpTools.Agent - Lightweight hybrid client
# Target: 32 MB single-file executable
#
# Features:
# - Trimming для удаления неиспользуемого кода
# - ReadyToRun для быстрого старта
# - Dynamic PGO для оптимизации hot paths
# - Workstation GC для меньшего потребления памяти
#
# Usage: .\Dev.Scripts\publish-agent.ps1

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDir = "Run.Publish/Agent"
)

$ErrorActionPreference = "Stop"

Write-Host "Publishing UltrasharpTools.Agent..." -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Gray
Write-Host "Runtime: $Runtime" -ForegroundColor Gray
Write-Host "Output: $OutputDir" -ForegroundColor Gray
Write-Host ""

# Clean output directory
if (Test-Path $OutputDir) {
    Write-Host "Cleaning output directory..." -ForegroundColor Yellow
    Remove-Item -Path $OutputDir -Recurse -Force
}

# Publish
Write-Host "Publishing Agent..." -ForegroundColor Green
dotnet publish UltrasharpTools.Agent/UltrasharpTools.Agent.csproj `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:PublishTrimmed=true `
    -p:TrimMode=link `
    -p:PublishReadyToRun=true `
    -p:EnableCompressionInSingleFile=true `
    --output $OutputDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Publish successful!" -ForegroundColor Green

# Show file sizes
Write-Host ""
Write-Host "Output files:" -ForegroundColor Cyan
Get-ChildItem -Path $OutputDir -File | ForEach-Object {
    $sizeMB = [math]::Round($_.Length / 1MB, 2)
    Write-Host "  $($_.Name): $sizeMB MB" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Agent published to: $OutputDir" -ForegroundColor Green
Write-Host ""
Write-Host "Usage:" -ForegroundColor Cyan
Write-Host "  $OutputDir\UltrasharpTools.Agent.exe --project-name MyProject --server-url http://localhost:3001" -ForegroundColor Gray
