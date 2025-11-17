# Fast Tokenizer Converter for TEI

Автоматическое решение для конвертации "slow" токенизаторов в "fast" токенизаторы для совместимости с TEI (Text Embeddings Inference).

## Проблема

TEI требует **fast tokenizer** (tokenizer.json), но некоторые модели на HuggingFace имеют только **slow tokenizer** (tokenizer_config.json + vocab files). При попытке запустить такую модель в TEI возникает ошибка.

## Решение

Используйте скрипты конвертации для автоматического преобразования slow → fast токенизатора.

## Установка зависимостей

```bash
pip install transformers
```

## Использование

### Python (кросс-платформенный)

```bash
python convert-tokenizer-to-fast.py <model-id> [-o output_dir]
```

**Примеры:**

```bash
# Конвертация granite-embedding
python convert-tokenizer-to-fast.py ibm-granite/granite-embedding-125m-english

# С указанием выходной директории
python convert-tokenizer-to-fast.py sentence-transformers/all-MiniLM-L6-v2 -o ./my_fast_tokenizer
```

### PowerShell (Windows)

```powershell
.\convert-tokenizer-to-fast.ps1 -ModelId "ibm-granite/granite-embedding-125m-english"

# С указанием выходной директории
.\convert-tokenizer-to-fast.ps1 -ModelId "model-id" -OutputDir "C:\path\to\output"
```

PowerShell скрипт автоматически:
- Проверит наличие Python
- Установит transformers если нужно
- Запустит конвертацию

## Использование сконвертированного токенизатора с TEI

После конвертации у вас есть 2 варианта:

### Вариант 1: Загрузить на HuggingFace Hub

```bash
huggingface-cli upload your-username/model-name-fast ./output_dir
```

Затем запустить TEI:

```bash
docker run ... --model-id your-username/model-name-fast
```

### Вариант 2: Использовать локально

```bash
docker run -v /path/to/output_dir:/model \
  ghcr.io/huggingface/text-embeddings-inference:cpu-1.8.3 \
  --model-id /model
```

## Выходные файлы

После успешной конвертации в выходной директории будут:

- ✅ `tokenizer.json` - fast tokenizer (главный файл!)
- ✅ `tokenizer_config.json` - конфигурация
- ✅ `special_tokens_map.json` - специальные токены
- ✅ `vocab.txt` / `vocab.json` - словарь (зависит от модели)

## Устранение неполадок

### "This model may not support fast tokenization"

Некоторые модели (особенно очень старые или специфичные) не поддерживают fast токенизацию. В этом случае:
- Используйте другую модель
- Или используйте Ollama вместо TEI

### "transformers library not installed"

Установите:
```bash
pip install transformers
```

## Поддерживаемые модели

✅ Большинство современных моделей на HuggingFace
✅ BERT, RoBERTa, DistilBERT, Albert
✅ sentence-transformers модели
✅ Granite embedding модели

❌ Некоторые очень старые или кастомные модели

## Примеры использования

### Сценарий: Модель выдает ошибку в TEI

```bash
# 1. Проверьте ошибку в логах
docker logs tei-server

# 2. Если ошибка связана с токенизатором, конвертируйте:
python convert-tokenizer-to-fast.py your-model-id

# 3. Перезапустите TEI с локальной моделью:
docker stop tei-server && docker rm tei-server
docker run -v $(pwd)/your-model-id_fast:/model \
  -p 8080:80 \
  ghcr.io/huggingface/text-embeddings-inference:cpu-1.8.3 \
  --model-id /model
```

## Альтернативы

Если конвертация не работает, рассмотрите:

1. **Ollama** - поддерживает больше моделей, проще в использовании
2. **Другая модель** - используйте sentence-transformers/all-MiniLM-L6-v2
3. **Собственный inference** - напишите свой сервис на Python + transformers
