#!/bin/bash
# Build UltrasharpTools in Debug configuration
# Usage: ./build-debug.sh [options]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Check for PowerShell
if command -v pwsh &> /dev/null; then
    exec pwsh -ExecutionPolicy Bypass -File "$SCRIPT_DIR/build-debug.ps1" "$@"
fi

echo "Error: PowerShell (pwsh) not found!"
echo ""
echo "Please install PowerShell 7+:"
echo "  Ubuntu/Debian: sudo apt install powershell"
echo "  macOS:         brew install powershell"
echo "  Or download:   https://github.com/PowerShell/PowerShell/releases"
exit 1
