#!/usr/bin/env bash
#
# build-releases.sh - Build UltrasharpTools for multiple platforms
#
# Description:
#   Builds all components for Linux and macOS with Native AOT optimization.
#   Run this script directly on Linux/macOS for optimal builds.
#
#   Build strategy:
#   - Linux: Native AOT for VectorDB/Comm (~15MB + ~5MB)
#   - macOS: Native AOT for VectorDB/Comm (requires Xcode CLT)
#   - Droid: Always self-contained (uses Roslyn, not AOT-compatible)
#
# Prerequisites (auto-installed if missing):
#   Linux (Ubuntu/Debian): dotnet-sdk-9.0, clang, build-essential, zlib1g-dev
#   macOS: dotnet-sdk, Xcode Command Line Tools
#
# Usage:
#   ./build-releases.sh
#   ./build-releases.sh --version 3.0.8
#   ./build-releases.sh --skip-aot        # Use self-contained instead of AOT
#   ./build-releases.sh --current-only    # Build only for current platform
#
set -euo pipefail

# ============================================================================
# Configuration
# ============================================================================

VERSION=""
CONFIGURATION="Release"
OUTPUT_DIR="Run.Publish/Releases"
SKIP_AOT=false
CURRENT_ONLY=false

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# ============================================================================
# Utility Functions
# ============================================================================

print_header() {
    echo -e "\n${CYAN}=== $1 ===${NC}"
}

print_success() {
    echo -e "${GREEN}[OK]${NC} $1"
}

print_info() {
    echo -e "    ${YELLOW}$1${NC}"
}

print_warn() {
    echo -e "${YELLOW}[!]${NC} $1"
}

print_error() {
    echo -e "${RED}[X]${NC} $1"
}

# Find project root (directory with .sln)
find_project_root() {
    local current="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

    while [[ "$current" != "/" ]]; do
        if ls "$current"/*.sln &>/dev/null; then
            echo "$current"
            return 0
        fi
        current="$(dirname "$current")"
    done

    echo "Error: Could not find project root (directory with .sln file)" >&2
    exit 1
}

# Detect OS
detect_os() {
    case "$(uname -s)" in
        Linux*)  echo "linux" ;;
        Darwin*) echo "macos" ;;
        *)       echo "unknown" ;;
    esac
}

# Detect architecture
detect_arch() {
    case "$(uname -m)" in
        x86_64)  echo "x64" ;;
        aarch64) echo "arm64" ;;
        arm64)   echo "arm64" ;;
        *)       echo "x64" ;;
    esac
}

# ============================================================================
# Prerequisites Check and Installation
# ============================================================================

check_dotnet() {
    if command -v dotnet &>/dev/null; then
        local version=$(dotnet --version 2>/dev/null || echo "0")
        local major=$(echo "$version" | cut -d. -f1)
        if [[ "$major" -ge 9 ]]; then
            print_success "Found .NET SDK $version"
            return 0
        else
            print_warn ".NET SDK $version is too old (need 9.0+)"
            return 1
        fi
    else
        print_warn ".NET SDK not found"
        return 1
    fi
}

check_aot_prerequisites() {
    local os=$(detect_os)
    local all_present=true

    print_info "Checking NativeAOT prerequisites..."

    if [[ "$os" == "linux" ]]; then
        # Check clang
        if command -v clang &>/dev/null; then
            print_success "  clang - OK"
        else
            print_warn "  clang - missing"
            all_present=false
        fi

        # Check zlib
        if dpkg -l zlib1g-dev &>/dev/null 2>&1 || rpm -q zlib-devel &>/dev/null 2>&1; then
            print_success "  zlib - OK"
        else
            print_warn "  zlib - missing"
            all_present=false
        fi
    elif [[ "$os" == "macos" ]]; then
        # Check Xcode CLT
        if xcode-select -p &>/dev/null; then
            print_success "  Xcode CLT - OK"
        else
            print_warn "  Xcode CLT - missing"
            all_present=false
        fi
    fi

    $all_present
}

install_prerequisites() {
    local os=$(detect_os)

    print_header "Installing prerequisites"

    if [[ "$os" == "linux" ]]; then
        # Detect package manager
        if command -v apt-get &>/dev/null; then
            print_info "Using apt-get..."

            # Check if Microsoft repo is configured
            if [[ ! -f /etc/apt/sources.list.d/microsoft-prod.list ]]; then
                print_info "Adding Microsoft package repository..."

                local ubuntu_version=$(lsb_release -rs 2>/dev/null || echo "22.04")
                wget -q "https://packages.microsoft.com/config/ubuntu/${ubuntu_version}/packages-microsoft-prod.deb" -O /tmp/packages-microsoft-prod.deb
                sudo dpkg -i /tmp/packages-microsoft-prod.deb
                rm /tmp/packages-microsoft-prod.deb
            fi

            sudo apt-get update -qq
            sudo apt-get install -y dotnet-sdk-9.0 clang build-essential zlib1g-dev

        elif command -v dnf &>/dev/null; then
            print_info "Using dnf..."
            sudo dnf install -y dotnet-sdk-9.0 clang zlib-devel

        elif command -v yum &>/dev/null; then
            print_info "Using yum..."
            sudo yum install -y dotnet-sdk-9.0 clang zlib-devel

        else
            print_error "Unsupported package manager. Please install manually:"
            print_info "  - .NET SDK 9.0+"
            print_info "  - clang"
            print_info "  - zlib development headers"
            exit 1
        fi

    elif [[ "$os" == "macos" ]]; then
        # Install Xcode CLT if missing
        if ! xcode-select -p &>/dev/null; then
            print_info "Installing Xcode Command Line Tools..."
            xcode-select --install
            # Wait for installation
            until xcode-select -p &>/dev/null; do
                sleep 5
            done
        fi

        # Install .NET via Homebrew if available
        if command -v brew &>/dev/null; then
            if ! check_dotnet; then
                print_info "Installing .NET SDK via Homebrew..."
                brew install dotnet-sdk
            fi
        else
            print_warn "Homebrew not found. Please install .NET SDK manually:"
            print_info "  https://dotnet.microsoft.com/download"
            exit 1
        fi
    fi

    print_success "Prerequisites installed"
}

ensure_prerequisites() {
    local dotnet_ok=true
    local aot_ok=true

    check_dotnet || dotnet_ok=false
    check_aot_prerequisites || aot_ok=false

    if $dotnet_ok && $aot_ok; then
        return 0
    fi

    print_info "Installing missing prerequisites (may prompt for sudo password)..."
    install_prerequisites

    # Re-check
    check_dotnet || { print_error "Failed to install .NET SDK"; exit 1; }
    check_aot_prerequisites || { print_error "Failed to install AOT prerequisites"; exit 1; }
}

# ============================================================================
# Build Functions
# ============================================================================

build_component_aot() {
    local project="$1"
    local rid="$2"
    local output="$3"

    print_info "Building $(basename "$project" .csproj) (Native AOT)..."

    dotnet publish "$project" \
        -c Release \
        -r "$rid" \
        -o "$output" \
        /p:PublishAot=true \
        --verbosity quiet
}

build_component_selfcontained() {
    local project="$1"
    local rid="$2"
    local output="$3"

    print_info "Building $(basename "$project" .csproj) (self-contained)..."

    dotnet publish "$project" \
        -c Release \
        -r "$rid" \
        --self-contained true \
        -o "$output" \
        /p:PublishSingleFile=true \
        /p:EnableCompressionInSingleFile=true \
        --verbosity quiet
}

build_platform() {
    local rid="$1"
    local os="$2"
    local arch="$3"
    local archive_type="$4"

    print_header "Building for $rid ($os / $arch)"

    local platform_output="$PROJECT_ROOT/Run.Publish.$rid"
    local droid_output="$platform_output/Droid"
    local vectordb_output="$platform_output/_temp_vectordb"
    local comm_output="$platform_output/_temp_comm"

    # Clean output directory
    rm -rf "$platform_output"
    mkdir -p "$droid_output" "$vectordb_output" "$comm_output"

    # Determine build mode
    local use_aot=true
    if $SKIP_AOT; then
        use_aot=false
    fi

    # Build VectorDB
    if $use_aot; then
        build_component_aot \
            "$PROJECT_ROOT/UltraSharpTools.VectorDB/UltraSharpTools.VectorDB.csproj" \
            "$rid" \
            "$vectordb_output"
    else
        build_component_selfcontained \
            "$PROJECT_ROOT/UltraSharpTools.VectorDB/UltraSharpTools.VectorDB.csproj" \
            "$rid" \
            "$vectordb_output"
    fi

    # Build Comm
    if $use_aot; then
        build_component_aot \
            "$PROJECT_ROOT/UltraSharpTools.Comm/UltraSharpTools.Comm.csproj" \
            "$rid" \
            "$comm_output"
    else
        build_component_selfcontained \
            "$PROJECT_ROOT/UltraSharpTools.Comm/UltraSharpTools.Comm.csproj" \
            "$rid" \
            "$comm_output"
    fi

    # Build Droid (always self-contained - uses Roslyn)
    build_component_selfcontained \
        "$PROJECT_ROOT/UltrasharpTools.Droid/UltrasharpTools.Droid.csproj" \
        "$rid" \
        "$droid_output"

    # Copy BuildHost-netcore if exists
    local buildhost_source="$PROJECT_ROOT/UltrasharpTools.Droid/bin/Release/net10.0/$rid/BuildHost-netcore"
    if [[ ! -d "$buildhost_source" ]]; then
        buildhost_source="$PROJECT_ROOT/UltrasharpTools.Droid/bin/Release/net10.0/BuildHost-netcore"
    fi
    if [[ -d "$buildhost_source" ]]; then
        cp -r "$buildhost_source" "$droid_output/"
        print_info "Copied BuildHost-netcore"
    fi

    # Copy VectorDB and Comm to Droid folder
    cp -r "$vectordb_output"/* "$droid_output/"
    cp -r "$comm_output"/* "$droid_output/"

    # Cleanup temp folders
    rm -rf "$vectordb_output" "$comm_output"

    # Create archive
    local archive_name="ultrasharp-tools-v$VERSION-$os-$arch"
    print_info "Creating archive: $archive_name.$archive_type"

    if [[ "$archive_type" == "tar.gz" ]]; then
        tar -czf "$RELEASES_DIR/$archive_name.tar.gz" -C "$droid_output" .
    else
        (cd "$droid_output" && zip -rq "$RELEASES_DIR/$archive_name.zip" .)
    fi

    local file_size=$(du -h "$RELEASES_DIR/$archive_name.$archive_type" | cut -f1)
    print_success "Created: $archive_name.$archive_type ($file_size)"

    # Cleanup platform output
    rm -rf "$platform_output"
}

# ============================================================================
# Main
# ============================================================================

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --version)
            VERSION="$2"
            shift 2
            ;;
        --skip-aot)
            SKIP_AOT=true
            shift
            ;;
        --current-only)
            CURRENT_ONLY=true
            shift
            ;;
        --output)
            OUTPUT_DIR="$2"
            shift 2
            ;;
        -h|--help)
            echo "Usage: $0 [options]"
            echo ""
            echo "Options:"
            echo "  --version VERSION    Set version (default: auto-detect)"
            echo "  --skip-aot           Use self-contained instead of Native AOT"
            echo "  --current-only       Build only for current platform"
            echo "  --output DIR         Output directory (default: Run.Publish/Releases)"
            echo "  -h, --help           Show this help"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Find project root
PROJECT_ROOT=$(find_project_root)
cd "$PROJECT_ROOT"

print_header "UltrasharpTools Multi-Platform Release Builder"

# Detect current platform
CURRENT_OS=$(detect_os)
CURRENT_ARCH=$(detect_arch)
print_info "Running on: $CURRENT_OS-$CURRENT_ARCH"

# Check prerequisites
ensure_prerequisites

# Auto-detect version
if [[ -z "$VERSION" ]]; then
    VERSION=$(grep -oP '(?<=<Version>)[^<]+' UltrasharpTools.Droid/UltrasharpTools.Droid.csproj 2>/dev/null || echo "")
    if [[ -z "$VERSION" ]]; then
        VERSION=$(grep -oP '(?<=<VersionPrefix>)[^<]+' UltrasharpTools.Droid/UltrasharpTools.Droid.csproj 2>/dev/null || echo "3.0.8")
    fi
    print_info "Auto-detected version: $VERSION"
fi

# Create releases directory
RELEASES_DIR="$PROJECT_ROOT/$OUTPUT_DIR"
rm -rf "$RELEASES_DIR"
mkdir -p "$RELEASES_DIR"
print_success "Created: $RELEASES_DIR"

# Define platforms
if $CURRENT_ONLY; then
    # Build only for current platform
    if [[ "$CURRENT_OS" == "linux" ]]; then
        PLATFORMS=("linux-$CURRENT_ARCH:linux:$CURRENT_ARCH:tar.gz")
    elif [[ "$CURRENT_OS" == "macos" ]]; then
        PLATFORMS=("osx-$CURRENT_ARCH:macos:$CURRENT_ARCH:tar.gz")
    fi
else
    # Build for all platforms that can be built natively
    if [[ "$CURRENT_OS" == "linux" ]]; then
        PLATFORMS=(
            "linux-x64:linux:x64:tar.gz"
            "linux-arm64:linux:arm64:tar.gz"
        )
    elif [[ "$CURRENT_OS" == "macos" ]]; then
        PLATFORMS=(
            "osx-x64:macos:x64:tar.gz"
            "osx-arm64:macos:arm64:tar.gz"
        )
    fi
fi

# Build each platform
SUCCESS_COUNT=0
FAIL_COUNT=0

for platform in "${PLATFORMS[@]}"; do
    IFS=':' read -r rid os arch archive_type <<< "$platform"

    if build_platform "$rid" "$os" "$arch" "$archive_type"; then
        ((SUCCESS_COUNT++))
    else
        print_error "Failed to build $rid"
        ((FAIL_COUNT++))
    fi
done

# Summary
print_header "Build Summary"
echo "Total platforms: ${#PLATFORMS[@]}"
print_success "Successful: $SUCCESS_COUNT"
if [[ $FAIL_COUNT -gt 0 ]]; then
    print_error "Failed: $FAIL_COUNT"
fi

echo ""
echo -e "${CYAN}Release archives location:${NC}"
echo "  $RELEASES_DIR"

if [[ $SUCCESS_COUNT -gt 0 ]]; then
    echo ""
    echo -e "${CYAN}Generated files:${NC}"
    ls -lh "$RELEASES_DIR" | tail -n +2 | awk '{print "  " $NF " - " $5}'
fi

if [[ $FAIL_COUNT -gt 0 ]]; then
    exit 1
fi

print_success "All builds completed successfully!"
