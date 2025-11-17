#!/usr/bin/env pwsh
# Скрипт для переименования SharpTools в UltrasharpTools

param(
    [switch]$Apply = $false
)

$ErrorActionPreference = "Stop"

# Find project root (directory containing .sln files)
function Find-ProjectRoot {
    $current = $PSScriptRoot
    if (-not $current) { $current = Get-Location }

    while ($current) {
        if (Test-Path (Join-Path $current "*.sln")) {
            return $current
        }
        $parent = Split-Path -Parent $current
        if ($parent -eq $current) { break }  # Reached filesystem root
        $current = $parent
    }

    throw "Could not find project root (directory with .sln file)"
}

$rootPath = Find-ProjectRoot
Set-Location $rootPath

Write-Host "=== Переименование SharpTools → UltrasharpTools ===" -ForegroundColor Cyan
Write-Host "Директория: $rootPath" -ForegroundColor Gray
Write-Host ""

# Mapping замен
$replacements = @{
    # C# namespaces and types (точные совпадения)
    'namespace SharpTools' = 'namespace UltrasharpTools'
    'using SharpTools' = 'using UltrasharpTools'
    'SharpTools.Tools' = 'UltrasharpTools.Tools'
    'SharpTools.Test' = 'UltrasharpTools.Test'
    'SharpTools.RemoteServer' = 'UltrasharpTools.RemoteServer'
    'SharpTools.MCPServer' = 'UltrasharpTools.MCPServer'
    'SharpTools.Benchmarks' = 'UltrasharpTools.Benchmarks'

    # MCP tool names
    'SharpTool_' = 'UltrasharpTool_'

    # Project references и Assembly names
    'Include="SharpTools' = 'Include="UltrasharpTools'
    '<AssemblyName>SharpTools' = '<AssemblyName>UltrasharpTools'
    '<RootNamespace>SharpTools' = '<RootNamespace>UltrasharpTools'

    # Kebab-case для MCP server names
    'sharp-tools' = 'ultrasharp-tools-mcp'

    # README и документация
    'SharpTools MCP' = 'UltrasharpTools MCP'
    '# SharpTools' = '# UltrasharpTools'
    'SharpTools is' = 'UltrasharpTools is'
    'SharpTools provides' = 'UltrasharpTools provides'
}

# Паттерны файлов для обработки
$includePatterns = @('*.cs', '*.csproj', '*.sln', '*.md', '*.json', '*.yml', '*.yaml', '*.sh', '*.cmd', '*.ps1', '*.txt')

# Файлы и директории которые НЕ трогаем
$excludePatterns = @(
    'LICENSE*',
    '.git/*',
    '.vs/*',
    'bin/*',
    'obj/*',
    'node_modules/*',
    '*.dll',
    '*.exe',
    '*.log',
    'vectors.db*',
    'rename-sharp-to-ultrasharp.ps1',
    '.github/*'
)

function Test-ShouldExclude($path) {
    $relativePath = $path.Replace($rootPath, '').TrimStart('\', '/')
    foreach ($pattern in $excludePatterns) {
        if ($relativePath -like $pattern -or $relativePath -like "*\$pattern" -or $relativePath -like "*/$pattern") {
            return $true
        }
    }
    return $false
}

function Test-ShouldInclude($file) {
    foreach ($pattern in $includePatterns) {
        if ($file.Name -like $pattern) {
            return $true
        }
    }
    return $false
}

# Этап 1: Замена содержимого файлов
Write-Host "Этап 1: Замена содержимого файлов..." -ForegroundColor Yellow

$files = Get-ChildItem -Path $rootPath -Recurse -File | Where-Object {
    -not (Test-ShouldExclude $_.FullName) -and (Test-ShouldInclude $_)
}

$modifiedCount = 0
$totalReplacements = 0

foreach ($file in $files) {
    try {
        $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8 -ErrorAction Stop
        if (-not $content) { continue }

        $modified = $false
        $fileReplacements = 0
        $newContent = $content

        foreach ($kvp in $replacements.GetEnumerator()) {
            $old = $kvp.Key
            $new = $kvp.Value

            if ($newContent -match [regex]::Escape($old)) {
                $count = ([regex]::Matches($newContent, [regex]::Escape($old))).Count
                $newContent = $newContent -replace [regex]::Escape($old), $new
                $modified = $true
                $fileReplacements += $count
            }
        }

        if ($modified) {
            $modifiedCount++
            $totalReplacements += $fileReplacements
            $relativePath = $file.FullName.Replace($rootPath, '').TrimStart('\', '/')

            if ($Apply) {
                Set-Content -Path $file.FullName -Value $newContent -Encoding UTF8 -NoNewline
                Write-Host "  [✓] $relativePath ($fileReplacements замен)" -ForegroundColor Green
            } else {
                Write-Host "  [DRY] $relativePath ($fileReplacements замен)" -ForegroundColor Gray
            }
        }
    } catch {
        Write-Warning "Ошибка обработки файла $($file.Name): $_"
    }
}

Write-Host ""
Write-Host "Результат этапа 1:" -ForegroundColor Cyan
Write-Host "  Файлов обработано: $modifiedCount" -ForegroundColor White
Write-Host "  Всего замен: $totalReplacements" -ForegroundColor White
Write-Host ""

# Этап 2: Переименование файлов
Write-Host "Этап 2: Переименование файлов..." -ForegroundColor Yellow

$filesToRename = Get-ChildItem -Path $rootPath -Recurse -File | Where-Object {
    $_.Name -match 'SharpTools' -and -not (Test-ShouldExclude $_.FullName)
}

$renamedFiles = 0

foreach ($file in $filesToRename) {
    $newName = $file.Name -replace 'SharpTools', 'UltrasharpTools'
    $newPath = Join-Path $file.Directory.FullName $newName
    $relativePath = $file.FullName.Replace($rootPath, '').TrimStart('\', '/')

    if ($Apply) {
        try {
            Move-Item -Path $file.FullName -Destination $newPath -Force -ErrorAction Stop
            Write-Host "  [✓] $($file.Name) → $newName" -ForegroundColor Green
            $renamedFiles++
        } catch {
            Write-Warning "Не удалось переименовать $($file.Name): $_"
        }
    } else {
        Write-Host "  [DRY] $($file.Name) → $newName" -ForegroundColor Gray
        $renamedFiles++
    }
}

Write-Host ""
Write-Host "Результат этапа 2:" -ForegroundColor Cyan
Write-Host "  Файлов переименовано: $renamedFiles" -ForegroundColor White
Write-Host ""

# Этап 3: Переименование директорий (снизу вверх, чтобы избежать конфликтов)
Write-Host "Этап 3: Переименование директорий..." -ForegroundColor Yellow

$dirsToRename = Get-ChildItem -Path $rootPath -Recurse -Directory | Where-Object {
    $_.Name -match 'SharpTools' -and -not (Test-ShouldExclude $_.FullName)
} | Sort-Object -Property FullName -Descending

$renamedDirs = 0

foreach ($dir in $dirsToRename) {
    $newName = $dir.Name -replace 'SharpTools', 'UltrasharpTools'
    $newPath = Join-Path $dir.Parent.FullName $newName
    $relativePath = $dir.FullName.Replace($rootPath, '').TrimStart('\', '/')

    if ($Apply) {
        try {
            Move-Item -Path $dir.FullName -Destination $newPath -Force -ErrorAction Stop
            Write-Host "  [✓] $($dir.Name)/ → $newName/" -ForegroundColor Green
            $renamedDirs++
        } catch {
            Write-Warning "Не удалось переименовать директорию $($dir.Name): $_"
        }
    } else {
        Write-Host "  [DRY] $($dir.Name)/ → $newName/" -ForegroundColor Gray
        $renamedDirs++
    }
}

Write-Host ""
Write-Host "Результат этапа 3:" -ForegroundColor Cyan
Write-Host "  Директорий переименовано: $renamedDirs" -ForegroundColor White
Write-Host ""

# Итоговая статистика
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "ИТОГО:" -ForegroundColor Yellow
Write-Host "  Модифицировано файлов: $modifiedCount" -ForegroundColor White
Write-Host "  Замен в содержимом: $totalReplacements" -ForegroundColor White
Write-Host "  Переименовано файлов: $renamedFiles" -ForegroundColor White
Write-Host "  Переименовано директорий: $renamedDirs" -ForegroundColor White
Write-Host ""

if (-not $Apply) {
    Write-Host "Это был DRY RUN. Для применения изменений запустите:" -ForegroundColor Yellow
    Write-Host "  .\rename-sharp-to-ultrasharp.ps1 -Apply" -ForegroundColor Cyan
} else {
    Write-Host "Переименование завершено!" -ForegroundColor Green
}
