@echo off
REM Setup script for UltraSharpTools.Comm build environment
REM Downloads and installs cosmocc (Cosmopolitan C Compiler)

setlocal enabledelayedexpansion

set COSMO_VERSION=4.0.2
set COSMO_URL=https://github.com/jart/cosmopolitan/releases/download/%COSMO_VERSION%/cosmocc-%COSMO_VERSION%.zip
set INSTALL_DIR=%LOCALAPPDATA%\cosmocc
set BIN_DIR=%INSTALL_DIR%\bin

echo.
echo === UltraSharpTools.Comm Setup ===
echo.

REM Check if cosmocc is already in PATH
where cosmocc >nul 2>&1
if %errorlevel% equ 0 (
    echo [+] cosmocc already installed and in PATH
    goto :done
)

REM Check in install dir
if exist "%BIN_DIR%\cosmocc.exe" (
    echo [+] cosmocc found in %BIN_DIR%
    goto :addpath
)

echo [!] cosmocc not found, downloading...

REM Download
set ZIP_PATH=%TEMP%\cosmocc.zip
echo [*] Downloading cosmocc %COSMO_VERSION%...

REM Use PowerShell for download (works on all Windows versions)
powershell -Command "& {$ProgressPreference='SilentlyContinue'; Invoke-WebRequest -Uri '%COSMO_URL%' -OutFile '%ZIP_PATH%' -UseBasicParsing}"
if %errorlevel% neq 0 (
    echo [-] Download failed!
    exit /b 1
)

REM Extract
echo [*] Extracting to %INSTALL_DIR%...
if exist "%INSTALL_DIR%" rd /s /q "%INSTALL_DIR%"
powershell -Command "Expand-Archive -Path '%ZIP_PATH%' -DestinationPath '%INSTALL_DIR%' -Force"
del "%ZIP_PATH%"

REM Move files from subdirectory if needed
for /d %%D in ("%INSTALL_DIR%\cosmocc-*") do (
    xcopy "%%D\*" "%INSTALL_DIR%\" /e /y /q >nul
    rd /s /q "%%D"
)

echo [+] cosmocc installed to %INSTALL_DIR%

:addpath
REM Add to user PATH permanently
echo [*] Adding to user PATH...
for /f "tokens=2*" %%A in ('reg query "HKCU\Environment" /v PATH 2^>nul') do set USERPATH=%%B
echo %USERPATH% | find /i "%BIN_DIR%" >nul
if %errorlevel% neq 0 (
    setx PATH "%BIN_DIR%;%USERPATH%" >nul
    echo [+] Added to user PATH permanently
) else (
    echo [+] Already in user PATH
)

REM Refresh PATH for current session (combine User + Machine paths)
for /f "tokens=2*" %%A in ('reg query "HKCU\Environment" /v PATH 2^>nul') do set NEWUSERPATH=%%B
for /f "tokens=2*" %%A in ('reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v PATH 2^>nul') do set MACHINEPATH=%%B
set PATH=%NEWUSERPATH%;%MACHINEPATH%
echo [+] Refreshed PATH in current session

:done
echo.
echo [+] Setup complete!
echo.
echo Next steps:
echo   1. Restart terminal (or use this one)
echo   2. Run: build.cmd
echo.

REM Verify
if exist "%BIN_DIR%\cosmocc.exe" (
    echo Installed files:
    echo   cosmocc:  %BIN_DIR%\cosmocc.exe
    echo   cosmoc++: %BIN_DIR%\cosmoc++.exe
)

endlocal
