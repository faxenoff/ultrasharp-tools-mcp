# Development Build Scripts

Скрипты для локальной разработки и тестирования UltrasharpTools.

## Основные скрипты в корне проекта

### build-debug.cmd / build-debug.sh / build-debug.ps1

**Debug сборка для локальной разработки**

```bash
# Windows
build-debug.cmd

# Linux/macOS
./build-debug.sh

# PowerShell (кросс-платформенный)
pwsh build-debug.ps1
```

**Параметры:**
- `-Clean` — очистить выходные директории перед сборкой
- `-RuntimeIdentifier` — целевая платформа (default: win-x64)

**Что делает:**
1. Собирает VectorDB (self-contained)
2. Собирает Droid (self-contained)
3. Собирает Comm (trimmed single-file)
4. Копирует VectorDB в Droid/

> **Note:** Overlord собирается отдельно через Dockerfile.

**Выход:** `Run.Publish.Debug/`
```
Run.Publish.Debug/
├── Droid/          # Droid + VectorDB + все зависимости
├── Comm/           # Comm single-file
└── VectorDB/       # VectorDB standalone
```

---

### build-release.cmd / build-release.sh / build-release.ps1

**Release сборка с shared runtime (~70MB экономия)**

```bash
# Windows
build-release.cmd

# Linux/macOS
./build-release.sh

# PowerShell (кросс-платформенный)
pwsh build-release.ps1
```

**Параметры:**
- `-Clean` — очистить выходные директории перед сборкой
- `-RuntimeIdentifier` — целевая платформа (default: win-x64)
- `-SkipRuntime` — пропустить извлечение runtime (использовать существующий)

**Что делает:**
1. Извлекает .NET runtime в `shared/` (один раз)
2. Собирает VectorDB (Native AOT) → один бинарник
3. Собирает Comm (Trimmed SingleFile) → один бинарник
4. Собирает Droid (framework-dependent) → использует shared runtime
5. Создаёт launcher скрипты (run-droid.cmd, run-droid.sh)

> **Note:** Overlord собирается отдельно через Dockerfile.

**Выход:** `Run.Publish/`
```
Run.Publish/
├── shared/         # Общий .NET runtime (~70MB)
├── Droid/          # Droid + VectorDB (framework-dependent)
│   ├── run-droid.cmd
│   ├── run-droid.sh
│   └── UltrasharpTools.Droid.exe
├── Comm/           # Comm (trimmed single-file, ~5MB)
└── README.md
```

**Экономия места:**
- До: ~150MB (Droid self-contained)
- После: ~70MB (shared) + ~30MB (Droid) = ~100MB
- **Экономия: ~50MB (~33%)**

---

## Вспомогательные скрипты в Dev.Scripts/

### build-all.ps1

**Универсальный скрипт для сборки всех компонентов (legacy)**

```powershell
# Debug сборка (по умолчанию)
pwsh Dev.Scripts/build-all.ps1

# Release сборка
pwsh Dev.Scripts/build-all.ps1 -Configuration Release
```

---

### build-hybrid.ps1

**Сборка Droid + VectorDB**

```powershell
pwsh Dev.Scripts/build-hybrid.ps1 -Configuration Debug
```

---

### build-comm.ps1

**Сборка Comm (IPC proxy)**

```powershell
pwsh Dev.Scripts/build-comm.ps1 -Configuration Debug
```

---

### publish-mcp.ps1

**Публикация только Droid (монолитная версия)**

```powershell
pwsh Dev.Scripts/publish-mcp.ps1 -Runtime win-x64
```

---

## Версионирование

### update-version.ps1

**Обновление версии во всех .csproj**

```powershell
pwsh Dev.Scripts/update-version.ps1 -NewVersion "3.0.8"
```

**Что делает:**
1. Обновляет `<Version>` во всех .csproj
2. Создает Git commit
3. Создает Git tag `v3.0.8`

**Windows launcher в корне:**
- `update-version.cmd 3.0.8`

---

## Multi-Platform Releases

### build-releases.ps1

**Сборка для всех платформ (win/mac/linux, x64/arm64)**

```powershell
pwsh Dev.Scripts/build-releases.ps1 -Version "3.0.8"
```

**Выход:** `Run.Publish/Releases/*.zip` и `*.tar.gz` архивы

**Launchers:**
- `Dev.Scripts/build-releases.cmd` (Windows)
- `Dev.Scripts/build-releases.sh` (Linux/macOS)

См. также: [BUILD_RELEASES_README.md](./BUILD_RELEASES_README.md)

---

## Semantic Embedding

### setup-semantic-embedding.ps1

**Интерактивная настройка semantic search**

```powershell
pwsh Dev.Scripts/setup-semantic-embedding.ps1
```

**Windows launcher:**
- `Run.Config/setup-semantic-embedding.cmd`

---

## Быстрый старт

```bash
# 1. Debug сборка для локальной разработки
build-debug.cmd

# 2. Release сборка для production
build-release.cmd

# 3. Настройка Claude Desktop
# %APPDATA%\Claude\claude_desktop_config.json:
{
  "mcpServers": {
    "ultrasharp-tools": {
      "command": "D:\\path\\to\\Run.Publish\\Droid\\run-droid.cmd"
    }
  }
}

# 4. Или использовать Comm (IPC proxy)
{
  "mcpServers": {
    "ultrasharp-tools": {
      "command": "D:\\path\\to\\Run.Publish\\Comm\\UltraSharpTools.Comm.exe"
    }
  }
}
```

---

## Архитектура сборки

```
                    ┌─────────────────────────────────────┐
                    │         build-release.ps1           │
                    │   (создаёт shared runtime)          │
                    └─────────────┬───────────────────────┘
                                  │
          ┌───────────────────────┼───────────────────────┐
          │                       │                       │
          ▼                       ▼                       ▼
    ┌───────────┐           ┌───────────┐          ┌───────────┐
    │   Droid   │           │  VectorDB │          │   Comm    │
    │ framework │           │ Native AOT│          │  trimmed  │
    │ dependent │           │ (single)  │          │single-file│
    └─────┬─────┘           └───────────┘          └───────────┘
          │
          │    ┌────────────────────────────┐
          └───►│  shared/ (.NET runtime)   │
               │       ~70MB               │
               └────────────────────────────┘
```

**Comm** — отдельный trimmed single-file (~5MB), не использует shared runtime.

> **Note:** Overlord собирается отдельно через Dockerfile (не включён в диаграмму).

---

## См. также

- [BUILD_RELEASES_README.md](./BUILD_RELEASES_README.md) - Multi-platform releases
- [UPDATE_VERSION_README.md](./UPDATE_VERSION_README.md) - Управление версиями
- [../Dev.Docs/BUILD_SCRIPTS.md](../Dev.Docs/BUILD_SCRIPTS.md) - Детальная документация
