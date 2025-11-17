#!/usr/bin/env bash
# Interactive Embeddings Setup for UltrasharpTools MCP
# Automatically detects GPU and recommends optimal embedding provider

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

echo "========================================"
echo "UltrasharpTools MCP - Embeddings Setup"
echo "========================================"
echo ""

# Detect GPU
echo -e "${CYAN}🔍 Detecting GPU capabilities...${NC}"
echo ""

gpu_detected=false
gpu_name="Unknown"
compute_capability=0.0
recommended_provider="ollama"

if command -v nvidia-smi &> /dev/null; then
    gpu_output=$(nvidia-smi --query-gpu=name,compute_cap --format=csv,noheader,nounits 2>/dev/null || true)

    if [ -n "$gpu_output" ]; then
        gpu_detected=true
        gpu_name=$(echo "$gpu_output" | cut -d',' -f1 | tr -d ' ')
        compute_capability=$(echo "$gpu_output" | cut -d',' -f2 | tr -d ' ')

        echo -e "${GREEN}🎮 GPU Detected: $gpu_name${NC}"
        echo -e "   Compute Capability: ${GREEN}$compute_capability${NC}"
        echo ""

        if (( $(echo "$compute_capability >= 8.0" | bc -l) )); then
            recommended_provider="tei"
            echo -e "${GREEN}✅ RTX 30xx+ detected!${NC}"
            echo -e "   ${GREEN}TEI recommended (8192 tokens context)${NC}"
        elif (( $(echo "$compute_capability >= 7.0" | bc -l) )); then
            recommended_provider="ollama"
            echo -e "${YELLOW}⚠️  GTX/RTX 20xx detected${NC}"
            echo -e "   ${YELLOW}Ollama recommended (512 tokens context)${NC}"
            echo -e "   ${NC}(TEI requires RTX 30xx+ with Compute Capability 8.0+)${NC}"
        else
            recommended_provider="ollama"
            echo -e "${YELLOW}⚠️  Older GPU detected (CC $compute_capability)${NC}"
            echo -e "   ${YELLOW}Ollama recommended${NC}"
        fi
    fi
fi

if [ "$gpu_detected" = false ]; then
    echo -e "${YELLOW}⚠️  No NVIDIA GPU detected${NC}"
    echo -e "   nvidia-smi not available"
    echo ""
    echo -e "${CYAN}💡 Recommendation: Use Ollama (simple installation, no Docker required)${NC}"
fi

echo ""
echo -e "${CYAN}Available embedding providers:${NC}"
echo ""

echo -e "${NC}1) TEI (Text Embeddings Inference)${NC}"
if (( $(echo "$compute_capability >= 8.0" | bc -l) )); then
    echo -e "   ${GREEN}✅ RECOMMENDED for your GPU${NC}"
else
    echo -e "   ${YELLOW}⚠️  Requires RTX 30xx/40xx (Compute Capability 8.0+)${NC}"
    echo -e "   ${RED}❌ NOT COMPATIBLE with your GPU (CC $compute_capability)${NC}"
fi
echo "   ✅ 8192 tokens context (16x more than Ollama)"
echo "   🐳 Requires Docker"
echo "   📦 ~2 GB (image + model)"
echo ""

echo -e "${NC}2) Ollama${NC}"
if [ "$recommended_provider" = "ollama" ]; then
    echo -e "   ${GREEN}✅ RECOMMENDED for your system${NC}"
fi
echo "   ✅ Simple installation (no Docker)"
echo "   ⚠️  512 tokens context"
echo "   📦 ~200 MB"
echo ""

echo -e "${NC}3) Skip (use Memory provider)${NC}"
echo "   ⚠️  No ML embeddings (deterministic hash)"
echo "   ✅ No installation required"
echo ""

# Prompt user
read -p "Your choice [1-3]: " choice

case $choice in
    1)
        echo ""
        echo -e "${CYAN}[INFO] Selected: TEI (Text Embeddings Inference)${NC}"
        echo ""

        # Check GPU compatibility
        if (( $(echo "$compute_capability < 8.0" | bc -l) )) && [ "$gpu_detected" = true ]; then
            echo -e "${YELLOW}[WARNING] Your GPU (CC $compute_capability) may not be compatible with TEI${NC}"
            echo -e "${YELLOW}          TEI requires RTX 30xx+ (Compute Capability 8.0+)${NC}"
            echo ""
            read -p "Continue anyway? [y/N]: " continue_choice
            if [[ ! "$continue_choice" =~ ^[Yy]$ ]]; then
                echo -e "${YELLOW}[INFO] Installation cancelled${NC}"
                exit 0
            fi
        fi

        # Check Docker
        if ! command -v docker &> /dev/null; then
            echo -e "${RED}[ERROR] Docker not found!${NC}"
            echo ""
            echo -e "${YELLOW}TEI requires Docker. Please install Docker:${NC}"
            echo -e "  ${CYAN}macOS: https://docs.docker.com/desktop/install/mac-install/${NC}"
            echo -e "  ${CYAN}Linux: https://docs.docker.com/engine/install/${NC}"
            echo ""
            echo -e "${YELLOW}Or choose Ollama (option 2) when running this script again.${NC}"
            exit 1
        fi

        echo -e "${GREEN}[OK] Docker found${NC}"
        docker --version
        echo ""

        # Check NVIDIA Container Toolkit
        echo -e "${CYAN}[INFO] Checking Docker GPU support...${NC}"
        if docker run --rm --gpus all nvidia/cuda:12.0-base-ubuntu20.04 nvidia-smi >/dev/null 2>&1; then
            echo -e "${GREEN}[OK] Docker has GPU access (nvidia-container-toolkit configured)${NC}"
        else
            echo -e "${RED}[ERROR] Docker cannot access GPU!${NC}"
            echo ""
            echo -e "${YELLOW}You need to install nvidia-container-toolkit:${NC}"
            echo ""
            echo -e "${CYAN}Ubuntu/Debian:${NC}"
            echo "  curl -fsSL https://nvidia.github.io/libnvidia-container/gpgkey | sudo gpg --dearmor -o /usr/share/keyrings/nvidia-container-toolkit-keyring.gpg"
            echo "  curl -s -L https://nvidia.github.io/libnvidia-container/stable/deb/nvidia-container-toolkit.list | sed 's#deb https://#deb [signed-by=/usr/share/keyrings/nvidia-container-toolkit-keyring.gpg] https://#g' | sudo tee /etc/apt/sources.list.d/nvidia-container-toolkit.list"
            echo "  sudo apt-get update"
            echo "  sudo apt-get install -y nvidia-container-toolkit"
            echo "  sudo nvidia-ctk runtime configure --runtime=docker"
            echo "  sudo systemctl restart docker"
            echo ""
            echo -e "${YELLOW}Or choose Ollama (option 2) - it doesn't require Docker.${NC}"
            echo ""
            read -p "Continue anyway? (TEI will run on CPU only) [y/N]: " continue_choice
            if [[ ! "$continue_choice" =~ ^[Yy]$ ]]; then
                echo -e "${YELLOW}[INFO] Installation cancelled${NC}"
                exit 0
            fi
        fi
        echo ""

        # Run TEI setup
        if [ -f "./setup-tei.sh" ]; then
            echo -e "${CYAN}[INFO] Running TEI setup...${NC}"
            chmod +x ./setup-tei.sh
            ./setup-tei.sh
        else
            echo -e "${RED}[ERROR] setup-tei.sh not found!${NC}"
            echo -e "${YELLOW}Make sure you're in the SharpTools project root directory.${NC}"
            exit 1
        fi

        echo ""
        echo -e "${GREEN}================================================${NC}"
        echo -e "${GREEN}✅ TEI installed and configured!${NC}"
        echo ""
        echo -e "${CYAN}Container 'tei-server' running on http://127.0.0.1:8080${NC}"
        echo ""
        echo "Configuration (already set in appsettings.json):"
        echo "  Provider: tei"
        echo "  Model: ibm-granite/granite-embedding-english-r2"
        echo "  Context: 8192 tokens"
        echo ""
        echo "Management commands:"
        echo "  docker logs tei-server    # View logs"
        echo "  docker stop tei-server    # Stop container"
        echo "  docker start tei-server   # Start container"
        echo -e "${GREEN}================================================${NC}"
        ;;

    2)
        echo ""
        echo -e "${CYAN}[INFO] Selected: Ollama${NC}"
        echo ""

        # Check Ollama installation
        if ! command -v ollama &> /dev/null; then
            echo -e "${CYAN}[INFO] Ollama not found, installing...${NC}"
            echo ""

            # Detect OS
            if [[ "$OSTYPE" == "darwin"* ]]; then
                # macOS
                if command -v brew &> /dev/null; then
                    echo -e "${CYAN}[INFO] Installing Ollama via Homebrew...${NC}"
                    brew install ollama
                else
                    echo -e "${YELLOW}[WARNING] Homebrew not available${NC}"
                    echo ""
                    echo -e "${YELLOW}Please install Ollama manually:${NC}"
                    echo -e "  ${CYAN}https://ollama.com/download/mac${NC}"
                    exit 1
                fi
            else
                # Linux
                echo -e "${CYAN}[INFO] Installing Ollama via install script...${NC}"
                curl -fsSL https://ollama.com/install.sh | sh
            fi
        else
            echo -e "${GREEN}[OK] Ollama already installed${NC}"
        fi

        # Start Ollama service
        echo -e "${CYAN}[INFO] Starting Ollama service...${NC}"
        ollama serve > /dev/null 2>&1 &
        sleep 3

        # Pull granite-embedding model
        echo -e "${CYAN}[INFO] Downloading granite-embedding model (~150 MB)...${NC}"
        echo "       This may take a few minutes..."
        ollama pull granite-embedding

        if [ $? -eq 0 ]; then
            echo -e "${GREEN}[OK] Model downloaded successfully${NC}"
        else
            echo -e "${RED}[ERROR] Failed to download model${NC}"
            exit 1
        fi

        # Verify
        echo ""
        echo -e "${CYAN}[INFO] Verifying installation...${NC}"
        ollama list

        echo ""
        echo -e "${GREEN}================================================${NC}"
        echo -e "${GREEN}✅ Ollama installed and configured!${NC}"
        echo ""
        echo "To use Ollama, update appsettings.json:"
        echo ""
        echo -e "${YELLOW}  \"Embedding\": {${NC}"
        echo -e "${YELLOW}    \"Provider\": \"ollama\",${NC}"
        echo -e "${YELLOW}    \"Enabled\": true${NC}"
        echo -e "${YELLOW}  }${NC}"
        echo ""
        echo "Management commands:"
        echo "  ollama serve              # Start Ollama server"
        echo "  ollama list               # List installed models"
        echo "  ollama pull <model>       # Download model"
        echo -e "${GREEN}================================================${NC}"
        ;;

    3)
        echo ""
        echo -e "${CYAN}[INFO] Installation skipped${NC}"
        echo ""
        echo -e "${YELLOW}⚠️  Memory provider will be used (no ML embeddings)${NC}"
        echo ""
        echo "To configure in appsettings.json:"
        echo ""
        echo -e "${YELLOW}  \"Embedding\": {${NC}"
        echo -e "${YELLOW}    \"Provider\": \"memory\",${NC}"
        echo -e "${YELLOW}    \"Enabled\": true${NC}"
        echo -e "${YELLOW}  }${NC}"
        echo ""
        echo "Embeddings can be installed later by running:"
        echo -e "  ${CYAN}./setup-embeddings-interactive.sh${NC}"
        ;;

    *)
        echo ""
        echo -e "${RED}[ERROR] Invalid choice: $choice${NC}"
        echo -e "${YELLOW}Please choose 1, 2, or 3${NC}"
        exit 1
        ;;
esac

echo ""
echo -e "${GREEN}Done! Start UltrasharpTools MCP server:${NC}"
echo -e "  ${CYAN}dotnet run --project UltrasharpTools.MCPServer${NC}"
echo ""
