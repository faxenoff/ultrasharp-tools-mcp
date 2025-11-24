@echo off
REM Build Droid+Indexer (Debug configuration)
REM For Release build use: build-droid-release.cmd

pushd "%~dp0"
pwsh -NoProfile -ExecutionPolicy Bypass -File "Dev.Scripts\build-hybrid.ps1" -Configuration Debug
popd
