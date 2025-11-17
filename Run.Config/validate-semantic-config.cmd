@echo off
REM Validate Semantic Embedding Configuration
REM Simple launcher for Windows users

REM Check if PowerShell Core (pwsh) is available
where pwsh >nul 2>&1
if %errorlevel% equ 0 (
    pwsh -ExecutionPolicy Bypass -File "%~dp0Dev.Scripts\validate-semantic-config.ps1" %*
    goto :end
)

REM Fallback to Windows PowerShell
powershell -ExecutionPolicy Bypass -File "%~dp0Dev.Scripts\validate-semantic-config.ps1" %*

:end
if "%1"=="" pause
