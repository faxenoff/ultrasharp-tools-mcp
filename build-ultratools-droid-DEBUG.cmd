@echo off
REM Build Droid in DEBUG configuration for detailed logging
REM This is a wrapper for Dev.Scripts\publish-mcp.ps1 with Debug configuration

REM Check if PowerShell Core (pwsh) is available
where pwsh >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0Dev.Scripts\publish-mcp.ps1" -Configuration Debug %*
) else (
    REM Fallback to Windows PowerShell
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Dev.Scripts\publish-mcp.ps1" -Configuration Debug %*
)
