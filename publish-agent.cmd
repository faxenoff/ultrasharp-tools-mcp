@echo off
pwsh -NoProfile -ExecutionPolicy Bypass -File "%~dp0Dev.Scripts\publish-agent.ps1" %*
