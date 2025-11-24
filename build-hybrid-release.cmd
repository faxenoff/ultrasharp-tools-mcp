@echo off
REM Build Droid+Indexer (Release configuration)

pushd "%~dp0"
pwsh -NoProfile -ExecutionPolicy Bypass -File "Dev.Scripts\build-hybrid.ps1" -Configuration Release
popd
