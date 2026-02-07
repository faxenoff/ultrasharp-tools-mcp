# UltrasharpTools Overlord - MCP Remote Server

> ⚠️ **Статус:** В активном тестировании. API может меняться.

**Roslyn-powered C# code analysis server для командной работы через Model Context Protocol.**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Docker](https://img.shields.io/badge/Docker-Ready-blue.svg)](https://github.com/faxenoff/ultrasharp-tools-mcp/pkgs/container/ultrasharp-tools-overlord)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)

---

## 🚀 Что это?

**UltrasharpTools Overlord** - это централизованный HTTP-сервер для анализа C# кода, который позволяет:

- 🤝 **Командная работа** - несколько разработчиков используют один semantic index
- 🔍 **Semantic Search** - поиск кода на естественном языке через embedding модели
- 🌐 **Cross-Project Analysis** - анализ дубликатов и связей между проектами
- 📊 **Centralized Vector Store** - единая база векторных представлений кода
- ⚡ **High Performance** - ReadyToRun compilation, optimized for production

---

## 📦 Quick Start

### Запуск через Docker

```bash
docker run -d \
  --name ultrasharp-overlord \
  -p 3001:3001 \
  -v /path/to/projects:/app/projects \
  -e GIT_AUTHOR_NAME="Your Name" \
  -e GIT_AUTHOR_EMAIL="your@email.com" \
  ghcr.io/faxenoff/ultrasharp-tools-overlord:latest
```

### Проверка работоспособности

```bash
curl http://localhost:3001/health
# Expected: {"status":"healthy"}
```

### Подключение к кластеру Kubernetes

```bash
kubectl apply -f https://raw.githubusercontent.com/faxenoff/ultrasharp-tools-mcp/main/kubernetes/deployment.yaml
```

---

## 🔧 Конфигурация

### Переменные окружения

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_URLS` | Server binding URL | `http://+:3001` |
| `ASPNETCORE_ENVIRONMENT` | Environment | `Production` |
| `GIT_AUTHOR_NAME` | Git commit author | `UltrasharpTools Bot` |
| `GIT_AUTHOR_EMAIL` | Git author email | `bot@example.com` |
| `LOG_LEVEL` | Logging level | `Information` |
| `BUILD_CONFIGURATION` | MSBuild config | `Debug` |

### Volumes

- **/app/projects/** - Mount ваших проектов для анализа (обязательно!)
- **/app/data/** - Persistent storage для vector store
- **/app/logs/** - Логи сервера

### Пример с Ollama для semantic search

```bash
docker run -d \
  --name ultrasharp-overlord \
  -p 3001:3001 \
  -v /path/to/projects:/app/projects \
  -v ultrasharp-data:/app/data \
  -e EMBEDDING_URL=http://ollama:11434 \
  -e EMBEDDING_MODEL=granite-embedding:latest \
  --network ultrasharp-net \
  ghcr.io/faxenoff/ultrasharp-tools-overlord:latest
```

---

## 🌟 Возможности

### 52 MCP инструмента
- **Solution Management** - LoadSolution, LoadProject, ReloadSolution
- **Code Navigation** - ViewDefinition, FindReferences, GetTypeHierarchy
- **Code Modification** - ModifyCode, AddMember, RenameSymbol
- **Code Quality** - FormatCode, AnalyzeCodeStyle, ApplyCodeFixes
- **Debugging** - TraceExecution, TraceBackwards, AnalyzeLogs
- **Git Integration** - GitCommit, GitStatus, GitDiff

### Semantic Tools (через Embedding Service)
- **semantic_search** - поиск кода на естественном языке
- **semantic_diff** - сравнение кода через cosine similarity
- **detect_code_clones** - обнаружение дубликатов кода
- **pattern_search** - hybrid search (semantic + regex)
- **find_duplicates** - cross-project duplicate detection

### Universal Semantic Mode
- **Auto-enrichment** для ВСЕХ инструментов
- **Cross-project recommendations** при любой операции
- **Smart caching** - 5-minute TTL для embeddings
- **Graceful degradation** - работает без embedding service

---

## 📖 Документация

### Основные документы
- [**Full Documentation**](https://github.com/faxenoff/ultrasharp-tools-mcp) - Полная документация проекта
- [**Architecture**](https://github.com/faxenoff/ultrasharp-tools-mcp/blob/main/ARCHITECTURE.md) - Архитектура системы
- [**Deployment Guide**](https://github.com/faxenoff/ultrasharp-tools-mcp/blob/main/Run.Docs/Deployment/README.md) - Kubernetes & Docker deployment
- [**Usage Guide**](https://github.com/faxenoff/ultrasharp-tools-mcp/blob/main/USAGE_GUIDE.md) - Практические примеры

### Quick Links
- [Kubernetes Manifests](https://github.com/faxenoff/ultrasharp-tools-mcp/tree/main/kubernetes) - Готовые манифесты
- [Helm Chart](https://github.com/faxenoff/ultrasharp-tools-mcp/tree/main/helm/ultrasharp-tools) - Helm deployment
- [Docker Compose](https://github.com/faxenoff/ultrasharp-tools-mcp/blob/main/Run.Docs/Setup/docker-compose.yml) - Local setup

---

## ⚠️ Важно: Доступ к файлам

**Remote server работает в контейнере и НЕ ИМЕЕТ доступа к файлам на вашей локальной машине!**

### Способы предоставления кода:

1. **Git Clone** (рекомендуется)
```bash
kubectl exec -it ultrasharp-overlord-xxx -- /bin/bash
cd /app/projects
git clone https://github.com/yourcompany/yourproject.git
```

2. **NFS/SMB Mount** - для shared team storage
3. **kubectl cp** - для быстрого тестирования
4. **PersistentVolume + manual upload**

**Подробности:** [Deployment Guide - Access Modes](https://github.com/faxenoff/ultrasharp-tools-mcp/blob/main/Run.Docs/Setup/Access-Modes.md)

---

## 🏗️ Архитектура

### Hybrid Mode (Team Collaboration)
```
Claude Desktop → HTTP/SSE → Overlord
                                ↓
                    [Tool Router - Smart Routing]
                         ↓              ↓
                    [Semantic]     [Analysis]
                     5 tools       47 tools
                         ↓
              EmbeddingService (Ollama/TEI)
                         ↓
            MultiProjectVectorStore
                         ↓
          Cross-project semantic search
```

### Технологии
- **.NET 10** - Latest runtime
- **Roslyn** - Microsoft compiler platform
- **ASP.NET Core** - HTTP server
- **LibGit2Sharp** - Git integration
- **Ollama/TEI** - Embedding models (optional)
- **ReadyToRun** - AOT compilation для быстрого старта

---

## 🔒 Production Best Practices

### Kubernetes Deployment
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: ultrasharp-tools-server
spec:
  replicas: 2
  template:
    spec:
      containers:
      - name: overlord
        image: ghcr.io/faxenoff/ultrasharp-tools-overlord:latest
        resources:
          requests:
            memory: "1Gi"
            cpu: "500m"
          limits:
            memory: "8Gi"
            cpu: "4000m"
        livenessProbe:
          httpGet:
            path: /health
            port: 3001
          initialDelaySeconds: 30
          periodSeconds: 30
        readinessProbe:
          httpGet:
            path: /health
            port: 3001
          initialDelaySeconds: 10
          periodSeconds: 10
```

### Security
- **Non-root user** - контейнер работает как `appuser` (UID 1001)
- **Read-only root filesystem** - рекомендуется
- **Secrets management** - через Kubernetes Secrets
- **Network policies** - ограничьте доступ к pod

### Мониторинг
- **/health** endpoint - health checks
- **Structured logging** - JSON logs для Elasticsearch/Loki
- **Metrics** - TODO: Prometheus metrics (Phase 13)

---

## 📊 Performance

### Benchmarks (890K symbols, 112 git branches)

**Solution Load:**
- Cold: 48.3s
- Warm (with cache): 8.9s (5.4x speedup)

**Branch Switch:**
- Cold: 48.3s
- Warm (layered indexing): 16.6ms (2900x speedup!)

**Semantic Search:**
- p50: 45ms
- p95: 120ms
- p99: 200ms

**Memory Usage:**
- Base: 800MB-1GB
- With semantic services: +150-200MB

---

## 🤝 Подключение клиентов

### Claude Desktop (HTTP/SSE)
```json
{
  "mcpServers": {
    "ultrasharp-remote": {
      "type": "sse",
      "url": "http://localhost:3001/sse",
      "timeout": 60000
    }
  }
}
```

### Droid (Hybrid Mode)
```bash
dotnet run -p UltrasharpTools.Droid -- \
  --mode hybrid \
  --server-url http://localhost:3001 \
  --load-solution /app/projects/MyApp/MyApp.sln
```

---

## 🐛 Troubleshooting

### Pod не стартует
```bash
# Проверьте статус
kubectl describe pod ultrasharp-overlord-xxx

# Проверьте логи
kubectl logs -f ultrasharp-overlord-xxx
```

### Health check fails
```bash
# Проверьте внутри контейнера
kubectl exec -it ultrasharp-overlord-xxx -- curl localhost:3001/health
```

### Out of Memory
- Увеличьте `memory.limits` в deployment
- Уменьшите количество проектов в `/app/projects`
- Используйте `GCConserveMemory=5` для memory optimization

### "Solution not loaded"
- Проверьте что проекты скопированы в `/app/projects`
- Убедитесь что NuGet packages восстановлены
- Проверьте права доступа к файлам

---

## 📦 Доступные теги

| Tag | Description | Use Case |
|-----|-------------|----------|
| `latest` | Latest stable (from `release` branch) | Production |
| `dev` | Development build (from `dev` branch) | Testing |
| `dev-Debug` | Dev build with debug symbols | Debugging |
| `release-Release` | Release build optimized | Production |
| `sha-xxxxxxx` | Specific commit SHA | Reproducible builds |

---

## 📞 Поддержка

- **GitHub Issues:** https://github.com/faxenoff/ultrasharp-tools-mcp/issues
- **Discussions:** https://github.com/faxenoff/ultrasharp-tools-mcp/discussions
- **Documentation:** https://github.com/faxenoff/ultrasharp-tools-mcp

---

## 📄 Лицензия

MIT License - see [LICENSE](https://github.com/faxenoff/ultrasharp-tools-mcp/blob/main/LICENSE)

---

**Версия:** 3.7.1
**Последнее обновление:** 2025-11-27
**Maintainers:** UltrasharpTools Team
