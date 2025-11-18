# Настройка Semantic Embedding для SharpTools

Semantic Embedding позволяет делать семантический поиск по коду - находить похожие фрагменты по смыслу, а не просто по ключевым словам.

## 🚀 Быстрый старт (3 шага)

### Вариант 1: Ollama (рекомендуется) ✅

**Почему лучше:** Проще всего настроить, не требует Docker, работает на CPU и GPU

1. **Установите Ollama:**
   ```bash
   # Windows/macOS/Linux: https://ollama.ai
   ```

2. **Скачайте модель:**
   ```bash
   ollama pull granite-embedding
   ```

3. **Запустите setup:**
   ```bash
   # Windows (двойной клик или через CMD)
   setup-semantic-embedding.cmd

   # Linux/macOS
   pwsh ./Scripts/setup-semantic-embedding.ps1
   ```
   Выберите `2) Ollama` когда спросит платформу

**Готово!** 🎉 MCP сервер автоматически использует Ollama

---

### Вариант 2: TEI (для производительности) ⚡

**Почему хорош:** Максимальная производительность на GPU, но требует Docker и NVIDIA GPU

1. **Требования:**
   - Docker Desktop
   - NVIDIA GPU (Pascal или новее)
   - NVIDIA Container Toolkit

2. **Запустите TEI сервер:**
   ```bash
   # Windows
   pwsh .\Scripts\setup-tei.ps1

   # Linux/macOS
   pwsh ./Scripts/setup-tei.sh
   ```

3. **Запустите setup:**
   ```bash
   # Windows
   setup-semantic-embedding.cmd

   # Linux/macOS
   pwsh ./Scripts/setup-semantic-embedding.ps1
   ```
   Выберите `1) TEI`

---

### Вариант 3: Memory (для тестов)

**Когда использовать:** Только для локального тестирования без внешних зависимостей

Выберите `3) Memory` при запуске setup. Работает медленнее остальных вариантов.

---

## 📁 Где лежат конфиги

### Глобальный конфиг (один для всех проектов)

**Файл:** `semantic-config.json` (создаётся рядом с `.exe`)

```json
{
  "embedding": {
    "platform": "ollama",           // Платформа: ollama, tei, memory
    "architecture": "auto",          // GPU: auto, cpu, ampere-80, etc.
    "ollama": {
      "endpoint": "http://localhost:11434",
      "selected_model": "granite-embedding:latest"
    }
  }
}
```

**Что можно менять:**
- `platform` - переключить между Ollama/TEI/Memory
- `endpoint` - если сервер на другом хосте
- `selected_model` - выбрать другую модель

### Проектный конфиг (для каждого проекта)

**Файл:** `.sharptools/project-semantic-config.yaml` (создаётся автоматически)

```yaml
codebase:
  size: auto              # Размер: auto, small, medium, large
  language: auto          # Язык: auto, english, multilingual
  multilingual_threshold: 20  # % не-английских слов для переключения на multilingual

vector_store:
  engine: sqlite-vec      # sqlite-vec или vectorlite (для больших проектов)
```

**Что можно менять:**
- `language: multilingual` - если в коде много кириллицы/китайских комментариев
- `size: large` - если проект очень большой (>10k файлов)

---

## 🔧 Продвинутые настройки

### Переменные окружения (переопределяют конфиг)

```bash
# Windows (PowerShell)
$env:SEMANTIC_PLATFORM = "ollama"
$env:OLLAMA_ENDPOINT = "http://192.168.1.100:11434"

# Linux/macOS
export SEMANTIC_PLATFORM=ollama
export OLLAMA_ENDPOINT=http://192.168.1.100:11434
```

Доступные переменные:
- `SEMANTIC_PLATFORM` - платформа (ollama, tei, memory)
- `SEMANTIC_ARCHITECTURE` - GPU архитектура (auto, cpu, ampere-80, ada, etc.)
- `TEI_ENDPOINT` - адрес TEI сервера
- `OLLAMA_ENDPOINT` - адрес Ollama сервера

### Выбор модели

#### Ollama модели:
- `granite-embedding:latest` - английский, быстрая (384 dim) ✅ **рекомендуется**
- `mxbai-embed-large:latest` - многоязычная, качественная (1024 dim)
- `nomic-embed-text:latest` - английский, эффективная (768 dim)

```bash
# Скачать модель
ollama pull mxbai-embed-large

# Изменить в semantic-config.json
"selected_model": "mxbai-embed-large:latest"
```

#### TEI модели:
- `sentence-transformers/all-MiniLM-L6-v2` - английский, быстрая (384 dim) ✅
- `sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2` - многоязычная (384 dim)
- `sentence-transformers/all-mpnet-base-v2` - английский, качественная (768 dim)

Модель выбирается автоматически на основе языка кодовой базы.

### Определение GPU архитектуры

```bash
# Windows
pwsh .\Scripts\detect-gpu-architecture.ps1

# Linux/macOS
pwsh ./Scripts/detect-gpu-architecture.ps1
```

Вернёт: `cpu`, `turing`, `ampere-80`, `ampere-86`, `ada`, `hopper`

**Важно:** RTX 5060 (Blackwell) не поддерживается TEI - используйте Ollama или CPU режим

---

## ❓ Проблемы?

### "TEI is not available"

**Решение:**
1. Проверьте что Docker запущен: `docker ps`
2. Запустите TEI: `pwsh .\Scripts\setup-tei.ps1` (Windows) или `pwsh ./Scripts/setup-tei.sh` (Linux/macOS)
3. Или переключитесь на Ollama (проще):
   ```bash
   ollama pull granite-embedding
   # В semantic-config.json измените platform на "ollama"
   ```

### "Configuration not found"

**Решение:** Запустите `setup-semantic-embedding.cmd`

### "Model not found" (Ollama)

**Решение:**
```bash
ollama list                      # Посмотреть установленные модели
ollama pull granite-embedding    # Скачать модель
```

### Медленный поиск

**Решение:**
- Ollama: Убедитесь что модель скачана (`ollama list`)
- TEI: Используйте GPU вместо CPU
- Для проектов >10k файлов: измените `size: large` в проектном конфиге

### GPU не используется (TEI)

**Решение:**
1. Проверьте NVIDIA драйверы: `nvidia-smi`
2. Установите NVIDIA Container Toolkit
3. В `semantic-config.json` измените `architecture` на вашу GPU (например `"ampere-80"`)

---

## 📚 Технические детали

### Как это работает

1. **Индексация:** При первом открытии проекта код разбивается на семантические блоки (функции, классы)
2. **Векторизация:** Каждый блок преобразуется в embedding (числовой вектор)
3. **Хранение:** Векторы сохраняются в `.sharptools/embeddings.db` (SQLite)
4. **Поиск:** Ваш запрос тоже преобразуется в вектор и ищутся похожие через косинусное расстояние

### Размеры векторов

- 384 dim - быстро, достаточно для большинства задач
- 768 dim - выше качество, медленнее
- 1024 dim - максимальное качество, самая медленная

### Когда переиндексировать

База обновляется автоматически при изменении файлов. Полная переиндексация нужна только если:
- Сменили модель (размерность вектора изменилась)
- База повреждена

Удалите `.sharptools/embeddings.db` и перезапустите MCP сервер.

---

## 🎯 Сравнение платформ

| Платформа | Скорость | Простота установки | GPU | Качество |
|-----------|----------|-------------------|-----|----------|
| **Ollama** | ⚡⚡⚡ | ✅ Очень просто | Опционально | ⭐⭐⭐⭐ |
| **TEI** | ⚡⚡⚡⚡ | ⚠️ Нужен Docker | Обязательно | ⭐⭐⭐⭐⭐ |
| **Memory** | ⚡ | ✅ Без зависимостей | Нет | ⭐⭐⭐ |

**Рекомендация:** Начните с Ollama. Переходите на TEI если нужна максимальная производительность на большом проекте.
