@echo off
REM Build script for UltraSharpTools.Comm
REM Creates a single portable binary that runs on Windows/Linux/macOS
REM Requires: cosmocc (run setup.cmd) + Git Bash

setlocal enabledelayedexpansion

set SCRIPT_DIR=%~dp0
set SOURCE_FILE=%SCRIPT_DIR%comm.c
set OUTPUT_COM=%SCRIPT_DIR%UltraSharp-tools.com
set OUTPUT_DIR=%SCRIPT_DIR%..\Run.Publish\Droid
set COSMO_DIR=%LOCALAPPDATA%\cosmocc\bin

echo.
echo === UltraSharpTools.Comm Build ===
echo.

REM Handle arguments
if "%1"=="clean" goto :clean
if "%1"=="-clean" goto :clean
if "%1"=="--clean" goto :clean
goto :build

:clean
echo [*] Cleaning...
del /q "%SCRIPT_DIR%*.com" 2>nul
del /q "%SCRIPT_DIR%*.exe" 2>nul
del /q "%SCRIPT_DIR%*.o" 2>nul
echo [+] Cleaned
if "%2"=="" exit /b 0

:build
REM Check cosmocc
if not exist "%COSMO_DIR%\cosmocc" (
    echo [-] cosmocc not found at %COSMO_DIR%\cosmocc
    echo [-] Run setup.cmd first.
    exit /b 1
)
echo [*] Using cosmocc: %COSMO_DIR%\cosmocc

REM Find Git Bash
set GITBASH=
if exist "C:\Program Files\Git\bin\bash.exe" set GITBASH=C:\Program Files\Git\bin\bash.exe
if exist "C:\Program Files (x86)\Git\bin\bash.exe" set GITBASH=C:\Program Files (x86)\Git\bin\bash.exe

if "%GITBASH%"=="" (
    echo [-] Git Bash not found! Install Git for Windows.
    echo [-] Download: https://git-scm.com/download/win
    exit /b 1
)
echo [*] Using bash: %GITBASH%

REM Convert paths to Unix style
set UNIX_COSMO_DIR=%COSMO_DIR:\=/%
set UNIX_COSMO_DIR=/%UNIX_COSMO_DIR:~0,1%%UNIX_COSMO_DIR:~2%

set UNIX_SCRIPT_DIR=%SCRIPT_DIR:\=/%
set UNIX_SCRIPT_DIR=/%UNIX_SCRIPT_DIR:~0,1%%UNIX_SCRIPT_DIR:~2%

echo [*] Compiling comm.c...

REM Run cosmocc via Git Bash
"%GITBASH%" -c "export PATH='%UNIX_COSMO_DIR%':$PATH && cd '%UNIX_SCRIPT_DIR%' && cosmocc -Os -Wall -Wextra -o UltraSharp-tools.com comm.c"

if %errorlevel% neq 0 (
    echo [-] Build failed!
    exit /b 1
)

if not exist "%OUTPUT_COM%" (
    echo [-] Output file not created!
    exit /b 1
)

REM Get file size
for %%A in ("%OUTPUT_COM%") do set SIZE=%%~zA
set /a SIZE_KB=%SIZE%/1024
echo [+] Built: UltraSharp-tools.com (%SIZE_KB% KB)

REM Copy to output directory
if "%1"=="-nocopy" goto :summary
if "%1"=="--nocopy" goto :summary

echo [*] Copying to %OUTPUT_DIR%...

if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

copy /y "%OUTPUT_COM%" "%OUTPUT_DIR%\UltraSharp-tools.com" >nul
echo [+] Copied to: %OUTPUT_DIR%\UltraSharp-tools.com

:summary
echo.
echo === Build Summary ===
echo.
echo Output:     UltraSharp-tools.com
echo Size:       %SIZE_KB% KB
echo Platforms:  Windows x64, Linux x64, macOS x64/ARM64, FreeBSD, NetBSD, OpenBSD
echo.
echo Usage:
echo   Windows:  .\comm.com --help
echo   Linux:    ./comm.com --help
echo   macOS:    ./comm.com --help
echo.
echo [+] Build complete!

endlocal
