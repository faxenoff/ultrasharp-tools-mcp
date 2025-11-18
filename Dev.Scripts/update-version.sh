#!/bin/bash
# Update version across all project files
# Usage: ./update-version.sh <version> [--dry-run]
# Example: ./update-version.sh 3.1.0
# Example: ./update-version.sh 3.1.0 --dry-run

set -e

# Get script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Check if version parameter is provided
if [ -z "$1" ]; then
    echo "Error: Version parameter is required"
    echo ""
    echo "Usage: $0 <version> [--dry-run]"
    echo "Example: $0 3.1.0"
    echo "Example: $0 3.1.0 --dry-run"
    exit 1
fi

VERSION="$1"
DRY_RUN_FLAG=""

# Check for --dry-run flag
if [ "$2" = "--dry-run" ] || [ "$2" = "-d" ]; then
    DRY_RUN_FLAG="-DryRun"
fi

# Run PowerShell script
if [ -n "$DRY_RUN_FLAG" ]; then
    pwsh -ExecutionPolicy Bypass -File "$SCRIPT_DIR/update-version.ps1" -Version "$VERSION" $DRY_RUN_FLAG
else
    pwsh -ExecutionPolicy Bypass -File "$SCRIPT_DIR/update-version.ps1" -Version "$VERSION"
fi

exit $?
