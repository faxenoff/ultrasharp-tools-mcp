# Embedding Models Guide for Code Search

> Анализ лучших embedding моделей для semantic code search (2025)

## TEI - Поддерживаемые архитектуры

TEI поддерживает: **BERT, ModernBERT, NomicBERT, JinaBERT (ALiBi), XLM-RoBERTa, MPNet, Mistral, Alibaba GTE, Qwen2/Qwen3, Gemma3**

## Рекомендации для TEI

### Jina Embeddings V2 Base Code (Специализированная для кода)

| Параметр | Значение |
|----------|----------|
| ID | `jinaai/jina-embeddings-v2-base-code` |
| Размер | 161M параметров |
| Размерность | 768 |
| Контекст | 8192 токенов |
| Языки | English + 30 языков программирования |
| Архитектура | JinaBERT (ALiBi) - TEI совместим |

**Преимущества:**
- Специально обучена на 150M+ пар код-документация
- Python, JavaScript, Java, C++, Rust, Go, C#, TypeScript, SQL и др.
- Длинный контекст 8K - идеально для больших методов

### GTE ModernBERT Base (Универсальная + код)

| Параметр | Значение |
|----------|----------|
| ID | `Alibaba-NLP/gte-modernbert-base` |
| Размер | ~150M параметров |
| Размерность | 768 |
| Контекст | 8192 токенов |
| CoIR Score | 79.31 ndcg@10 |
| Архитектура | ModernBERT - TEI совместим |

**Преимущества:**
- Токенизатор специально обучен на коде (OLMo)
- 2-3x быстрее других моделей на длинных текстах
- Flash Attention 2 - высокая производительность

### Nomic Embed Text v1.5 (Универсальная)

| Параметр | Значение |
|----------|----------|
| ID | `nomic-ai/nomic-embed-text-v1.5` |
| Размер | 137M параметров |
| Размерность | 768 |
| Контекст | 8192 токенов |
| Архитектура | NomicBERT - TEI совместим |

### Быстрые варианты для TEI

| Модель | Размер | Dim | Контекст |
|--------|--------|-----|----------|
| `sentence-transformers/all-MiniLM-L6-v2` | 22M | 384 | 512 |
| `BAAI/bge-small-en-v1.5` | 33M | 384 | 512 |
| `intfloat/e5-small-v2` | 33M | 384 | 512 |

## Рекомендации для Ollama

### Nomic Embed Text (Универсальная)

| Параметр | Значение |
|----------|----------|
| ID | `nomic-embed-text` |
| Размер | 274 MB |
| Размерность | 768 |
| Контекст | 8192 токенов |

**Преимущества:**
- Превосходит OpenAI text-embedding-ada-002
- Отличная работа с длинными документами
- Полностью open source

### Snowflake Arctic Embed 2 (Техническая документация)

| Параметр | Значение |
|----------|----------|
| ID | `snowflake-arctic-embed2` |
| Размер | ~500 MB |
| Размерность | 1024 |
| Контекст | 8192 токенов |

**Преимущества:**
- Специализация на технической терминологии
- Matryoshka Representation Learning (MRL)
- Enterprise-ready производительность

### MxBai Embed Large (Качество)

| Параметр | Значение |
|----------|----------|
| ID | `mxbai-embed-large` |
| Размер | 669 MB |
| Размерность | 1024 |
| Контекст | 512 токенов |

**Преимущества:**
- Превосходит text-embedding-3-large
- Лучше на контекстно-зависимых запросах

### Быстрые варианты для Ollama

| Модель | Размер | Dim | Контекст |
|--------|--------|-----|----------|
| `all-minilm` | 45 MB | 384 | 512 |
| `snowflake-arctic-embed:s` | 67 MB | 384 | 512 |

## Выбор модели по размеру контекста

### Что индексируется в UltraSharp MCP

Semantic инструменты индексируют:
1. **Методы** — полное тело метода
2. **Классы** — определение класса

### Статистика типичного C# кода

| Элемент | Типичный размер | Токены (~4 символа) |
|---------|-----------------|---------------------|
| Короткий метод (10-30 строк) | 200-600 символов | 50-150 токенов |
| Средний метод (30-100 строк) | 600-2000 символов | 150-500 токенов |
| Большой метод (100-300 строк) | 2000-6000 символов | 500-1500 токенов |
| Очень большой метод (300+ строк) | 6000+ символов | 1500+ токенов |
| Класс целиком | 2000-20000 символов | 500-5000 токенов |

### Когда достаточно 512 токенов

- `semantic_search` — поиск **коротких методов**
- `detect_code_clones` — поиск дубликатов (обычно дублируют короткие фрагменты)
- Проекты с хорошим code style (методы < 50 строк)
- **Быстрая** индексация для разработки

### Когда нужно 8K токенов

- `semantic_search` — поиск **любых методов** без потери контекста
- `semantic_diff` — сравнение **больших рефакторингов**
- Legacy код с длинными методами
- Индексация **целых классов** (не только методов)
- **Production** использование

### Рекомендации по сценариям

| Сценарий | TEI | Ollama |
|----------|-----|--------|
| **Production** (качество) | `jina-code-v2` (8K, для кода) | `nomic-embed-text` (8K) |
| **Разработка** (скорость) | `all-minilm-l6-v2` (256) | `all-minilm` (512) |
| **Большие проекты** | `gte-modernbert` (8K, быстрая) | `snowflake-arctic-embed2` (8K) |

> **Вывод**: Для UltraSharp рекомендуются **8K модели**, так как code search часто ищет сложные методы (которые длинные), а truncation при 512 токенах теряет важный контекст в конце метода.

## GPU Совместимость

### TEI
- **RTX 30xx/40xx** (Ampere/Ada): Полная поддержка GPU
- **RTX 50xx** (Blackwell): Только CPU режим (compute cap 12.0 vs требуемый 8.0)

### Ollama
- **Все GPU включая RTX 50xx** (Blackwell): Полная поддержка

## Итоговые рекомендации

### Для code search (TEI):
1. `jinaai/jina-embeddings-v2-base-code` - Лучший для кода
2. `Alibaba-NLP/gte-modernbert-base` - Быстрый + код
3. `nomic-ai/nomic-embed-text-v1.5` - Универсальный

### Для code search (Ollama):
1. `nomic-embed-text` - Рекомендуется (8K контекст)
2. `snowflake-arctic-embed2` - Техническая документация
3. `mxbai-embed-large` - Максимальное качество

## Источники

- [TEI Supported Models](https://huggingface.co/docs/text-embeddings-inference/en/supported_models)
- [Jina Embeddings V2 Base Code](https://huggingface.co/jinaai/jina-embeddings-v2-base-code)
- [ModernBERT Introduction](https://huggingface.co/blog/modernbert)
- [6 Best Code Embedding Models](https://modal.com/blog/6-best-code-embedding-models-compared)
- [Ollama Embedding Models](https://ollama.com/blog/embedding-models)
- [MTEB Leaderboard](https://huggingface.co/spaces/mteb/leaderboard)
