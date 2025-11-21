#!/usr/bin/env pwsh
# Fix CA1860: Replace .Any() with .Count > 0
param(
    [switch]$Apply
)

$ErrorActionPreference = "Stop"

Write-Host "=== Fixing CA1860: .Any() -> .Count > 0 ===" -ForegroundColor Cyan

$files = Get-ChildItem -Path "$PSScriptRoot" -Include "*.cs" -Recurse |
    Where-Object { $_.FullName -notmatch "\\obj\\|\\bin\\|\\Benchmarks\\" }

$changes = @()
$backupDir = "$PSScriptRoot\_bak\$(Get-Date -Format 'MMddHHmm')_fix-ca1860"

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw -Encoding UTF8
    $originalContent = $content

    # ВАЖНО: Count() - это метод LINQ, не свойство! Нужны скобки ()

    # Pattern 1: !collection.Any() -> collection.Count() == 0
    $content = $content -replace '!\s*(\w+)\.Any\(\)', '$1.Count() == 0'

    # Pattern 2: collection.Any() in if/while conditions -> collection.Count() > 0
    $content = $content -replace '\bif\s*\(\s*([^!][^)]+?)\.Any\(\)\s*\)', 'if ($1.Count() > 0)'
    $content = $content -replace '\bwhile\s*\(\s*([^)]+?)\.Any\(\)\s*\)', 'while ($1.Count() > 0)'
    $content = $content -replace '([^\s!]+)\.Any\(\)\s*\?', '$1.Count() > 0 ?'
    $content = $content -replace '&&\s*([^!][^\s]+?)\.Any\(\)', '&& $1.Count() > 0'
    $content = $content -replace '\|\|\s*([^!][^\s]+?)\.Any\(\)', '|| $1.Count() > 0'

    if ($content -ne $originalContent) {
        $relativePath = $file.FullName.Replace("$PSScriptRoot\", "")
        $changes += [PSCustomObject]@{
            File = $relativePath
            Path = $file.FullName
        }

        Write-Host "  📝 $relativePath" -ForegroundColor Yellow

        if ($Apply) {
            # Backup
            $backupPath = Join-Path $backupDir $relativePath
            $backupFolder = Split-Path $backupPath -Parent
            if (!(Test-Path $backupFolder)) {
                New-Item -ItemType Directory -Path $backupFolder -Force | Out-Null
            }
            Copy-Item $file.FullName $backupPath

            # Apply
            $content | Set-Content $file.FullName -Encoding UTF8 -NoNewline
        }
    }
}

Write-Host ""
Write-Host "Found $($changes.Count) files to modify" -ForegroundColor Green

if ($changes.Count -gt 0 -and !$Apply) {
    Write-Host ""
    Write-Host "DRY RUN - No changes applied" -ForegroundColor Yellow
    Write-Host "Run with -Apply to apply changes" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Files to be modified:" -ForegroundColor Cyan
    $changes | ForEach-Object { Write-Host "  - $($_.File)" -ForegroundColor Gray }
} elseif ($changes.Count -gt 0 -and $Apply) {
    Write-Host ""
    Write-Host "✅ Changes applied!" -ForegroundColor Green
    Write-Host "Backup saved to: $backupDir" -ForegroundColor Gray
}
