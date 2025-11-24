@echo off
REM Build all UltraSharpTools components (Comm + Droid + Indexer)
REM For Release build use: build-all-release.cmd

pushd "%~dp0"
pwsh -NoProfile -ExecutionPolicy Bypass -File "Dev.Scripts\build-all.ps1" -Configuration Debug
popd
