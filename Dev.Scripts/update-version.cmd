@echo off
REM ============================================================================
REM update-version.cmd - Automatic Version Update Tool
REM ============================================================================
REM
REM DESCRIPTION:
REM   Updates version across all project files automatically
REM
REM WHAT GETS UPDATED (11 places):
REM   - .csproj files (3): Tools, Droid, Overlord
REM   - Dockerfile (2): version label + k8s version
REM   - Markdown docs (6): ARCHITECTURE.md, ROADMAP.md, USAGE_GUIDE.md,
REM                        OVERLORD_README.md, Dev.Docs/README.md, CHANGELOG.md
REM
REM USAGE:
REM   update-version.cmd <version> [--dry-run]
REM
REM EXAMPLES:
REM   update-version.cmd 3.1.0              - Update to version 3.1.0
REM   update-version.cmd 3.1.0 --dry-run    - Preview changes without applying
REM   update-version.cmd 4.0.0-beta.1       - Update to pre-release version
REM
REM VERSION FORMAT:
REM   Semantic Versioning: MAJOR.MINOR.PATCH[-PRERELEASE]
REM   Valid:   3.0.0, 3.1.0, 4.0.0-beta.1, 4.0.0-rc.2
REM   Invalid: 3.1, v3.1.0, 3.1.x
REM
REM WORKFLOW:
REM   1. Run with --dry-run to preview changes
REM   2. Run without flag to apply changes
REM   3. Update CHANGELOG.md TODO sections manually
REM   4. Review: git diff
REM   5. Commit: git add . && git commit -m "Bump version to X.Y.Z"
REM   6. Tag: git tag vX.Y.Z
REM   7. Push: git push && git push --tags
REM
REM REQUIREMENTS:
REM   - PowerShell 5.1+ or PowerShell Core 7+
REM
REM ============================================================================

setlocal

REM Get script directory
set SCRIPT_DIR=%~dp0

REM Check if version parameter is provided
if "%~1"=="" (
    echo Error: Version parameter is required
    echo.
    echo Usage: %~nx0 ^<version^> [--dry-run]
    echo Example: %~nx0 3.1.0
    echo Example: %~nx0 3.1.0 --dry-run
    exit /b 1
)

set VERSION=%~1
set DRY_RUN_FLAG=

REM Check for --dry-run flag
if /i "%~2"=="--dry-run" set DRY_RUN_FLAG=-DryRun
if /i "%~2"=="-d" set DRY_RUN_FLAG=-DryRun

REM Check for PowerShell availability
where pwsh >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    set PS_CMD=pwsh
) else (
    where powershell >nul 2>&1
    if %ERRORLEVEL% EQU 0 (
        set PS_CMD=powershell
    ) else (
        echo Error: PowerShell not found!
        echo Please install PowerShell 7+ from: https://github.com/PowerShell/PowerShell/releases
        exit /b 1
    )
)

REM Run PowerShell script
if defined DRY_RUN_FLAG (
    %PS_CMD% -ExecutionPolicy Bypass -File "%SCRIPT_DIR%update-version.ps1" -Version "%VERSION%" %DRY_RUN_FLAG%
) else (
    %PS_CMD% -ExecutionPolicy Bypass -File "%SCRIPT_DIR%update-version.ps1" -Version "%VERSION%"
)

exit /b %ERRORLEVEL%
