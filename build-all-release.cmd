@echo off
REM Build all UltraSharpTools components in Release mode (Comm + Droid + Indexer)

pushd "%~dp0"
pwsh -NoProfile -ExecutionPolicy Bypass -File "Dev.Scripts\build-all.ps1" -Configuration Release
popd
