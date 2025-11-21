#!/usr/bin/env pwsh
# Export all code analysis issues to CSV

$ErrorActionPreference = "Stop"

Write-Host "=== Exporting Code Analysis Issues ===" -ForegroundColor Cyan

# Run dotnet build with /warnaserror- to see all warnings
Write-Host "Running Roslyn analyzers..." -ForegroundColor Yellow

$output = dotnet build -c Release `
    /p:TreatWarningsAsErrors=false `
    /p:CodeAnalysisLevel=latest `
    /p:AnalysisLevel=latest `
    /p:EnforceCodeStyleInBuild=true `
    /warnaserror- `
    /v:detailed 2>&1

# Parse build output for diagnostics
$issues = @()
$linePattern = '(?<file>[^(]+)\((?<line>\d+),(?<col>\d+)\):\s+(?<severity>\w+)\s+(?<code>\w+):\s+(?<message>.+)'

foreach ($line in $output) {
    if ($line -match $linePattern) {
        $issues += [PSCustomObject]@{
            File = $matches['file']
            Line = $matches['line']
            Column = $matches['col']
            Severity = $matches['severity']
            Code = $matches['code']
            Message = $matches['message']
        }
    }
}

Write-Host "Found $($issues.Count) issues" -ForegroundColor Green

# Export to CSV
$csvPath = "$PSScriptRoot\code-issues.csv"
$issues | Export-Csv -Path $csvPath -NoTypeInformation -Encoding UTF8

Write-Host "Exported to: $csvPath" -ForegroundColor Green

# Group by severity
$grouped = $issues | Group-Object Severity | Sort-Object Count -Descending
Write-Host ""
Write-Host "=== By Severity ===" -ForegroundColor Cyan
foreach ($group in $grouped) {
    Write-Host "$($group.Name): $($group.Count)" -ForegroundColor Yellow
}

# Group by code
$byCode = $issues | Group-Object Code | Sort-Object Count -Descending | Select-Object -First 20
Write-Host ""
Write-Host "=== Top 20 Issue Types ===" -ForegroundColor Cyan
foreach ($item in $byCode) {
    Write-Host "$($item.Name): $($item.Count)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Open $csvPath in Excel or VS Code to view all issues" -ForegroundColor Green
