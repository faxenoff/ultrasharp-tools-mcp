@echo off
REM Build Droid for Claude Code / MCP clients
REM This is a convenient wrapper for Dev.Scripts\publish-mcp.ps1

REM Check if PowerShell Core (pwsh) is available
where pwsh >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0Dev.Scripts\publish-mcp.ps1" %*
) else (
    REM Fallback to Windows PowerShell
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Dev.Scripts\publish-mcp.ps1" %*
)
