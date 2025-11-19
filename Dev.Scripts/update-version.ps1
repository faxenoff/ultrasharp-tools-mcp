#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Update version across all project files
.DESCRIPTION
    Updates version in .csproj files, Dockerfile, and markdown documentation
.PARAMETER Version
    New version number (e.g., "3.1.0")
.PARAMETER DryRun
    Show what would be changed without actually modifying files
.EXAMPLE
    .\update-version.ps1 -Version "3.1.0"
.EXAMPLE
    .\update-version.ps1 -Version "3.1.0" -DryRun
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,

    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# Validate version format (semantic versioning)
if ($Version -notmatch '^\d+\.\d+\.\d+(-[a-zA-Z0-9\.\-]+)?$') {
    Write-Host "❌ Invalid version format: $Version" -ForegroundColor Red
    Write-Host "Expected format: MAJOR.MINOR.PATCH (e.g., 3.1.0 or 3.1.0-beta.1)" -ForegroundColor Yellow
    exit 1
}

# Find project root
function Find-ProjectRoot {
    $current = $PSScriptRoot
    if (-not $current) { $current = Get-Location }

    while ($current) {
        if (Test-Path (Join-Path $current "*.sln")) {
            return $current
        }
        $parent = Split-Path -Parent $current
        if ($parent -eq $current) { break }
        $current = $parent
    }

    throw "Could not find project root (directory with .sln file)"
}

$ProjectRoot = Find-ProjectRoot
Set-Location $ProjectRoot

# Colors
function Write-Header { param([string]$Text) Write-Host "`n=== $Text ===" -ForegroundColor Cyan }
function Write-Success { param([string]$Text) Write-Host "✓ $Text" -ForegroundColor Green }
function Write-Info { param([string]$Text) Write-Host "→ $Text" -ForegroundColor Yellow }
function Write-Error { param([string]$Text) Write-Host "✗ $Text" -ForegroundColor Red }
function Write-DryRun { param([string]$Text) Write-Host "[DRY RUN] $Text" -ForegroundColor Magenta }

Write-Header "Version Update Tool"
Write-Info "Project root: $ProjectRoot"
Write-Info "New version: $Version"

if ($DryRun) {
    Write-DryRun "Dry run mode - no files will be modified"
}

Write-Host ""

# Define files to update with their patterns
$filesToUpdate = @(
    # .csproj files
    @{
        Path = "UltrasharpTools.Tools\UltrasharpTools.Tools.csproj"
        Pattern = '<Version>[\d\.]+(-[a-zA-Z0-9\.\-]+)?</Version>'
        Replacement = "<Version>$Version</Version>"
        Description = ".csproj (Tools)"
    },
    @{
        Path = "UltrasharpTools.Droid\UltrasharpTools.Droid.csproj"
        Pattern = '<Version>[\d\.]+(-[a-zA-Z0-9\.\-]+)?</Version>'
        Replacement = "<Version>$Version</Version>"
        Description = ".csproj (Droid)"
    },
    @{
        Path = "UltrasharpTools.Overlord\UltrasharpTools.Overlord.csproj"
        Pattern = '<Version>[\d\.]+(-[a-zA-Z0-9\.\-]+)?</Version>'
        Replacement = "<Version>$Version</Version>"
        Description = ".csproj (Overlord)"
    },

    # Dockerfile - version label
    @{
        Path = "UltrasharpTools.Overlord\Dockerfile"
        Pattern = 'version="[\d\.]+(-[a-zA-Z0-9\.\-]+)?"'
        Replacement = "version=`"$Version`""
        Description = "Dockerfile (version label)"
    },

    # Dockerfile - kubernetes version label
    @{
        Path = "UltrasharpTools.Overlord\Dockerfile"
        Pattern = 'app\.kubernetes\.io/version="[\d\.]+(-[a-zA-Z0-9\.\-]+)?"'
        Replacement = "app.kubernetes.io/version=`"$Version`""
        Description = "Dockerfile (k8s version)"
    },

    # Program.cs files - ApplicationVersion constants
    @{
        Path = "UltrasharpTools.Droid\Program.cs"
        Pattern = 'ApplicationVersion = "[\d\.]+(-[a-zA-Z0-9\.\-]+)?"'
        Replacement = "ApplicationVersion = `"$Version`""
        Description = "Program.cs (Droid)"
    },
    @{
        Path = "UltrasharpTools.Overlord\Program.cs"
        Pattern = 'ApplicationVersion = "[\d\.]+(-[a-zA-Z0-9\.\-]+)?"'
        Replacement = "ApplicationVersion = `"$Version`""
        Description = "Program.cs (Overlord)"
    },

    # AgentController.cs - health endpoint version
    @{
        Path = "UltrasharpTools.Overlord\Controllers\AgentController.cs"
        Pattern = 'version = "[\d\.]+(-[a-zA-Z0-9\.\-]+)?"'
        Replacement = "version = `"$Version`""
        Description = "AgentController.cs (health endpoint)"
    },

    # Helm Chart
    @{
        Path = "Run.Docs\Deployment\helm\ultrasharp-tools\Chart.yaml"
        Pattern = 'version: [\d\.]+(-[a-zA-Z0-9\.\-]+)?'
        Replacement = "version: $Version"
        Description = "Chart.yaml (version)"
    },
    @{
        Path = "Run.Docs\Deployment\helm\ultrasharp-tools\Chart.yaml"
        Pattern = 'appVersion: "[\d\.]+(-[a-zA-Z0-9\.\-]+)?"'
        Replacement = "appVersion: `"$Version`""
        Description = "Chart.yaml (appVersion)"
    },

    # Deployment README - Docker examples
    @{
        Path = "Run.Docs\Deployment\README.md"
        Pattern = 'ghcr\.io/YOUR_ORG/ultrasharp-tools-overlord:v[\d\.]+(-[a-zA-Z0-9\.\-]+)?'
        Replacement = "ghcr.io/YOUR_ORG/ultrasharp-tools-overlord:v$Version"
        Description = "Deployment README (Docker examples)"
    },

    # Markdown documentation
    @{
        Path = "ARCHITECTURE.md"
        Pattern = '\*\*Версия:\*\* [\d\.]+(-[a-zA-Z0-9\.\-]+)?'
        Replacement = "**Версия:** $Version"
        Description = "ARCHITECTURE.md"
    },
    @{
        Path = "ROADMAP.md"
        Pattern = '\*\*Текущая версия:\*\* [\d\.]+(-[a-zA-Z0-9\.\-]+)?'
        Replacement = "**Текущая версия:** $Version"
        Description = "ROADMAP.md"
    },
    @{
        Path = "USAGE_GUIDE.md"
        Pattern = '\*\*Версия:\*\* [\d\.]+(-[a-zA-Z0-9\.\-]+)?'
        Replacement = "**Версия:** $Version"
        Description = "USAGE_GUIDE.md"
    },
    @{
        Path = "Run.Docs\OVERLORD_README.md"
        Pattern = '\*\*Версия:\*\* [\d\.]+(-[a-zA-Z0-9\.\-]+)?'
        Replacement = "**Версия:** $Version"
        Description = "OVERLORD_README.md"
    },
    @{
        Path = "Dev.Docs\README.md"
        Pattern = '\*\*Версия проекта:\*\* [\d\.]+(-[a-zA-Z0-9\.\-]+)?'
        Replacement = "**Версия проекта:** $Version"
        Description = "Dev.Docs/README.md"
    }
)

$updatedCount = 0
$skippedCount = 0
$errorCount = 0

Write-Header "Updating Files"

foreach ($fileInfo in $filesToUpdate) {
    $filePath = Join-Path $ProjectRoot $fileInfo.Path

    if (-not (Test-Path $filePath)) {
        Write-Error "File not found: $($fileInfo.Path)"
        $errorCount++
        continue
    }

    try {
        $content = Get-Content $filePath -Raw -Encoding UTF8
        $oldContent = $content

        # Apply regex replacement
        $content = $content -replace $fileInfo.Pattern, $fileInfo.Replacement

        if ($content -eq $oldContent) {
            Write-Info "No changes needed: $($fileInfo.Description)"
            $skippedCount++
        }
        else {
            if ($DryRun) {
                Write-DryRun "Would update: $($fileInfo.Description)"

                # Show what would change
                $matches = [regex]::Matches($oldContent, $fileInfo.Pattern)
                foreach ($match in $matches) {
                    Write-Host "  OLD: $($match.Value)" -ForegroundColor DarkGray
                    Write-Host "  NEW: $($fileInfo.Replacement)" -ForegroundColor DarkGray
                }
            }
            else {
                # Actually write the file
                Set-Content -Path $filePath -Value $content -Encoding UTF8 -NoNewline
                Write-Success "Updated: $($fileInfo.Description)"
            }
            $updatedCount++
        }
    }
    catch {
        Write-Error "Error processing $($fileInfo.Path): $_"
        $errorCount++
    }
}

# Update CHANGELOG.md (special handling)
Write-Host ""
Write-Header "CHANGELOG.md"

$changelogPath = Join-Path $ProjectRoot "CHANGELOG.md"
if (Test-Path $changelogPath) {
    $changelogContent = Get-Content $changelogPath -Raw -Encoding UTF8

    # Check if version already exists in changelog
    if ($changelogContent -match "## \[$Version\]") {
        Write-Info "Version $Version already exists in CHANGELOG.md"
        $skippedCount++
    }
    else {
        $today = Get-Date -Format "yyyy-MM-dd"
        $newEntry = @"
## [$Version] - $today

### 🎯 Статус
**TBD** - Brief description of this release

### Добавлено
- TODO: Add new features here

### Изменено
- TODO: Add changes here

### Исправлено
- TODO: Add fixes here

---

"@

        # Find where to insert (after "---" following header)
        $insertPosition = $changelogContent.IndexOf("---`n`n") + 6

        if ($insertPosition -gt 6) {
            if ($DryRun) {
                Write-DryRun "Would add new version entry to CHANGELOG.md"
                Write-Host "`n$newEntry" -ForegroundColor DarkGray
            }
            else {
                $newContent = $changelogContent.Insert($insertPosition, $newEntry)
                Set-Content -Path $changelogPath -Value $newContent -Encoding UTF8 -NoNewline
                Write-Success "Added new version entry to CHANGELOG.md"
                Write-Info "Please update the CHANGELOG.md entry with actual changes!"
            }
            $updatedCount++
        }
        else {
            Write-Error "Could not find insertion point in CHANGELOG.md"
            $errorCount++
        }
    }
}
else {
    Write-Error "CHANGELOG.md not found"
    $errorCount++
}

# Summary
Write-Host ""
Write-Header "Summary"
Write-Host ""

if ($DryRun) {
    Write-DryRun "Dry run completed - no files were modified"
    Write-Host ""
}

Write-Info "Files that would be updated: $updatedCount"
Write-Info "Files skipped (no changes): $skippedCount"

if ($errorCount -gt 0) {
    Write-Error "Errors encountered: $errorCount"
}

Write-Host ""

if ($updatedCount -gt 0) {
    if ($DryRun) {
        Write-Success "Dry run successful! Run without -DryRun to apply changes."
    }
    else {
        Write-Success "Version updated to $Version successfully!"
        Write-Host ""
        Write-Info "Next steps:"
        Write-Host "  1. Update CHANGELOG.md entry with actual changes" -ForegroundColor Yellow
        Write-Host "  2. Review changes: git diff" -ForegroundColor Yellow
        Write-Host "  3. Commit: git add . && git commit -m 'Bump version to $Version'" -ForegroundColor Yellow
        Write-Host "  4. Tag: git tag v$Version" -ForegroundColor Yellow
        Write-Host "  5. Push: git push && git push --tags" -ForegroundColor Yellow
    }
}
else {
    Write-Info "No files needed updating"
}

Write-Host ""
