# Hybrid Mode - Руководство

**Статус:** ✅ Реализовано (MVP)

## Что это?

Hybrid Mode - это режим работы Droid, при котором:
- **Локально**: Droid отслеживает изменения файлов и Git
- **Удаленно**: Overlord хранит векторы ВСЕХ проектов команды
- **Результат**: Cross-project search и team insights

## Архитектура

```
Droid (--mode hybrid)               Overlord (Server)
├─ GitWatcherService          ────→ AgentController
├─ FileWatcherService         ────→ MultiProjectVectorStore
├─ EmbeddingService (Ollama)        ├─ Project1/main/vectors.db
└─ ServerBridgeService              ├─ Project2/main/vectors.db
                                    └─ ...
```

**Ключевое отличие от отдельного Agent:**
- Не отдельное приложение, а **режим работы Droid**
- Размер остается 103 MB (все зависимости на месте)
- Просто добавлены новые сервисы для hybrid mode

## Запуск

### 1. Overlord (сервер)

```bash
cd UltrasharpTools.Overlord
dotnet run -- --port 3001
```

API endpoints:
- `http://localhost:3001/api/agent/health` - health check
- `http://localhost:3001/api/agent/file-changed` - file events
- `http://localhost:3001/api/agent/mcp-proxy` - MCP proxy
- `http://localhost:3001/mcp` - MCP server (прямой доступ)

### 2. Droid (hybrid mode)

```bash
cd UltrasharpTools.Droid
dotnet run -- \
  --mode hybrid \
  --server-url http://localhost:3001 \
  --embedding-url http://localhost:11434 \
  --embedding-model nomic-embed-text \
  --load-solution D:/MyProject/MyProject.sln
```

### Новые опции:

| Опция | Описание | По умолчанию |
|-------|----------|------------|
| `--mode` | Режим: `local` или `hybrid` | `local` |
| `--server-url` | URL Overlord сервера | - |
| `--embedding-url` | URL Ollama/TEI | `http://localhost:11434` |
| `--embedding-model` | Модель embedding | `nomic-embed-text` |

## Как это работает

### Local Mode (по умолчанию)

```
Droid (standalone)
├─ Все работает локально
├─ Roslyn, Git, MCP tools
└─ Нет коммуникации с сервером
```

### Hybrid Mode

```
1. Developer редактирует UserService.cs
2. FileWatcherService обнаруживает изменение
3. EmbeddingService векторизует код (Ollama)
4. ServerBridgeService отправляет на Overlord:
   {
     "project": "MyProject",
     "branch": "main",
     "file": "Services/UserService.cs",
     "vectors": [0.123, ...],  // 768 floats
     "content": "public class UserService { ... }"
   }
5. Overlord сохраняет в MultiProjectVectorStore:
   data/multi-project-vectors/MyProject/main/vectors.db
```

### Cross-Project Search

**Главная фича hybrid mode:**

```bash
# Claude вызывает через Droid:
find_duplicates(
  targetCode: "async Task ProcessAsync() { ... }",
  threshold: 0.7,
  scope: "all_projects"  # ← КЛЮЧЕВОЕ!
)

# Droid проксирует на Overlord
# Overlord ищет в ВСЕХ проектах команды:
# - MyProject (текущий)
# - TeamProject1
# - TeamProject2
# - SharedLibraries
# - ... все 20 проектов

# Результат:
# [
#   { project: "TeamProject1", similarity: 0.92, ... },  ← Можно переиспользовать!
#   { project: "SharedLibraries", similarity: 0.89, ... }, ← Уже есть готовое!
#   { project: "MyProject", similarity: 0.87, ... }
# ]
```

## MCP Proxy

В hybrid mode некоторые MCP tools проксируются на Overlord:

**Проксируемые:**
- ✅ `find_duplicates` - cross-project search
- ✅ `load_solution` - загрузка на сервере

**Локальные (используйте local mode или прямой MCP endpoint):**
- ⚠️ `view_definition`
- ⚠️ `find_references`
- ⚠️ `modify_code`
- ⚠️ `analyze_complexity`
- ⚠️ `format_code`

**Рекомендация:** Для полного функционала используйте:
- **Hybrid mode** - для cross-project search и team insights
- **Local mode** - для повседневной разработки и модификации кода
- Или подключайтесь напрямую к Overlord через MCP endpoint

## Embedding Service

Для векторизации нужен Ollama или TEI.

### Ollama (рекомендуется)

```bash
# Установка
curl https://ollama.ai/install.sh | sh

# Загрузка модели
ollama pull nomic-embed-text

# Проверка
curl http://localhost:11434/api/tags
```

### TEI (HuggingFace)

```bash
docker run -p 8080:80 \
  ghcr.io/huggingface/text-embeddings-inference:latest \
  --model-id BAAI/bge-small-en-v1.5

# Использование
--embedding-url http://localhost:8080
```

## Преимущества

✅ **Cross-project search** - поиск дубликатов во ВСЕХ проектах команды
✅ **Team insights** - анализ всей кодовой базы
✅ **Централизованное хранение** - экономия storage
✅ **Один Droid** - не нужно отдельное приложение

## Недостатки

⚠️ **Требует Overlord сервер** - нужна инфраструктура
⚠️ **Требует embedding service** - Ollama или TEI
⚠️ **Network latency** - +200-500ms vs pure local
⚠️ **Ограниченный MCP proxy** - не все tools проксируются

## Storage

Для команды 10 разработчиков, 20 проектов:

**Overlord (централизованно):**
```
~/data/multi-project-vectors/
├─ Project1/
│  ├─ main/vectors.db (50 MB)
│  └─ feature-*/vectors.db (deltas)
├─ Project2/
│  └─ main/vectors.db (120 MB)
└─ ...

Total: ~1.5 GB для всех проектов
```

**vs Local mode:**
- 10 developers × ~500 MB local cache = 5 GB

**Экономия: 70%** 🎉

## Мониторинг

### Логи Droid

```bash
tail -f ~/.ultrasharp/logs/UltrasharpToolsMcpDroid-.log
```

### Логи Overlord

```bash
tail -f ~/.ultrasharp/logs/UltrasharpToolsMcpOverlord-.log
```

### Health Check

```bash
# Overlord
curl http://localhost:3001/api/agent/health

# MCP tools list
curl http://localhost:3001/api/agent/mcp-tools
```

## Troubleshooting

### Droid не подключается к серверу

```bash
# Проверьте доступность
curl http://localhost:3001/api/agent/health

# Убедитесь что server-url правильный
--server-url http://localhost:3001  # НЕ http://localhost:3001/
```

### Embedding service недоступен

```bash
# Ollama
ollama list
curl http://localhost:11434/api/tags

# TEI
curl http://localhost:8080/health
```

**Droid продолжит работать, но без векторизации.**

### High network traffic

```bash
# Увеличьте debounce для FileWatcher
# Отредактируйте AgentConfig (в коде):
FileWatcherDebounceMs = 1000  # 1 секунда вместо 500ms
```

## Next Steps

- [ ] Полный MCP proxy (все tools)
- [ ] Real-time notifications (SSE)
- [ ] Conflict detection (автоматические уведомления о дубликатах)
- [ ] Code reuse recommendations
- [ ] Team analytics dashboard

## См. также

- [HYBRID_ARCHITECTURE.md](HYBRID_ARCHITECTURE.md) - оригинальный дизайн
- [HYBRID_IMPLEMENTATION.md](HYBRID_IMPLEMENTATION.md) - техническая документация
- [UltrasharpTools.Overlord](../../UltrasharpTools.Overlord/) - серверная часть
