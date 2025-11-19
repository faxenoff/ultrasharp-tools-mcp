#!/usr/bin/env bash
# Setup Semantic Embedding Configuration
# Simple launcher for Linux/macOS users

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "========================================"
echo "Semantic Embedding Configuration Setup"
echo "========================================"
echo ""

# Check if pwsh is available
if command -v pwsh &> /dev/null; then
    echo "Using PowerShell Core..."
    pwsh -ExecutionPolicy Bypass -File "$SCRIPT_DIR/Scripts/setup-semantic-embedding.ps1" "$@"
else
    echo "PowerShell Core (pwsh) is required but not installed."
    echo "Please install PowerShell Core: https://aka.ms/powershell"
    exit 1
fi
