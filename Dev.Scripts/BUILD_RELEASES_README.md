# Multi-Platform Release Build Guide

Скрипты для сборки UltrasharpTools под все поддерживаемые платформы и создания release архивов.

## Архитектура сборки (v3.0.8+)

**Shared Runtime Architecture** — один .NET runtime для всех framework-dependent приложений.

```
Run.Publish/Releases/ultrasharp-tools-v3.0.8-windows-x64.zip
├── shared/                    # Общий .NET runtime (~70MB)
│   └── Microsoft.NETCore.App/
├── Droid/                     # Framework-dependent (~30MB)
│   ├── run-droid.cmd          # Launcher (устанавливает DOTNET_ROOT)
│   ├── run-droid.sh
│   ├── UltrasharpTools.Droid.exe
│   └── UltraSharpTools.VectorDB.exe  # Native AOT (~15MB)
├── Comm/                      # Trimmed SingleFile (~5MB)
│   └── UltraSharpTools.Comm.exe
└── README.md
```

> **Note:** Overlord собирается отдельно через Dockerfile и не включён в release архивы.

**Экономия:** ~50MB (~33%) по сравнению с self-contained сборкой.

---

## Поддерживаемые платформы

| Platform | OS | Arch | Archive |
|----------|-------|------|---------|
| `win-x64` | Windows | x64 | .zip |
| `win-arm64` | Windows | ARM64 | .zip |
| `osx-x64` | macOS | Intel | .tar.gz |
| `osx-arm64` | macOS | Apple Silicon | .tar.gz |
| `linux-x64` | Linux | x64 | .tar.gz |
| `linux-arm64` | Linux | ARM64 | .tar.gz |

---

## Использование

### Быстрый старт (из корня проекта)

```bash
# Windows
build-release.cmd

# Linux/macOS
./build-release.sh
```

Это создаст release для текущей платформы в `Run.Publish/`.

### Multi-Platform Release (все платформы)

```cmd
# Windows
Dev.Scripts\build-releases.cmd

# Linux/macOS
./Dev.Scripts/build-releases.sh

# PowerShell (кросс-платформенный)
pwsh Dev.Scripts/build-releases.ps1 -Version "3.0.8"
```

---

## Что делает скрипт

1. **Определяет версию** — автоматически из `.csproj` или из параметра
2. **Для каждой платформы:**
   - Извлекает shared .NET runtime в `shared/`
   - Собирает VectorDB (Native AOT)
   - Собирает Comm (Trimmed SingleFile)
   - Собирает Droid (framework-dependent)
   - Копирует VectorDB в Droid/
   - Создаёт launcher скрипты
3. **Упаковывает в архивы:**
   - Windows: `.zip`
   - Linux/macOS: `.tar.gz`
4. **Выводит summary** с размерами файлов

> **Note:** Overlord собирается отдельно через Dockerfile.

---

## Результат

Архивы сохраняются в `Run.Publish/Releases/`:

```
Run.Publish/Releases/
├── ultrasharp-tools-v3.0.8-windows-x64.zip       (~100MB)
├── ultrasharp-tools-v3.0.8-windows-arm64.zip
├── ultrasharp-tools-v3.0.8-macos-x64.tar.gz
├── ultrasharp-tools-v3.0.8-macos-arm64.tar.gz
├── ultrasharp-tools-v3.0.8-linux-x64.tar.gz
└── ultrasharp-tools-v3.0.8-linux-arm64.tar.gz
```

---

## Публикация в GitHub Releases

### Вариант 1: Через GitHub CLI

```bash
# 1. Создать release
gh release create v3.0.8 \
  --title "Release v3.0.8" \
  --notes "$(cat CHANGELOG.md)"

# 2. Загрузить все архивы
gh release upload v3.0.8 Run.Publish/Releases/*
```

### Вариант 2: Через GitHub UI

1. Перейдите на https://github.com/your-username/ultrasharp-tools-mcp/releases/new
2. Заполните форму:
   - **Tag**: `v3.0.8`
   - **Title**: `Release v3.0.8`
   - **Description**: скопируйте из CHANGELOG.md
3. Перетащите файлы из `Run.Publish/Releases/`
4. Нажмите "Publish release"

### Вариант 3: Через GitHub Actions (CI/CD)

```yaml
name: Build Release

on:
  push:
    tags:
      - 'v*'

jobs:
  build:
    strategy:
      matrix:
        os: [ubuntu-latest, windows-latest, macos-latest]
    runs-on: ${{ matrix.os }}
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Build release
        run: pwsh build-release.ps1

      - name: Upload artifacts
        uses: actions/upload-artifact@v4
        with:
          name: release-${{ matrix.os }}
          path: Run.Publish/

  release:
    needs: build
    runs-on: ubuntu-latest
    steps:
      - name: Download artifacts
        uses: actions/download-artifact@v4

      - name: Create GitHub Release
        uses: softprops/action-gh-release@v1
        with:
          files: release-*/**/*
```

---

## Требования

### Windows
- PowerShell 7+ (для pwsh)
- .NET 10 SDK
- Visual Studio Build Tools с C++ workload (для Native AOT)

### Windows + WSL (для Linux Native AOT)

Скрипт `build-releases.ps1` автоматически использует WSL для сборки Linux Native AOT.
Это даёт **~60% уменьшение размера** Linux архивов (130MB → 50-60MB).

#### Настройка WSL

1. **Установите WSL2 с Ubuntu:**
   ```powershell
   wsl --install -d Ubuntu-24.04
   ```

2. **Настройте DNS (если используете прокси типа Mihomo/Clash):**

   Добавьте в `%USERPROFILE%\.wslconfig`:
   ```ini
   [wsl2]
   networkingMode=NAT

   [experimental]
   dnsTunneling=true
   ```

   Перезапустите WSL:
   ```powershell
   wsl --shutdown
   ```

3. **Установите зависимости в WSL:**
   ```bash
   # Build tools
   sudo apt-get update
   sudo apt-get install -y clang build-essential zlib1g-dev curl

   # .NET 10 SDK (preview - не в стандартных репозиториях)
   curl -sSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
   chmod +x /tmp/dotnet-install.sh
   sudo /tmp/dotnet-install.sh --channel 10.0 --install-dir /usr/share/dotnet
   sudo ln -sf /usr/share/dotnet/dotnet /usr/local/bin/dotnet

   # Проверка
   dotnet --version
   clang --version
   ```

4. **Запустите сборку:**
   ```powershell
   .\Dev.Scripts\build-releases.ps1
   ```

   Скрипт автоматически:
   - Обнаружит WSL
   - Проверит наличие .NET SDK и clang
   - Соберёт VectorDB и Comm с Native AOT через WSL
   - Соберёт Droid как self-contained (Roslyn не поддерживает AOT)

#### Пропуск WSL сборки

Если WSL недоступен или хотите self-contained:
```powershell
.\Dev.Scripts\build-releases.ps1 -SkipLinuxAot
```

### Linux/macOS
- Bash
- PowerShell 7+ (`brew install powershell` или `snap install powershell`)
- .NET 10 SDK
- clang, zlib (для Native AOT)
- `tar` и `zip` (обычно установлены)

Для Native AOT на Linux:
```bash
sudo apt install clang build-essential zlib1g-dev
```

---

## Параметры скриптов

### build-release.ps1 (в корне)

| Параметр | По умолчанию | Описание |
|----------|--------------|----------|
| `-Clean` | false | Очистить выходные директории |
| `-RuntimeIdentifier` | win-x64 | Целевая платформа (RID) |
| `-SkipRuntime` | false | Пропустить извлечение runtime |

### build-releases.ps1 (в Dev.Scripts/)

| Параметр | По умолчанию | Описание |
|----------|--------------|----------|
| `-Version` | auto | Версия (из .csproj если не указана) |
| `-Configuration` | Release | Конфигурация сборки |
| `-OutputDir` | Run.Publish/Releases | Директория для архивов |

---

## Troubleshooting

### "tar: command not found" на Windows

- Windows 10 1803+: встроен в систему
- Или установите через Git for Windows (включает GNU tar)

### Сборка зависает

- Проверьте доступность NuGet пакетов
- Очистите кэш: `dotnet nuget locals all --clear`
- Пересоберите: `build-release.cmd -Clean`

### Native AOT не собирается

- Убедитесь что установлен C++ Build Tools
- Windows: Visual Studio Build Tools с C++ workload
- Linux: `sudo apt install clang zlib1g-dev`

---

## См. также

- [BUILD_DEV_README.md](./BUILD_DEV_README.md) - Скрипты разработки
- [UPDATE_VERSION_README.md](./UPDATE_VERSION_README.md) - Управление версиями
- [GitHub Releases Documentation](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository)
