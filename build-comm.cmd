@echo off
REM Build UltraSharpTools.Comm (Debug configuration)
REM For Release build use: build-comm-release.cmd

pushd "%~dp0"
pwsh -NoProfile -ExecutionPolicy Bypass -File "Dev.Scripts\build-comm.ps1" -Configuration Debug
popd
