#!/usr/bin/env bash
# Build Droid for multiple platforms and create release archives
# Usage: ./build-releases.sh [version]

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Find project root
find_project_root() {
    local current="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
    while [[ "$current" != "/" ]]; do
        if ls "$current"/*.sln >/dev/null 2>&1; then
            echo "$current"
            return 0
        fi
        current="$(dirname "$current")"
    done
    echo "Error: Could not find project root (directory with .sln file)" >&2
    exit 1
}

PROJECT_ROOT=$(find_project_root)
cd "$PROJECT_ROOT"

VERSION="${1:-}"
CONFIGURATION="Release"
OUTPUT_DIR="Run.Publish/Releases"

echo -e "${CYAN}=== UltrasharpTools Multi-Platform Release Builder ===${NC}\n"

# Auto-detect version
if [[ -z "$VERSION" ]]; then
    if [[ -f "UltrasharpTools.Droid/UltrasharpTools.Droid.csproj" ]]; then
        VERSION=$(grep -oP '(?<=<Version>)[^<]+' UltrasharpTools.Droid/UltrasharpTools.Droid.csproj || echo "")
        if [[ -z "$VERSION" ]]; then
            VERSION=$(grep -oP '(?<=<VersionPrefix>)[^<]+' UltrasharpTools.Droid/UltrasharpTools.Droid.csproj || echo "")
        fi
    fi
    VERSION="${VERSION:-3.0.0}"
    echo -e "${YELLOW}→ Auto-detected version: $VERSION${NC}"
fi

# Create output directory
RELEASES_DIR="$PROJECT_ROOT/$OUTPUT_DIR"
if [[ -d "$RELEASES_DIR" ]]; then
    echo -e "${YELLOW}→ Cleaning existing releases directory...${NC}"
    rm -rf "$RELEASES_DIR"
fi
mkdir -p "$RELEASES_DIR"
echo -e "${GREEN}✓ Created: $RELEASES_DIR${NC}\n"

# Define platforms
declare -a PLATFORMS=(
    "win-x64:windows:x64:zip"
    "win-arm64:windows:arm64:zip"
    "osx-x64:macos:x64:tar.gz"
    "osx-arm64:macos:arm64:tar.gz"
    "linux-x64:linux:x64:tar.gz"
    "linux-arm64:linux:arm64:tar.gz"
)

SUCCESS_COUNT=0
FAIL_COUNT=0

for platform_spec in "${PLATFORMS[@]}"; do
    IFS=':' read -r rid os arch archive_type <<< "$platform_spec"

    echo -e "${CYAN}=== Building for $rid ($os / $arch) ===${NC}"

    if pwsh "$PROJECT_ROOT/Dev.Scripts/publish-mcp.ps1" -Configuration "$CONFIGURATION" -Runtime "$rid"; then
        BUILD_OUTPUT="$PROJECT_ROOT/Run.Publish/Droid"

        if [[ ! -d "$BUILD_OUTPUT" ]]; then
            echo -e "${RED}✗ Build output not found at: $BUILD_OUTPUT${NC}"
            ((FAIL_COUNT++))
            continue
        fi

        ARCHIVE_NAME="ultrasharp-tools-droid-v$VERSION-$os-$arch"
        echo -e "${YELLOW}→ Creating archive: $ARCHIVE_NAME.$archive_type${NC}"

        if [[ "$archive_type" == "zip" ]]; then
            # Windows: ZIP
            (cd "$BUILD_OUTPUT" && zip -r "$RELEASES_DIR/$ARCHIVE_NAME.zip" . >/dev/null)
            FILE_SIZE=$(du -h "$RELEASES_DIR/$ARCHIVE_NAME.zip" | cut -f1)
            echo -e "${GREEN}✓ Created: $ARCHIVE_NAME.zip ($FILE_SIZE)${NC}"
        else
            # Linux/macOS: tar.gz
            (cd "$BUILD_OUTPUT" && tar -czf "$RELEASES_DIR/$ARCHIVE_NAME.tar.gz" *)
            FILE_SIZE=$(du -h "$RELEASES_DIR/$ARCHIVE_NAME.tar.gz" | cut -f1)
            echo -e "${GREEN}✓ Created: $ARCHIVE_NAME.tar.gz ($FILE_SIZE)${NC}"
        fi

        ((SUCCESS_COUNT++))
    else
        echo -e "${RED}✗ Failed to build $rid${NC}"
        ((FAIL_COUNT++))
    fi
    echo ""
done

# Summary
echo -e "${CYAN}=== Build Summary ===${NC}"
echo "Total platforms: ${#PLATFORMS[@]}"
echo -e "${GREEN}✓ Successful: $SUCCESS_COUNT${NC}"
[[ $FAIL_COUNT -gt 0 ]] && echo -e "${RED}✗ Failed: $FAIL_COUNT${NC}"

echo -e "\n${CYAN}Release archives location:${NC}"
echo "  $RELEASES_DIR"

if [[ $SUCCESS_COUNT -gt 0 ]]; then
    echo -e "\n${CYAN}Generated files:${NC}"
    ls -lh "$RELEASES_DIR" | tail -n +2 | awk '{print "  " $9 " - " $5}'

    echo -e "\n${YELLOW}📦 Next steps for GitHub Release:${NC}"
    echo "1. Create a new release on GitHub:"
    echo "   gh release create v$VERSION --title \"Release v$VERSION\" --notes \"Release notes here\""
    echo ""
    echo "2. Upload release archives:"
    echo "   gh release upload v$VERSION $RELEASES_DIR/*"
    echo ""
    echo "   OR manually at: https://github.com/your-repo/releases/new"
fi

[[ $FAIL_COUNT -gt 0 ]] && exit 1

echo -e "\n${GREEN}✓ All builds completed successfully!${NC}"
