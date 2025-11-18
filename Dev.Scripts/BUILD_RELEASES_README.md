# Multi-Platform Release Build Guide

Скрипты для сборки UltrasharpTools.Droid под все поддерживаемые платформы и создания release архивов.

## Поддерживаемые платформы

| Platform | OS | Arch | Archive |
|----------|-------|------|---------|
| `win-x64` | Windows | x64 | .zip |
| `win-arm64` | Windows | ARM64 | .zip |
| `osx-x64` | macOS | Intel | .tar.gz |
| `osx-arm64` | macOS | Apple Silicon | .tar.gz |
| `linux-x64` | Linux | x64 | .tar.gz |
| `linux-arm64` | Linux | ARM64 | .tar.gz |

## Использование

### Windows

```cmd
# Автоопределение версии из .csproj
Dev.Scripts\build-releases.cmd

# Указать версию вручную
Dev.Scripts\build-releases.cmd 3.0.0
```

### Linux/macOS

```bash
# Автоопределение версии
./Dev.Scripts/build-releases.sh

# Указать версию
./Dev.Scripts/build-releases.sh 3.0.0
```

### PowerShell (кросс-платформенный)

```powershell
# Автоопределение версии
pwsh Dev.Scripts/build-releases.ps1

# С параметрами
pwsh Dev.Scripts/build-releases.ps1 -Version "3.0.0" -Configuration "Release"
```

## Что делает скрипт

1. **Определяет версию** - автоматически из `.csproj` или из параметра
2. **Очищает старые релизы** - удаляет `Run.Publish/Releases/`
3. **Собирает для каждой платформы**:
   - Вызывает `publish-mcp.ps1` с нужным RID
   - Создаёт ReadyToRun сборки для быстрого старта
4. **Упаковывает в архивы**:
   - Windows: `.zip` (через `Compress-Archive`)
   - Linux/macOS: `.tar.gz` (через `tar`)
5. **Выводит summary** с размерами файлов

## Результат

Архивы сохраняются в `Run.Publish/Releases/`:

```
Run.Publish/Releases/
├── ultrasharp-tools-droid-v3.0.0-windows-x64.zip
├── ultrasharp-tools-droid-v3.0.0-windows-arm64.zip
├── ultrasharp-tools-droid-v3.0.0-macos-x64.tar.gz
├── ultrasharp-tools-droid-v3.0.0-macos-arm64.tar.gz
├── ultrasharp-tools-droid-v3.0.0-linux-x64.tar.gz
└── ultrasharp-tools-droid-v3.0.0-linux-arm64.tar.gz
```

## Публикация в GitHub Releases

### Вариант 1: Через GitHub CLI (автоматически)

```bash
# 1. Создать release
gh release create v3.0.0 \
  --title "Release v3.0.0" \
  --notes "$(cat CHANGELOG.md)"

# 2. Загрузить все архивы
gh release upload v3.0.0 Run.Publish/Releases/*
```

### Вариант 2: Через GitHub UI (вручную)

1. Перейдите на https://github.com/your-username/ultrasharp-tools-mcp/releases/new
2. Заполните форму:
   - **Tag**: `v3.0.0` (создаётся автоматически если нет)
   - **Title**: `Release v3.0.0`
   - **Description**: скопируйте из CHANGELOG.md
3. Перетащите файлы из `Run.Publish/Releases/` в секцию "Attach binaries"
4. Нажмите "Publish release"

### Вариант 3: Через GitHub Actions (CI/CD)

Создайте `.github/workflows/release.yml`:

```yaml
name: Build Release

on:
  push:
    tags:
      - 'v*'

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Build releases
        run: pwsh Dev.Scripts/build-releases.ps1 -Version ${GITHUB_REF#refs/tags/v}

      - name: Create GitHub Release
        uses: softprops/action-gh-release@v1
        with:
          files: Run.Publish/Releases/*
```

Затем:
```bash
git tag v3.0.0
git push origin v3.0.0
```

GitHub Actions автоматически соберёт и опубликует релиз.

## Требования

### Windows
- PowerShell 7+ (для pwsh)
- .NET 10 SDK

### Linux/macOS
- Bash
- PowerShell 7+ (`brew install powershell` или `snap install powershell`)
- .NET 10 SDK
- `tar` и `zip` (обычно установлены)

## Troubleshooting

### "tar: command not found" на Windows

Установите tar через:
- Windows 10 1803+: встроен в систему
- Или через Git for Windows (включает GNU tar)

### "Compress-Archive: OutOfMemoryException"

Для очень больших сборок (>500 MB):
```powershell
# Используйте 7-Zip вместо Compress-Archive
7z a archive.zip folder/*
```

### Сборка зависает

- Проверьте доступность NuGet пакетов
- Очистите кэш: `dotnet nuget locals all --clear`
- Пересоберите: `dotnet clean && ./build-releases.cmd`

## Примеры

### Собрать только для текущей платформы

```powershell
# Вместо build-releases используйте publish-mcp
pwsh Dev.Scripts/publish-mcp.ps1 -Runtime $(dotnet --info | grep 'RID' | awk '{print $2}')
```

### Собрать только для Windows

```powershell
# Модифицируйте $Platforms в build-releases.ps1
$Platforms = @(
    @{ RID = "win-x64"; OS = "windows"; Arch = "x64"; Archive = "zip" }
)
```

### Создать pre-release

```bash
# Tag с суффиксом
git tag v3.0.0-beta.1

# Build
./build-releases.sh 3.0.0-beta.1

# Release как pre-release
gh release create v3.0.0-beta.1 \
  --prerelease \
  --title "Beta Release v3.0.0-beta.1" \
  Run.Publish/Releases/*
```

## См. также

- [Dev.Scripts/publish-mcp.ps1](./publish-mcp.ps1) - Single-platform build
- [Dev.Scripts/publish-all.ps1](./publish-all.ps1) - Build Droid + Overlord
- [GitHub Releases Documentation](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository)
- [GitHub CLI Documentation](https://cli.github.com/manual/gh_release)
