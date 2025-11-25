#!/bin/bash
# TEI (Text Embeddings Inference) Setup Script
# Installs and configures HuggingFace TEI with GPU support
# Usage: ./setup-tei.sh [--model-id MODEL_ID] [--architecture ARCH]

set -e

# Parse arguments
model_id=""
architecture=""
while [[ $# -gt 0 ]]; do
    case $1 in
        --model-id)
            model_id="$2"
            shift 2
            ;;
        --architecture)
            architecture="$2"
            shift 2
            ;;
        *)
            shift
            ;;
    esac
done

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

echo "========================================"
echo "TEI (Text Embeddings Inference) Setup"
echo "========================================"
echo ""

# Configuration
container_name="tei-server"
# Use provided model or default (All-MiniLM - works on CPU and GPU)
model="${model_id:-sentence-transformers/all-MiniLM-L6-v2}"
port=8080
base_image="ghcr.io/huggingface/text-embeddings-inference"
version="1.8.3"

# Check if container already exists
if docker ps -a --filter "name=$container_name" --format "{{.Names}}" | grep -q "^$container_name$"; then
    echo -e "${YELLOW}[INFO] Container '$container_name' already exists${NC}"
    echo ""
    read -p "What to do? [1=Restart, 2=Remove and reinstall, 3=Cancel]: " action

    case $action in
        1)
            echo -e "${CYAN}[INFO] Restarting existing container...${NC}"
            docker restart $container_name

            if [ $? -eq 0 ]; then
                echo -e "${GREEN}[OK] Container restarted successfully${NC}"
                echo ""
                echo -e "${CYAN}TEI is now running on http://127.0.0.1:$port${NC}"
                exit 0
            else
                echo -e "${RED}[ERROR] Failed to restart container${NC}"
                exit 1
            fi
            ;;

        2)
            echo -e "${CYAN}[INFO] Removing existing container...${NC}"
            docker stop $container_name 2>/dev/null || true
            docker rm $container_name 2>/dev/null || true
            echo -e "${GREEN}[OK] Container removed${NC}"
            ;;

        3)
            echo -e "${YELLOW}[INFO] Installation cancelled${NC}"
            exit 0
            ;;

        *)
            echo -e "${RED}[ERROR] Invalid choice${NC}"
            exit 1
            ;;
    esac
fi

# Architecture selection - use parameter if provided, otherwise ask
use_gpu=true
arch_name=""
image_tag=""

if [ -n "$architecture" ]; then
    # Use provided architecture
    echo -e "${CYAN}[INFO] Using architecture: $architecture${NC}"

    case "$architecture" in
        cpu)
            image_tag="${base_image}:cpu-${version}"
            arch_name="CPU"
            use_gpu=false
            ;;
        turing)
            image_tag="${base_image}:turing-${version}"
            arch_name="Turing (RTX 2000/T4)"
            ;;
        ampere-80)
            image_tag="${base_image}:${version}"
            arch_name="Ampere A100/A30"
            ;;
        ampere-86)
            image_tag="${base_image}:86-${version}"
            arch_name="Ampere A10/A40"
            ;;
        ada)
            image_tag="${base_image}:89-${version}"
            arch_name="Ada Lovelace (RTX 4000)"
            ;;
        hopper)
            image_tag="${base_image}:hopper-${version}"
            arch_name="Hopper (H100)"
            ;;
        blackwell)
            # Blackwell not supported by official TEI - fallback to CPU
            image_tag="${base_image}:cpu-${version}"
            arch_name="CPU (Blackwell not supported)"
            use_gpu=false
            echo ""
            echo -e "${YELLOW}[WARNING] Blackwell GPU not supported by official TEI. Using CPU mode.${NC}"
            echo -e "          Consider using 'blackwell-patch' architecture or Ollama for RTX 50xx GPUs."
            ;;
        blackwell-patch)
            # Alternative TEI with Blackwell patch from hotchpotch
            image_tag="hotchpotch/tei-blackwell-testing:latest"
            arch_name="Blackwell (RTX 5000) - patched TEI"
            use_gpu=true
            echo ""
            echo -e "${CYAN}[INFO] Using alternative TEI with Blackwell patch (hotchpotch/tei-blackwell-testing)${NC}"
            echo -e "       This is a community build, not official HuggingFace release."
            ;;
        *)
            echo -e "${YELLOW}[WARNING] Unknown architecture '$architecture', using default (Ampere)${NC}"
            image_tag="${base_image}:${version}"
            arch_name="Ampere A100/A30 (default)"
            ;;
    esac
else
    # Interactive architecture selection
    echo "========================================"
    echo "GPU Architecture Selection"
    echo "========================================"
    echo ""
    echo "Select your GPU architecture:"
    echo "  1) CPU only (slowest, but works everywhere)"
    echo "  2) NVIDIA Turing (RTX 2000 series, T4)"
    echo "  3) NVIDIA Ampere A100/A30 (default, best compatibility)"
    echo "  4) NVIDIA Ampere A10/A40"
    echo "  5) NVIDIA Ada Lovelace (RTX 4000 series)"
    echo "  6) NVIDIA Hopper (H100)"
    echo "  7) NVIDIA Blackwell (RTX 5000 series) - NOT SUPPORTED, uses CPU"
    echo "  8) NVIDIA Blackwell with TEI patch (RTX 5000 series) - EXPERIMENTAL"
    echo ""
    read -p "Enter choice [1-8] (default: 3): " arch_choice
    arch_choice=${arch_choice:-3}

    case $arch_choice in
        1)
            image_tag="${base_image}:cpu-${version}"
            arch_name="CPU"
            use_gpu=false
            ;;
        2)
            image_tag="${base_image}:turing-${version}"
            arch_name="Turing (RTX 2000/T4)"
            ;;
        3)
            image_tag="${base_image}:${version}"
            arch_name="Ampere A100/A30"
            ;;
        4)
            image_tag="${base_image}:86-${version}"
            arch_name="Ampere A10/A40"
            ;;
        5)
            image_tag="${base_image}:89-${version}"
            arch_name="Ada Lovelace (RTX 4000)"
            ;;
        6)
            image_tag="${base_image}:hopper-${version}"
            arch_name="Hopper (H100)"
            ;;
        7)
            image_tag="${base_image}:cpu-${version}"
            arch_name="CPU (Blackwell not supported)"
            use_gpu=false
            echo ""
            echo -e "${YELLOW}[WARNING] Blackwell not supported by official TEI. Using CPU mode.${NC}"
            echo -e "          Consider option 8 (TEI with Blackwell patch) or Ollama for RTX 50xx GPUs."
            ;;
        8)
            image_tag="hotchpotch/tei-blackwell-testing:latest"
            arch_name="Blackwell (RTX 5000) - patched TEI"
            use_gpu=true
            echo ""
            echo -e "${CYAN}[INFO] Using alternative TEI with Blackwell patch (hotchpotch/tei-blackwell-testing)${NC}"
            echo -e "       This is a community build, not official HuggingFace release."
            ;;
        *)
            echo -e "${RED}[ERROR] Invalid choice${NC}"
            exit 1
            ;;
    esac
fi

echo ""
echo -e "${CYAN}[INFO] Selected: $arch_name${NC}"
echo ""

# Pull TEI image
echo -e "${CYAN}[INFO] Pulling TEI Docker image...${NC}"
echo -e "       Image: $image_tag"
echo -e "       This may take a few minutes (first time only)..."
echo ""

docker pull $image_tag

if [ $? -ne 0 ]; then
    echo -e "${RED}[ERROR] Failed to pull TEI image${NC}"
    exit 1
fi

echo -e "${GREEN}[OK] Image downloaded${NC}"
echo ""

# Create and run container
echo -e "${CYAN}[INFO] Creating TEI container...${NC}"
echo -e "       Container name: $container_name"
echo -e "       Model: $model"
echo -e "       Port: $port"
echo -e "       Architecture: $arch_name"
echo ""

# Prepare docker command
docker_cmd="docker run -d --name $container_name"

if [ "$use_gpu" = true ]; then
    echo -e "${CYAN}[INFO] Starting with GPU support...${NC}"
    docker_cmd="$docker_cmd --gpus all"
else
    echo -e "${CYAN}[INFO] Starting in CPU mode...${NC}"
fi

docker_cmd="$docker_cmd -p $port:80 -v $HOME/.cache/huggingface:/data --restart unless-stopped $image_tag --model-id $model --max-concurrent-requests 512"

if eval $docker_cmd; then
    if [ "$use_gpu" = true ]; then
        echo -e "${GREEN}[OK] TEI started with GPU acceleration${NC}"
    else
        echo -e "${GREEN}[OK] TEI started in CPU mode${NC}"
    fi
else
    echo -e "${RED}[ERROR] Failed to start TEI container${NC}"
    if [ "$use_gpu" = true ]; then
        echo ""
        echo -e "${YELLOW}[HINT] If GPU failed, try running the script again and select option 1 (CPU)${NC}"
    fi
    exit 1
fi

echo ""

# Wait for container to be ready
echo -e "${CYAN}[INFO] Waiting for TEI to initialize...${NC}"
max_wait_seconds=120
waited_seconds=0

while [ $waited_seconds -lt $max_wait_seconds ]; do
    sleep 2
    waited_seconds=$((waited_seconds + 2))

    if curl -s -f http://127.0.0.1:$port/health >/dev/null 2>&1; then
        echo -e "\n${GREEN}[OK] TEI is ready!${NC}"
        break
    else
        echo -n "."
    fi
done

if [ $waited_seconds -ge $max_wait_seconds ]; then
    echo ""
    echo -e "${YELLOW}[WARNING] TEI health check timed out after $max_wait_seconds seconds${NC}"
    echo -e "          Container might still be initializing. Check logs:"
    echo -e "${CYAN}          docker logs $container_name${NC}"
fi

echo ""
echo "========================================"
echo -e "${GREEN}✅ TEI Setup Complete${NC}"
echo "========================================"
echo ""
echo -e "${CYAN}Container:${NC} $container_name"
echo -e "${CYAN}Endpoint:${NC}  http://127.0.0.1:$port"
echo -e "${CYAN}Model:${NC}     $model"
echo ""
echo "Management commands:"
echo "  docker logs $container_name        # View logs"
echo "  docker stop $container_name        # Stop container"
echo "  docker start $container_name       # Start container"
echo "  docker restart $container_name     # Restart container"
echo ""
