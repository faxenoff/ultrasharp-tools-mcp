@echo off
REM Build UltrasharpTools in Debug configuration
REM Usage: build-debug.cmd [options]
REM Options are passed to build-debug.ps1

REM Try PowerShell 7+ (pwsh) first
where pwsh >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    pwsh -ExecutionPolicy Bypass -File "%~dp0Dev.Scripts\build-debug.ps1" %*
    exit /b %ERRORLEVEL%
)

REM Fallback to Windows PowerShell 5.1
where powershell >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    powershell -ExecutionPolicy Bypass -File "%~dp0Dev.Scripts\build-debug.ps1" %*
    exit /b %ERRORLEVEL%
)

REM No PowerShell found
echo Error: PowerShell not found!
echo.
echo Please install one of:
echo   - PowerShell 7+ (recommended): https://github.com/PowerShell/PowerShell/releases
echo   - Or ensure Windows PowerShell 5.1 is available
exit /b 1
