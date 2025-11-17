# Embeddings Setup Guide

Руководство по настройке embedding providers для семантического анализа C# кода в UltrasharpTools MCP.

## 🎯 Быстрый старт

### Автоматическая установка (рекомендуется)

**Windows:**
```powershell
.\setup-embeddings-interactive.ps1
```

**macOS/Linux:**
```bash
chmod +x ./setup-embeddings-interactive.sh
./setup-embeddings-interactive.sh
```

Скрипт автоматически:
1. ✅ Определяет GPU capabilities (NVIDIA compute capability)
2. ✅ Рекомендует оптимальный provider (TEI/Ollama)
3. ✅ Устанавливает выбранный provider
4. ✅ Проверяет работоспособность

---

## 📊 Сравнение Providers

| Provider | Контекст | Требования | Качество | Скорость | Рекомендация |
|----------|----------|------------|----------|----------|--------------|
| **TEI** | **8192 токена** | RTX 30xx+ + Docker | ⭐⭐⭐⭐⭐ | ⚡⚡⚡⚡⚡ | 🏆 **Лучший выбор** |
| Ollama | 512 токенов | Любая система | ⭐⭐⭐⭐ | ⚡⚡⚡⚡ | 🥈 Без Docker |
| Memory | N/A | Нет | ⭐ | ⚡⚡⚡⭐⭐ | 🔙 Fallback |

---

## 🆕 TEI (Text Embeddings Inference)

**⚠️ ТРЕБУЕТ RTX 30xx/40xx GPU** (Compute Capability 8.0+)
**❌ НЕ РАБОТАЕТ** на GTX 16xx/20xx (Compute Capability 7.5)

### Преимущества

- ✅ **8192 токена контекста** (16x больше чем Ollama!)
- ⚡ Оптимизированная производительность
- 🔒 Локально (без облачных API)
- 🚀 Auto-restart контейнера
- 📦 ~2 GB (образ + модель)

### Минимальные требования GPU

- ✅ RTX 30xx серия (3060, 3070, 3080, 3090)
- ✅ RTX 40xx серия (4060, 4070, 4080, 4090)
- ❌ GTX 16xx серия (1650, 1660)
- ❌ RTX 20xx серия (2060, 2070, 2080)
- ❌ CPU-only системы

### Быстрая установка

**Windows:**
```powershell
.\setup-embeddings-interactive.ps1
# Выберите опцию 1
```

**Unix/macOS/Linux:**
```bash
./setup-embeddings-interactive.sh
# Выберите опцию 1
```

### Что делает скрипт

1. ✅ Проверяет Docker (требуется Docker Desktop)
2. ✅ Проверяет GPU Compute Capability
3. ✅ Скачивает TEI образ (~1 GB)
4. ✅ Скачивает модель `ibm-granite/granite-embedding-english-r2` (~600 MB)
5. ✅ Создает контейнер с auto-restart
6. ✅ Проверяет работоспособность

### Управление контейнером

```bash
# Просмотр логов
docker logs tei-server

# Остановка
docker stop tei-server

# Запуск
docker start tei-server

# Удаление
docker rm -f tei-server

# Статус
docker ps --filter "name=tei-server"
```

### Проверка работы

```bash
# Health check
curl http://localhost:8080/health

# Тест эмбеддинга
curl -X POST http://localhost:8080/embed \
  -H 'Content-Type: application/json' \
  -d '{"inputs": "Hello world"}'
```

---

## 📦 Ollama

Легковесная альтернатива без Docker. Работает на любой системе.

### Преимущества

- ✅ Простая установка (без Docker)
- ✅ Работает на любой системе
- ✅ Multilingual support (русский + английский)
- ⚠️ 512 токенов контекста

### Установка

**Windows:**
```powershell
.\setup-embeddings-interactive.ps1
# Выберите опцию 2
```

**Unix/macOS/Linux:**
```bash
./setup-embeddings-interactive.sh
# Выберите опцию 2
```

**Вручную:**
```bash
# Windows
winget install Ollama.Ollama

# macOS
brew install ollama

# Linux
curl -fsSL https://ollama.com/install.sh | sh
```

### Загрузка модели

```bash
# IBM Granite Embedding (рекомендуется для кода)
ollama pull granite-embedding

# Проверка
ollama list
```

### Управление

```bash
# Запуск сервера
ollama serve

# Список моделей
ollama list

# Скачать модель
ollama pull <model>

# Удалить модель
ollama rm <model>
```

---

## 💾 Memory Provider

Fallback без ML embeddings. Использует детерминированный хеш вместо нейросетей.

### Когда использовать

- ⚠️ Docker и Ollama недоступны
- ⚠️ Нет GPU
- ✅ Быстрая разработка/тестирование
- ✅ Нет зависимостей

### Конфигурация

```json
{
  "Embedding": {
    "Provider": "memory",
    "Enabled": true,
    "Memory": {
      "UseDeterministicHash": true
    }
  }
}
```

---

## ⚙️ Конфигурация

### Auto режим (рекомендуется)

```json
{
  "Embedding": {
    "Provider": "auto",           // Автодетект GPU → выбор provider
    "Enabled": true,
    "AutoDetectGPU": true
  }
}
```

**Логика автодетекта:**
1. Проверяет GPU через `nvidia-smi`
2. Определяет Compute Capability
3. Выбирает provider:
   - CC >= 8.0 → TEI (8192 tokens)
   - CC < 8.0 или нет NVIDIA → Ollama (512 tokens)
   - Fallback → Memory (no ML)

### Явное указание provider

**TEI:**
```json
{
  "Embedding": {
    "Provider": "tei",
    "Enabled": true,
    "TEI": {
      "BaseUrl": "http://127.0.0.1:8080",
      "Model": "ibm-granite/granite-embedding-english-r2",
      "TimeoutMs": 30000,
      "Concurrency": 4,
      "CheckServer": true,
      "AutoStart": true,
      "ContainerName": "tei-server"
    }
  }
}
```

**Ollama:**
```json
{
  "Embedding": {
    "Provider": "ollama",
    "Enabled": true,
    "Ollama": {
      "BaseUrl": "http://127.0.0.1:11434",
      "Model": "granite-embedding",
      "TimeoutMs": 10000,
      "Concurrency": 4,
      "AutoPull": true,
      "CheckServer": true
    }
  }
}
```

**Memory:**
```json
{
  "Embedding": {
    "Provider": "memory",
    "Enabled": true,
    "Memory": {
      "UseDeterministicHash": true
    }
  }
}
```

---

## 🔧 Программная конфигурация

### Регистрация в DI

```csharp
using UltraUltrasharpTools.Tools.Extensions;
using UltraUltrasharpTools.Tools.Semantic.Embedding;

// В Startup.cs или Program.cs
services.WithEmbeddingServices(options =>
{
    options.Provider = "auto";
    options.Enabled = true;
    options.AutoDetectGPU = true;

    // Опционально: переопределить настройки
    options.TEI.BaseUrl = "http://localhost:8080";
    options.Ollama.Model = "granite-embedding";
});
```

### Использование в коде

```csharp
using UltraUltrasharpTools.Tools.Semantic.Embedding;

public class MyService
{
    private readonly IEmbeddingProvider _provider;

    public MyService(IEmbeddingProvider provider)
    {
        _provider = provider;
    }

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        // Генерация embedding
        var embedding = await _provider.EmbedAsync(text);

        Console.WriteLine($"Provider: {_provider.Name}");
        Console.WriteLine($"Max context: {_provider.MaxContextTokens} tokens");
        Console.WriteLine($"Dimension: {_provider.Dimension}");

        return embedding;
    }

    public async Task<float[][]> GetEmbeddingsBatchAsync(List<string> texts)
    {
        // Batch генерация (эффективнее для TEI)
        return await _provider.EmbedBatchAsync(texts);
    }
}
```

---

## 🧪 Проверка работы

### Через MCP Server

```bash
dotnet run --project UltrasharpTools.MCPServer
```

**Логи при запуске:**
```
[GPUDetection] Detecting GPU capabilities...
[GPUDetection] CUDA GPU detected: NVIDIA GeForce RTX 3060 (CC 8.6), 12.0 GB → tei (8192 tokens)
[TEI] Initializing with model: ibm-granite/granite-embedding-english-r2
[TEI] Server is healthy
[TEI] Initialized successfully (dimension: 768)
[EmbeddingFactory] Provider initialized: tei (8192 tokens, dimension: 768)
```

### Через код

```csharp
// Тест GPU detection
var gpuService = serviceProvider.GetRequiredService<IGPUDetectionService>();
var gpuInfo = await gpuService.DetectAsync();

Console.WriteLine(gpuInfo);
// Output: NVIDIA GeForce RTX 3060 (CC 8.6), 12.0 GB → tei (8192 tokens)

// Тест embedding provider
var factory = serviceProvider.GetRequiredService<EmbeddingProviderFactory>();
var provider = await factory.CreateAsync();

Console.WriteLine($"Provider: {provider.Name}");
Console.WriteLine($"Max tokens: {provider.MaxContextTokens}");

var embedding = await provider.EmbedAsync("test code");
Console.WriteLine($"Embedding dimension: {embedding.Length}");
```

---

## 🐛 Troubleshooting

### TEI: "Docker not found"

**Проблема:** Docker не установлен или не запущен

**Решение:**
```bash
# Установить Docker Desktop
# Windows/macOS: https://www.docker.com/products/docker-desktop
# Linux: https://docs.docker.com/engine/install/

# Проверка
docker --version
```

### TEI: "Container not compatible with GPU"

**Проблема:** GPU не поддерживает Compute Capability 8.0+

**Решение:**
```bash
# Проверить GPU
nvidia-smi --query-gpu=name,compute_cap --format=csv

# Если CC < 8.0, используйте Ollama
.\setup-embeddings-interactive.ps1  # Выберите опцию 2
```

### Ollama: "Server not available"

**Проблема:** Ollama сервер не запущен

**Решение:**
```bash
# Запустить вручную
ollama serve

# Проверка
curl http://127.0.0.1:11434/api/version
```

### Ollama: "Model not found"

**Проблема:** Модель не установлена

**Решение:**
```bash
# Установить модель
ollama pull granite-embedding

# Проверка
ollama list
```

### Auto-detect: "Fallback to Memory provider"

**Проблема:** Ни TEI, ни Ollama недоступны

**Решение:**
1. Установите Ollama (простейший вариант)
2. Или используйте Memory provider (без ML)
3. Проверьте логи для деталей

---

## 📈 Сравнение производительности

| Сценарий | TEI (8K) | Ollama (512) | Memory |
|----------|----------|--------------|--------|
| **Короткий метод** (~100 токенов) | ⚡⚡⚡⚡⚡ | ⚡⚡⚡⚡⚡ | ⚡⚡⚡⚡⚡ |
| **Средний класс** (~500 токенов) | ⚡⚡⚡⚡⚡ | ✅ Полностью | ⚡⚡⚡⚡⚡ |
| **Большой файл** (~2000 токенов) | ⚡⚡⚡⚡ | ⚠️ Truncated | ⚡⚡⚡⚡⚡ |
| **Целый проект** (~8000 токенов) | ✅ Полностью | ❌ Truncated | ⚡⚡⚡⚡⚡ |
| **Качество embeddings** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | ⭐ |
| **Семантический поиск** | ✅ Отлично | ✅ Хорошо | ⚠️ Базовый |

**Вывод:**
- Для **production**: TEI (если есть RTX 30xx+)
- Для **разработки**: Ollama (проще установка)
- Для **тестирования**: Memory (нет зависимостей)

---

## 🚀 Дальнейшие шаги

После установки embeddings:

1. **Семантический поиск кода**
   - Поиск похожих методов/классов
   - Обнаружение дубликатов
   - Code smell detection

2. **Refactoring suggestions**
   - Автоматические рекомендации по улучшению
   - Поиск паттернов для рефакторинга

3. **Knowledge base**
   - Индексация всей кодовой базы
   - Быстрый поиск по смыслу запроса

См. документацию **SEMANTIC_SEARCH.md** (coming soon) для деталей.

---

## 📚 Дополнительные ресурсы

- [IBM Granite Models](https://github.com/ibm-granite/granite-embedding-models)
- [TEI Documentation](https://huggingface.co/docs/text-embeddings-inference)
- [Ollama Documentation](https://ollama.com/docs)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)

---

## 📝 Changelog

- **2025-01-15:** Добавлен TEI provider (8192 tokens)
- **2025-01-15:** Добавлен auto-detect GPU
- **2025-01-15:** Интерактивные скрипты установки
- **2025-01-15:** Ollama и Memory providers
