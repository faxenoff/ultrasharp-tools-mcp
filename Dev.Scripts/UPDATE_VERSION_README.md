# update-version - Version Update Tool

Автоматически обновляет версию проекта во всех файлах.

## 🚀 Использование

### Windows (cmd)
```cmd
update-version.cmd 3.1.0
update-version.cmd 3.1.0 --dry-run
```

### Linux/macOS (bash)
```bash
./update-version.sh 3.1.0
./update-version.sh 3.1.0 --dry-run
```

### PowerShell (напрямую)
```powershell
.\update-version.ps1 -Version "3.1.0"
.\update-version.ps1 -Version "3.1.0" -DryRun
```

## 📋 Что обновляется

### 1. .csproj файлы (3 файла)
- `UltrasharpTools.Tools/UltrasharpTools.Tools.csproj`
- `UltrasharpTools.Droid/UltrasharpTools.Droid.csproj`
- `UltrasharpTools.Overlord/UltrasharpTools.Overlord.csproj`

```xml
<Version>X.Y.Z</Version>
```

### 2. Dockerfile (2 места)
- `UltrasharpTools.Overlord/Dockerfile`

```dockerfile
version="X.Y.Z"
app.kubernetes.io/version="X.Y.Z"
```

### 3. Markdown документация (6 файлов)
- `ARCHITECTURE.md` - **Версия:** X.Y.Z
- `ROADMAP.md` - **Текущая версия:** X.Y.Z
- `USAGE_GUIDE.md` - **Версия:** X.Y.Z
- `Run.Docs/OVERLORD_README.md` - **Версия:** X.Y.Z
- `Dev.Docs/README.md` - **Версия проекта:** X.Y.Z

### 4. CHANGELOG.md (автоматическое добавление)
Добавляет новую запись с шаблоном:

```markdown
## [X.Y.Z] - YYYY-MM-DD

### 🎯 Статус
**TBD** - Brief description of this release

### Добавлено
- TODO: Add new features here

### Изменено
- TODO: Add changes here

### Исправлено
- TODO: Add fixes here
```

## 🔍 Режим Dry Run

Режим `--dry-run` показывает что будет изменено БЕЗ фактической модификации файлов.

**Рекомендуется:**
1. Сначала запустить с `--dry-run`
2. Проверить что изменения корректны
3. Запустить без флага для применения

## ✅ Workflow

```bash
# 1. Dry run - проверка
update-version.cmd 3.1.0 --dry-run

# 2. Применить изменения
update-version.cmd 3.1.0

# 3. Обновить CHANGELOG.md вручную (заполнить TODO)
# Редактируем CHANGELOG.md

# 4. Проверить изменения
git diff

# 5. Закоммитить
git add .
git commit -m "Bump version to 3.1.0"

# 6. Создать тег
git tag v3.1.0

# 7. Запушить
git push && git push --tags
```

## 📐 Формат версии

Скрипт поддерживает **Semantic Versioning**:

```
MAJOR.MINOR.PATCH[-PRERELEASE]

Примеры:
  ✓ 3.0.0
  ✓ 3.1.0
  ✓ 3.1.5
  ✓ 4.0.0-beta.1
  ✓ 4.0.0-rc.2
  ✗ 3.1 (invalid)
  ✗ v3.1.0 (no 'v' prefix)
```

## 🎯 Примеры использования

### Патч-релиз (bug fixes)
```bash
# 3.0.0 → 3.0.1
update-version.cmd 3.0.1
```

### Минорный релиз (новые фичи)
```bash
# 3.0.1 → 3.1.0
update-version.cmd 3.1.0
```

### Мажорный релиз (breaking changes)
```bash
# 3.1.0 → 4.0.0
update-version.cmd 4.0.0
```

### Pre-release версии
```bash
# 4.0.0 → 4.0.0-beta.1
update-version.cmd 4.0.0-beta.1

# 4.0.0-beta.1 → 4.0.0-rc.1
update-version.cmd 4.0.0-rc.1
```

## 🐛 Troubleshooting

### PowerShell не найден
```
Error: pwsh: command not found
```

**Решение (Linux/macOS):**
```bash
# Установить PowerShell
# Ubuntu/Debian
sudo apt install powershell

# macOS
brew install powershell
```

**Решение (Windows):**
- Используйте `update-version.cmd` вместо прямого вызова PowerShell
- Или установите PowerShell 7+: https://aka.ms/powershell

### Execution Policy блокирует скрипт
```
Error: Execution of scripts is disabled on this system
```

**Решение:**
```powershell
# Temporary (current session)
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass

# Permanent (current user)
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned
```

### Версия уже существует в CHANGELOG.md
```
→ Version X.Y.Z already exists in CHANGELOG.md
```

**Это нормально** - скрипт не добавляет дубликаты. Просто обновите существующую запись вручную.

## 📝 После обновления версии

**Обязательные шаги:**
1. ✅ Обновить TODO в CHANGELOG.md (описать изменения)
2. ✅ Проверить git diff
3. ✅ Создать коммит: `git commit -m "Bump version to X.Y.Z"`
4. ✅ Создать тег: `git tag vX.Y.Z`
5. ✅ Запушить: `git push && git push --tags`

**CI/CD автоматически:**
- Соберёт Docker образ с новой версией
- Опубликует в GitHub Container Registry
- Обновит package metadata

## 🔗 См. также

- [CHANGELOG.md](../CHANGELOG.md) - История изменений
- [ROADMAP.md](../ROADMAP.md) - Планы развития
- [publish-mcp.ps1](publish-mcp.ps1) - Build script

---

**Version:** 1.0.0
**Last Updated:** 2025-11-18
