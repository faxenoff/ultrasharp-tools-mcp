# ========================================
# UltrasharpTools Overlord - .NET 10
# Multi-stage build with optimization
# Supports Debug/Release via BUILD_CONFIGURATION
# ========================================

ARG BUILD_CONFIGURATION=Release

# ========================================
# Stage 1: Build with .NET 10 SDK
# ========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
ARG BUILD_CONFIGURATION

WORKDIR /src

# Copy project files and restore dependencies
# NOTE: Build context should be project root, not this directory
COPY ["UltrasharpTools.Overlord/UltrasharpTools.Overlord.csproj", "UltrasharpTools.Overlord/"]
COPY ["UltrasharpTools.Tools/UltrasharpTools.Tools.csproj", "UltrasharpTools.Tools/"]
COPY ["UltrasharpTools.sln", "./"]

# Restore with runtime
RUN dotnet restore "UltrasharpTools.Overlord/UltrasharpTools.Overlord.csproj" \
    --runtime linux-x64

# Copy all source code
COPY . .

# Publish with optimizations
WORKDIR /src/UltrasharpTools.Overlord
RUN dotnet publish "UltrasharpTools.Overlord.csproj" \
    -c ${BUILD_CONFIGURATION} \
    -o /app/publish \
    --runtime linux-x64 \
    --self-contained false \
    --no-restore \
    /p:CI=true

# ========================================
# Stage 2: Runtime (ASP.NET distroless)
# ========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS final

# Install git in separate layer (only for non-chiseled variant)
# Note: Chiseled images are distroless, we'll need regular aspnet for git
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS final-with-git
RUN apt-get update && \
    apt-get install -y --no-install-recommends \
    git \
    curl \
    ca-certificates && \
    rm -rf /var/lib/apt/lists/* /var/cache/apt/archives/*

# ========================================
# Final stage: Copy app and configure
# ========================================
FROM final-with-git AS runtime

WORKDIR /app

# Environment variables for K8s optimization
ENV \
    # ASP.NET Core settings
    ASPNETCORE_URLS=http://+:3001 \
    ASPNETCORE_ENVIRONMENT=Production \
    # .NET Runtime optimization
    DOTNET_CLI_TELEMETRY_OPTOUT=true \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_USE_POLLING_FILE_WATCHER=true \
    TMPDIR=/tmp \
    # GC settings for long-running server
    DOTNET_gcServer=1 \
    DOTNET_GCConserveMemory=5 \
    # Globalization (enable for C# code analysis)
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false \
    # Diagnostics (keep enabled for debugging)
    DOTNET_EnableDiagnostics=1

# Copy published app from build stage
COPY --from=build /app/publish .

# Create directories for data, logs, cache with proper permissions
RUN mkdir -p /app/data /app/logs /app/.ultrasharp && \
    chmod -R 755 /app

# OCI labels and K8s labels for management
LABEL \
    org.opencontainers.image.title="UltrasharpTools Overlord" \
    org.opencontainers.image.description="Roslyn-powered C# code analysis server for team collaboration via Model Context Protocol. Supports semantic search, cross-project analysis, and 52 MCP tools." \
    org.opencontainers.image.authors="UltrasharpTools Team" \
    org.opencontainers.image.vendor="UltrasharpTools" \
    org.opencontainers.image.licenses="MIT" \
    org.opencontainers.image.url="https://github.com/faxenoff/ultrasharp-tools-mcp" \
    org.opencontainers.image.source="https://github.com/faxenoff/ultrasharp-tools-mcp" \
    org.opencontainers.image.documentation="https://github.com/faxenoff/ultrasharp-tools-mcp/blob/main/Run.Docs/OVERLORD_README.md" \
    version="3.6.5" \
    maintainer="UltrasharpTools Team" \
    app.kubernetes.io/name="ultrasharp-tools-server" \
    app.kubernetes.io/component="mcp-server" \
    app.kubernetes.io/part-of="ultrasharp-tools" \
    app.kubernetes.io/version="3.6.5"

# Expose port
EXPOSE 3001

# Health check (requires curl in image)
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:3001/health || exit 1

# Run as non-root user (K8s best practice)
RUN useradd -m -u 1001 appuser && \
    chown -R appuser:appuser /app
USER appuser

# Entry point
ENTRYPOINT ["dotnet", "UltrasharpTools.Overlord.dll"]
