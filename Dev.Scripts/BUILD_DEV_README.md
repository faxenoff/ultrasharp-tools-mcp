# Development Build Scripts

Скрипты для локальной разработки и тестирования hybrid архитектуры.

## Основные скрипты сборки

### build-all.ps1

**Универсальный скрипт для сборки всех компонентов**

```powershell
# Debug сборка (по умолчанию)
pwsh Dev.Scripts/build-all.ps1

# Release сборка
pwsh Dev.Scripts/build-all.ps1 -Configuration Release
```

**Что делает:**
1. Собирает Droid + Indexer (`build-hybrid.ps1`)
2. Собирает Comm (`build-comm.ps1`)
3. Копирует все в `Run.Build/Droid/`
4. Показывает инструкции для Claude Desktop

**Windows launchers в корне:**
- `build-all.cmd` - Debug
- `build-all-release.cmd` - Release

---

### build-hybrid.ps1

**Сборка Droid + Indexer**

```powershell
pwsh Dev.Scripts/build-hybrid.ps1 -Configuration Debug
pwsh Dev.Scripts/build-hybrid.ps1 -Configuration Release
```

**Выход:**
- `Run.Build/Droid/UltrasharpTools.Droid.exe`
- `Run.Build/Droid/UltraSharpTools.Indexer.exe`
- Все зависимости и `Config/`

**Windows launchers в корне:**
- `build-hybrid.cmd`
- `build-droid-release.cmd`

---

### build-comm.ps1

**Сборка Comm (IPC proxy)**

```powershell
pwsh Dev.Scripts/build-comm.ps1 -Configuration Debug
pwsh Dev.Scripts/build-comm.ps1 -Configuration Release
```

**Выход:**
- `Run.Build/Droid/UltraSharpTools.Comm.exe`
- Debug: + все dll зависимости
- Release: только exe (Native AOT)

**Windows launchers в корне:**
- `build-comm.cmd`
- `build-comm-release.cmd`

---

## Production Release Scripts

### publish-hybrid.ps1

**Публикация всей hybrid архитектуры с оптимизациями**

```powershell
pwsh Dev.Scripts/publish-hybrid.ps1
```

**Выход:** `Run.Publish/Hybrid/` со всеми компонентами
- Comm: Native AOT (~13 MB)
- Indexer: Native AOT (~18 MB)
- Droid: (~165 MB)

**Windows launcher в корне:**
- `publish-hybrid.cmd`

---

### publish-mcp.ps1

**Публикация только Droid (монолитная версия)**

Используется для standalone Droid без hybrid архитектуры.

```powershell
pwsh Dev.Scripts/publish-mcp.ps1
```

**Windows launcher в корне:**
- `publish-droid.cmd`

---

## Версионирование

### update-version.ps1

**Обновление версии во всех .csproj**

```powershell
pwsh Dev.Scripts/update-version.ps1 -NewVersion "3.0.7"
```

**Что делает:**
1. Обновляет `<Version>` во всех .csproj
2. Создает Git commit
3. Создает Git tag `v3.0.7`

**Windows launcher в корне:**
- `update-version.cmd 3.0.7`

См. также: [UPDATE_VERSION_README.md](./UPDATE_VERSION_README.md)

---

## Multi-Platform Releases

### build-releases.ps1

**Сборка для всех платформ (win/mac/linux, x64/arm64)**

```powershell
pwsh Dev.Scripts/build-releases.ps1 -Version "3.0.0"
```

**Выход:** `Run.Publish/Releases/*.zip` и `*.tar.gz` архивы

**Launchers:**
- `build-releases.cmd` (Windows)
- `build-releases.sh` (Linux/macOS)

См. также: [BUILD_RELEASES_README.md](./BUILD_RELEASES_README.md)

---

## Semantic Embedding

### setup-semantic-embedding.ps1

**Интерактивная настройка semantic search**

```powershell
pwsh Dev.Scripts/setup-semantic-embedding.ps1
```

**Что делает:**
1. Определяет язык кодовой базы
2. Определяет размер проекта
3. Подбирает embedding модель
4. Настраивает провайдер (TEI/Ollama/Memory)
5. Создает `semantic-config.json`

**Windows launcher в корне:**
- `Run.Config/setup-semantic-embedding.cmd`

---

## Вспомогательные скрипты

### organize-publish.ps1

Организует структуру папок в `Run.Publish/`

### convert-tokenizer-to-fast.ps1/.py

Конвертирует токенизаторы HuggingFace в fast версии

### setup-nvidia-container-toolkit.ps1

Устанавливает NVIDIA Container Toolkit для Docker + GPU

---

## Быстрый старт

```bash
# 1. Полная сборка для локальной разработки
build-all.cmd

# 2. Настройка Claude Desktop
# %APPDATA%\Claude\claude_desktop_config.json:
{
  "mcpServers": {
    "ultrasharp-tools": {
      "command": "D:\\github\\ultrasharp-tools-mcp\\Run.Build\\Droid\\UltraSharpTools.Comm.exe"
    }
  }
}

# 3. Перезапуск Claude Desktop
```

---

## Иерархия скриптов

```
Dev.Scripts/
├── build-all.ps1           ← Основной (вызывает build-hybrid + build-comm)
├── build-hybrid.ps1        ← Droid + Indexer
├── build-comm.ps1          ← Comm
├── publish-hybrid.ps1      ← Release hybrid (все компоненты)
├── publish-mcp.ps1         ← Release monolith (только Droid)
└── build-releases.ps1      ← Multi-platform releases
```

**Правило:** Всегда используйте `build-all.ps1` для локальной разработки!

---

## См. также

- [BUILD_SCRIPTS.md](../BUILD_SCRIPTS.md) - Детальная документация
- [BUILD_RELEASES_README.md](./BUILD_RELEASES_README.md) - Multi-platform releases
- [UPDATE_VERSION_README.md](./UPDATE_VERSION_README.md) - Управление версиями
- [QUICK_START.md](../QUICK_START.md) - Быстрый старт
