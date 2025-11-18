@echo off
REM Build multi-platform releases
REM Usage: build-releases.cmd [version]

pwsh -ExecutionPolicy Bypass -File "%~dp0build-releases.ps1" %*
