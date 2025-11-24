# Build Scripts для Hybrid Architecture

Скрипты для сборки компонентов hybrid архитектуры (Comm + Droid + Indexer).

## Структура

```
Run.Build/Droid/            # Debug сборки
├── UltraSharpTools.Comm.exe
├── UltrasharpTools.Droid.exe
├── UltraSharpTools.Indexer.exe
├── vectorlite.dll
├── Config/
│   ├── embedding-models.json
│   ├── semantic-config.json
│   ├── semantic-mode-config.json
│   ├── setup-semantic-embedding.cmd
│   └── Scripts/
└── ... все DLL

Run.Publish/Hybrid/         # Release сборки (с Native AOT)
├── UltraSharpTools.Comm.exe     (Native AOT, ~13 MB)
├── UltrasharpTools.Droid.exe    (~165 MB)
├── UltraSharpTools.Indexer.exe  (Native AOT, ~18 MB)
├── vectorlite.dll
├── Config/
│   ├── embedding-models.json
│   ├── semantic-config.json
│   ├── semantic-mode-config.json
│   ├── setup-semantic-embedding.cmd
│   └── Scripts/
└── ... все DLL
```

**Важно**: Config/ содержит файлы напрямую из `Run.Config/Config/`, без вложенных структур.

## Скрипты сборки (Debug)

### Сборка Comm

```cmd
build-comm.cmd                # Debug сборка (обычная)
build-comm-release.cmd        # Release сборка (Native AOT)
```

**Выход**: `Run.Build/Droid/UltraSharpTools.Comm.exe`

### Сборка Droid + Indexer

```cmd
build-droid.cmd              # Debug сборка (обычная)
build-droid-release.cmd      # Release сборка (с AOT для Indexer)
```

**Выход**:
- `Run.Build/Droid/UltrasharpTools.Droid.exe`
- `Run.Build/Droid/UltraSharpTools.Indexer.exe`
- Все зависимости и Config/

### Полная сборка (рекомендуется)

```cmd
# Собрать все компоненты в правильном порядке (Debug)
build-all.cmd

# Собрать все компоненты в правильном порядке (Release)
build-all-release.cmd
```

**Что делает `build-all.cmd`**:
1. Собирает Droid + Indexer (`build-hybrid.cmd`)
2. Собирает Comm (`build-comm.cmd`)
3. Копирует все зависимости в `Run.Build/Droid/`
4. Показывает инструкции для Claude Desktop

### Раздельная сборка (ручной режим)

```cmd
# Собрать все компоненты (Debug)
build-hybrid.cmd   # Сначала Droid + Indexer
build-comm.cmd     # Затем Comm

# Собрать все компоненты (Release)
build-droid-release.cmd   # Сначала Droid + Indexer
build-comm-release.cmd     # Затем Comm
```

**Важно:** Всегда собирайте Droid/Indexer перед Comm!

## Скрипты публикации (Release)

### Публикация всей hybrid архитектуры

```cmd
publish-hybrid.cmd
```

Собирает и публикует:
1. **Comm** (Native AOT) → Run.Publish/Comm/
2. **Indexer** (Native AOT) → Run.Publish/Indexer/
3. **Droid** → Run.Publish/Droid/
4. Копирует все в **Run.Publish/Hybrid/** (финальная структура)

**Выход**: `Run.Publish/Hybrid/` со всеми компонентами в одной директории

## Различия Debug vs Release

| Компонент | Debug | Release |
|-----------|-------|---------|
| **Comm** | Обычная сборка | Native AOT (~13 MB) |
| **Indexer** | Обычная сборка | Native AOT (~18 MB) |
| **Droid** | Обычная сборка (~165 MB) | Обычная сборка (~165 MB) |

**Особенности сборки:**
- Comm и Indexer в Release используют Native AOT для минимального размера
- Droid использует обычную сборку в обоих режимах
- Native AOT для Comm не работает в Debug без VS linker

## Тестирование локальной сборки

После сборки Debug версии:

```cmd
cd Run.Build\Droid
UltraSharpTools.Comm.exe
```

Comm автоматически:
- Запустит Droid.exe в IPC mode
- Droid запустит Indexer.exe для semantic операций
- Прокси MCP запросы через Named Pipe

## Очистка

```cmd
# Удалить Debug сборки
rmdir /s /q Run.Build\Droid

# Удалить Release публикации
rmdir /s /q Run.Publish\Hybrid
rmdir /s /q Run.Publish\Comm
rmdir /s /q Run.Publish\Droid
rmdir /s /q Run.Publish\Indexer
```

## Примечания

1. **Config/** автоматически копируется из `Run.Config/Config/` при сборке hybrid
2. **Все три exe** должны быть в одной директории для IPC
3. **vectorlite.dll** копируется автоматически для Indexer
4. **BuildHost-** папки копируются для Roslyn анализа
5. **semantic-config.json** должен быть в Config/ для semantic mode
6. **BUILD_SCRIPTS.md** НЕ копируется в сборку (остаётся только в корне)

## Troubleshooting

### Ошибка: "vswhere.exe not found" при Native AOT

**Решение**: Используйте Debug сборку (`build-comm.cmd`) или установите Visual Studio Build Tools

### Ошибка: "Droid executable not found"

**Причина**: Droid.exe не в той же директории что Comm.exe

**Решение**: Запустите `build-hybrid.cmd` перед запуском Comm

### Ошибка: "Indexer executable not found"

**Причина**: Indexer.exe не в той же директории что Droid.exe

**Решение**: Запустите `build-hybrid.cmd` - он копирует оба exe в Run.Build/Droid/

## Старые скрипты

Эти скрипты НЕ для hybrid архитектуры:

- `publish-mcp.ps1` - публикует только Droid (монолитная версия)
- `publish-comm.ps1` - публикует только Comm (используется внутри publish-hybrid.ps1)
- `publish-indexer.ps1` - публикует только Indexer (используется внутри publish-hybrid.ps1)

Для hybrid используйте **только** новые скрипты из этого документа!
