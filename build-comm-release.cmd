@echo off
REM Build UltraSharpTools.Comm (Release configuration)

pushd "%~dp0"
pwsh -NoProfile -ExecutionPolicy Bypass -File "Dev.Scripts\build-comm.ps1" -Configuration Release
popd
