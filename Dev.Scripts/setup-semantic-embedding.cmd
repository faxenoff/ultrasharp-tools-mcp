@echo off
REM Setup Semantic Embedding Configuration
REM Simple launcher for Windows users

echo ========================================
echo Semantic Embedding Configuration Setup
echo ========================================
echo.

REM Check if PowerShell Core (pwsh) is available
where pwsh >nul 2>&1
if %errorlevel% equ 0 (
    echo Using PowerShell Core...
    pwsh -ExecutionPolicy Bypass -File "%~dp0setup-semantic-embedding.ps1" %*
    goto :end
)

REM Fallback to Windows PowerShell
echo Using Windows PowerShell...
powershell -ExecutionPolicy Bypass -File "%~dp0setup-semantic-embedding.ps1" %*

:end
pause
