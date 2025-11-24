# UltrasharpTools MCP Server

MCP сервер для анализа C# кода с поддержкой semantic search.

## 🚀 Быстрый старт

### 1. Первая настройка (обязательно!)

**Windows:** Дважды кликните на файл:
```
Config\setup-semantic-embedding.cmd
```

**Linux/macOS:**
```bash
pwsh ./Config/setup-semantic-embedding.ps1
```

Скрипт автоматически:
- ✅ Найдёт вашу GPU
- ✅ Выберет лучшую платформу (Ollama/TEI)
- ✅ Создаст конфигурацию
- ✅ Покажет что установить если нужно

### 2. Проверка настройки

**Windows:** Дважды кликните:
```
Config\validate-semantic-config.cmd
```

**Linux/macOS:**
```bash
pwsh ./Config/validate-semantic-config.ps1
```

Если всё ✓ зелёное - готово к использованию!

### 3. Добавьте в Claude Code

Откройте файл конфигурации Claude Code и добавьте:

```json
{
  "Droids": {
    "SharpTools": {
      "command": "/path/to/UltrasharpTools.Droid.exe",
      "args": [
        "--log-directory",
        "/path/to/logs",
        "--log-level",
        "Information"
      ]
    }
  }
}
```

## 📁 Структура папки

```
Droid/
├── UltrasharpTools.Droid.exe      ← Главный файл
├── *.dll                               ← Runtime файлы
│
├── semantic-config.json                ← Создаётся setup (основной конфиг)
│
├── Config/                             ← 👈 Настройка и конфигурация
│   ├── setup-semantic-embedding.cmd    ← Дважды кликните для настройки!
│   ├── validate-semantic-config.cmd    ← Проверка настроек
│   ├── SEMANTIC_SETUP_GUIDE.md         ← Подробная инструкция
│   └── semantic-config.yaml            ← YAML пример (для справки)
│
├── Scripts/                            ← Вспомогательные скрипты
│   ├── setup-tei.ps1                   ← Запуск TEI сервера
│   ├── setup-semantic-embedding.ps1    ← Setup скрипт (для Linux/macOS)
│   ├── detect-gpu-architecture.ps1     ← Определение GPU
│   ├── validate-semantic-config.ps1    ← Валидация конфига
│   └── ...
│
└── Read.me/                            ← 📖 Документация
    ├── README.md                       ← Это руководство
    ├── PUBLISH_README.md               ← Оригинал
    └── README_FULL.md                  ← Полная документация проекта
```

## 📖 Документация

- **Config/SEMANTIC_SETUP_GUIDE.md** - Полная инструкция по настройке
- **Config/semantic-config.yaml** - Пример конфигурации (YAML формат)
- **Read.me/** - Полная документация проекта

## ⚙️ Конфигурация

### Основной конфиг: `semantic-config.json`

Создаётся автоматически через `setup-semantic-embedding.cmd`

**Если нужно изменить вручную:**

```json
{
  "embedding": {
    "platform": "ollama",      // Платформа: ollama, tei, memory
    "architecture": "auto",     // GPU: auto, cpu, ada, ampere-80, ...

    "ollama": {
      "endpoint": "http://localhost:11434",
      "selected_model": "granite-embedding:latest"
    }
  }
}
```

### Проектный конфиг: `.sharptools/semantic-config.json`

Создаётся автоматически для каждого проекта при первом запуске.

**Автоопределяет:**
- Размер кодовой базы (small/medium/large)
- Язык комментариев (english/multilingual)
- Выбор vector store (sqlite-vec/vectorlite)

## 🔧 Проблемы?

### "TEI is not available"

Установите Ollama (проще) или запустите TEI:
```bash
# Ollama (рекомендуется)
# 1. Скачать: https://ollama.ai
# 2. Установить модель:
ollama pull granite-embedding

# Или TEI (сложнее)
.\Config\Scripts\setup-tei.ps1
```

### "Configuration not found"

Запустите setup:
```bash
Config\setup-semantic-embedding.cmd
```

### "Validation failed"

Запустите с автоисправлением:
```bash
Config\validate-semantic-config.cmd --Fix
```

или:
```bash
pwsh ./Config/validate-semantic-config.ps1 -Fix
```

## 📞 Помощь

1. Сначала прочитайте: **Config\SEMANTIC_SETUP_GUIDE.md**
2. Проверьте конфигурацию: `Config\validate-semantic-config.cmd`
3. Попробуйте автоисправление: `Config\validate-semantic-config.cmd --Fix`

## 🎯 Рекомендуемая платформа

**Ollama** (для большинства пользователей):
- ✅ Установка за 2 минуты
- ✅ Работает с любой GPU (даже RTX 5000)
- ✅ Простое обновление

**TEI** (для опытных пользователей):
- ✅ Максимальная скорость
- ⚠️ Требует Docker
- ⚠️ Не все GPU поддерживаются

---

**Разработчик:** UltrasharpTools Team
**Лицензия:** MIT
