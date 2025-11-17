# Docker Setup для RAG Embeddings

Этот файл содержит инструкции по настройке embedding сервисов (TEI и Ollama) через Docker Compose.

**📄 Конфигурация:** [docker-compose.yml](docker-compose.yml) - находится в этой же директории

## Сервисы

### TEI (Text Embeddings Inference)
- **URL**: http://127.0.0.1:8080
- **Модель**: ibm-granite/granite-3.0-embedding-125m (768 dims)
- **Производительность**: Самый быстрый (Rust-based)
- **Use case**: Production, большие кодовые базы

### TEI Small (опциональный)
- **URL**: http://127.0.0.1:8081
- **Модель**: ibm-granite/granite-3.0-embedding-30m (384 dims)
- **Производительность**: Ещё быстрее, но меньше качество
- **Use case**: Быстрые эксперименты, малые проекты

### Ollama
- **URL**: http://127.0.0.1:11434
- **Модель**: ibm/granite-embedding:278m (768 dims)
- **Use case**: Легкая установка, model management

## Быстрый старт

**Важно:** Все команды выполняются из директории `Run.Docs/Setup/` где находится файл `docker-compose.yml`.

```bash
cd Run.Docs/Setup
```

### 1. Запустить только TEI (рекомендуется)
```bash
docker-compose up -d tei
```

### 2. Запустить только Ollama
```bash
docker-compose up -d ollama

# Загрузить модель Granite
docker-compose --profile setup up ollama-puller
```

### 3. Запустить всё
```bash
docker-compose up -d

# Загрузить Ollama модели
docker-compose --profile setup up ollama-puller
```

### 4. Запустить быструю TEI модель
```bash
docker-compose --profile fast up -d tei-small
```

## Проверка работы

### TEI Health Check
```bash
curl http://127.0.0.1:8080/health
# Ожидается: {"status":"ok"}
```

### TEI Model Info
```bash
curl http://127.0.0.1:8080/info
# Вернёт информацию о модели (dimension, max_length, и т.д.)
```

### TEI Embedding Test
```bash
curl -X POST http://127.0.0.1:8080/embed \
  -H "Content-Type: application/json" \
  -d '{"inputs": ["Hello world", "Test embedding"]}'
# Вернёт массив векторов
```

### Ollama Health Check
```bash
curl http://127.0.0.1:11434/api/tags
# Вернёт список моделей
```

### Ollama Embedding Test
```bash
curl -X POST http://127.0.0.1:11434/api/embed \
  -H "Content-Type: application/json" \
  -d '{"model": "ibm/granite-embedding:278m", "input": ["Hello world"]}'
# Вернёт embedding vector
```

## Управление

### Просмотр логов
```bash
# TEI
docker-compose logs -f tei

# Ollama
docker-compose logs -f ollama
```

### Остановка сервисов
```bash
docker-compose down
```

### Полное удаление (с данными)
```bash
docker-compose down -v
```

## Производительность

### Сравнение моделей

| Модель | Dimension | Latency (1 text) | Latency (batch 16) | Quality |
|--------|-----------|------------------|-------------------|---------|
| Granite 30M (TEI) | 384 | ~5ms | ~20ms | Good |
| Granite 125M (TEI) | 768 | ~10ms | ~40ms | Better |
| Granite 278M (Ollama) | 768 | ~50ms | ~200ms | Best |

### Рекомендации

**Малые проекты (<1K файлов):**
- TEI Small (granite-30m) - fastest
- Memory Provider - no Docker required

**Средние проекты (1K-10K файлов):**
- TEI (granite-125m) - balanced

**Большие проекты (>10K файлов):**
- TEI (granite-125m) - production-ready
- Ollama (granite-278m) - best quality

## GPU Support (опционально)

Для включения GPU поддержки раскомментируйте секции `deploy` в docker-compose.yml:

```yaml
deploy:
  resources:
    reservations:
      devices:
        - driver: nvidia
          count: 1
          capabilities: [gpu]
```

Требования:
- NVIDIA GPU
- NVIDIA Docker runtime установлен
- CUDA drivers

## Troubleshooting

### TEI не стартует
```bash
# Проверить логи
docker-compose logs tei

# Возможные причины:
# 1. Недостаточно памяти (требуется ~2GB для Granite 125M)
# 2. Не скачалась модель (первый запуск может занять время)
```

### Ollama модель не загружается
```bash
# Вручную загрузить модель
docker exec -it sharptools-ollama ollama pull ibm/granite-embedding:278m

# Проверить список моделей
docker exec -it sharptools-ollama ollama list
```

### Порты заняты
Измените порты в docker-compose.yml:
```yaml
ports:
  - "8090:80"  # вместо 8080:80
```

## Auto-detection в SharpTools

SharpTools автоматически выбирает provider в следующем порядке:
1. **TEI** (http://127.0.0.1:8080) - если доступен
2. **Ollama** (http://127.0.0.1:11434) - если доступен
3. **Memory** - fallback (без ML)

Для принудительного выбора используйте конфигурацию:
```csharp
var config = new EmbeddingProviderFactoryConfig
{
    ProviderType = EmbeddingProviderType.TEI,
    EnableOllama = false,
    EnableMemory = false
};
```

## Полезные команды

### Перезапуск после изменения конфигурации
```bash
docker-compose up -d --force-recreate
```

### Посмотреть использование ресурсов
```bash
docker stats sharptools-tei sharptools-ollama
```

### Очистить unused volumes
```bash
docker volume prune
```
