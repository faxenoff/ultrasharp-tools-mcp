# Code Models Selection Guide

**Лучшие модели для semantic code analysis на мощной GPU конфигурации (48GB VRAM, 256GB RAM).**

---

## 🎯 Два типа моделей

Для полноценного AI Agent нам нужны **ДВА типа** моделей:

### 1. **Embedding Models** (векторизация для семантического поиска)
- Преобразуют код в vectors
- Используются для find_duplicates, semantic search
- Быстрые (inference < 100ms per text)
- Малый размер (< 2GB)

### 2. **LLM Models** (AI анализ, генерация, reasoning)
- Анализируют код, находят паттерны, генерируют рекомендации
- Используются для AI Agent (vulnerability scan, quality optimizer, etc.)
- Медленные (inference 1-10s per request)
- Большой размер (7B-70B parameters)

**На вашей конфигурации (48GB GPU) можно запустить:**
- ✅ Несколько embedding models одновременно
- ✅ 1-2 крупных LLM (34B-70B)
- ✅ Или много маленьких моделей (7B-13B)

---

## 📦 Part 1: Embedding Models (для Semantic Search)

### Топ-5 моделей для code embeddings

| Model | Parameters | Dimensions | VRAM | Inference Speed | Best for |
|-------|------------|------------|------|-----------------|----------|
| **Salesforce/codet5p-110m-embedding** | 110M | 256 | ~0.5GB | ⚡ 2000 emb/sec | ✅ Fast, good quality |
| **microsoft/unixcoder-base** | 125M | 768 | ~0.6GB | ⚡ 1500 emb/sec | ✅ Multi-language |
| **microsoft/graphcodebert-base** | 125M | 768 | ~0.6GB | ⚡ 1500 emb/sec | ✅ Graph-aware |
| **BAAI/bge-large-en-v1.5** | 335M | 1024 | ~1.5GB | 🟡 600 emb/sec | General purpose |
| **sentence-transformers/all-mpnet-base-v2** | 110M | 768 | ~0.5GB | ⚡ 1800 emb/sec | Multilingual |

---

### 🥇 Recommended: Salesforce CodeT5+ Embedding

**Model:** `Salesforce/codet5p-110m-embedding`

**Почему лучшая для кода:**
- ✅ Специально обучена на code (6+ языков: Python, Java, JavaScript, Go, Ruby, PHP)
- ✅ Понимает code structure, syntax, semantics
- ✅ Отличная precision для code similarity
- ✅ Быстрая (2000 embeddings/second на GPU)
- ✅ Малый размер (110M params → ~0.5GB VRAM)

**Benchmark results (CodeXGLUE):**

| Task | CodeT5+ | UniXcoder | GraphCodeBERT |
|------|---------|-----------|---------------|
| Code search | **0.876** | 0.834 | 0.842 |
| Clone detection | **0.942** | 0.921 | 0.935 |
| Code summarization | **0.812** | 0.789 | 0.801 |

**Setup с TEI:**

```bash
docker run -d \
  --name tei-codet5p \
  --gpus all \
  -p 8080:8080 \
  -v /data/models:/data \
  ghcr.io/huggingface/text-embeddings-inference:latest \
    --model-id Salesforce/codet5p-110m-embedding \
    --port 8080 \
    --max-batch-size 256 \
    --max-concurrent-requests 1024
```

**Performance на вашей GPU (A100 48GB):**
```
Throughput: ~2000-3000 embeddings/sec
Latency: ~5-10ms per batch (32 inputs)
VRAM usage: ~0.5GB
CPU RAM: ~2GB

Можно запустить 10+ instances в parallel!
```

---

### 🥈 Alternative: UniXcoder (универсальный)

**Model:** `microsoft/unixcoder-base`

**Преимущества:**
- ✅ 6 programming languages
- ✅ Pre-trained на 4.2M code snippets
- ✅ Good для cross-language search

**Setup:**

```bash
docker run -d \
  --name tei-unixcoder \
  --gpus all \
  -p 8081:8080 \
  ghcr.io/huggingface/text-embeddings-inference:latest \
    --model-id microsoft/unixcoder-base \
    --port 8080 \
    --max-batch-size 128
```

---

### 🥉 Specialized: GraphCodeBERT (graph-aware)

**Model:** `microsoft/graphcodebert-base`

**Уникальность:**
- ✅ Понимает **control flow graph** (CFG)
- ✅ Учитывает data flow
- ✅ Best для структурного анализа

**Use case:** Когда важна структура кода, не только semantics.

---

### 💡 Multi-Model Strategy (рекомендуется!)

**На 48GB GPU запустите ВСЕ ТРИ модели параллельно:**

```yaml
# docker-compose.yml
services:
  tei-codet5p:
    image: ghcr.io/huggingface/text-embeddings-inference:latest
    command: --model-id Salesforce/codet5p-110m-embedding --port 8080
    ports: ["8080:8080"]
    deploy:
      resources:
        reservations:
          devices:
            - capabilities: [gpu]
              device_ids: ['0']  # Same GPU!

  tei-unixcoder:
    image: ghcr.io/huggingface/text-embeddings-inference:latest
    command: --model-id microsoft/unixcoder-base --port 8080
    ports: ["8081:8080"]  # Разные порты!
    deploy:
      resources:
        reservations:
          devices:
            - capabilities: [gpu]
              device_ids: ['0']  # Same GPU!

  tei-graphcodebert:
    image: ghcr.io/huggingface/text-embeddings-inference:latest
    command: --model-id microsoft/graphcodebert-base --port 8080
    ports: ["8082:8080"]
    deploy:
      resources:
        reservations:
          devices:
            - capabilities: [gpu]
              device_ids: ['0']  # Same GPU!
```

**Total VRAM:** ~1.7GB (все три модели!)
**Остаётся:** 48GB - 1.7GB = **46.3GB для LLM!**

**Зачем три модели?**

```csharp
// Ensemble approach для better accuracy
public async Task<List<Match>> FindDuplicatesEnsemble(string code)
{
    // Get embeddings from all 3 models
    var tasks = new[]
    {
        _codet5pService.GetEmbedding(code),
        _unixcoderService.GetEmbedding(code),
        _graphcodebertService.GetEmbedding(code)
    };

    var embeddings = await Task.WhenAll(tasks);

    // Search with each embedding
    var results = await SearchWithAllEmbeddings(embeddings);

    // Rank by consensus (код найденный всеми тремя = высокий confidence!)
    return results
        .GroupBy(r => r.Code)
        .Where(g => g.Count() >= 2)  // Минимум 2 модели согласны
        .OrderByDescending(g => g.Count())
        .SelectMany(g => g)
        .ToList();
}
```

**Результат:** Более точный semantic search (ensemble voting)!

---

## 🧠 Part 2: LLM Models (для AI Agent)

### Для 48GB GPU - лучшие code LLMs

| Model | Parameters | Quantization | VRAM | Context | Best for |
|-------|------------|--------------|------|---------|----------|
| **DeepSeek-Coder-V2-Lite-Instruct** | 16B | FP16 | ~32GB | 128K | ✅ Best quality/size |
| **CodeLlama-34B-Instruct** | 34B | 4-bit | ~20GB | 16K | Code generation |
| **WizardCoder-Python-34B** | 34B | 4-bit | ~20GB | 16K | Python expert |
| **DeepSeek-Coder-33B-Instruct** | 33B | 4-bit | ~19GB | 16K | General code |
| **Phind-CodeLlama-34B-v2** | 34B | 4-bit | ~20GB | 16K | Code understanding |
| **CodeLlama-70B-Instruct** | 70B | 4-bit | ~40GB | 16K | ✅ Best overall |

---

### 🥇 Recommended: DeepSeek-Coder-V2-Lite-Instruct 16B

**Model:** `deepseek-ai/DeepSeek-Coder-V2-Lite-Instruct`

**Почему лучшая:**
- ✅ **128K context window!** - может анализировать целые файлы
- ✅ Обучена на 6 trillion tokens code
- ✅ Поддержка 338 programming languages
- ✅ Fill-in-Middle (FIM) для code completion
- ✅ Отличная reasoning способность

**Benchmark (HumanEval):**

| Model | HumanEval Pass@1 | MBPP Pass@1 |
|-------|------------------|-------------|
| **DeepSeek-Coder-V2-Lite-16B** | **81.1%** | **70.2%** |
| CodeLlama-34B | 76.4% | 64.1% |
| WizardCoder-34B | 73.2% | 61.8% |
| GPT-3.5-Turbo | 72.4% | 65.7% |

**VRAM usage:**
- FP16 (full precision): ~32GB
- INT8 (quantized): ~16GB
- INT4 (quantized): ~10GB

**Setup с Ollama:**

```bash
# Pull model
ollama pull deepseek-coder-v2:16b-lite-instruct-q4_K_M

# Run
ollama run deepseek-coder-v2:16b-lite-instruct-q4_K_M

# Test
curl http://localhost:11434/api/generate -d '{
  "model": "deepseek-coder-v2:16b-lite-instruct-q4_K_M",
  "prompt": "Analyze this C# code for vulnerabilities:\npublic void Login(string username, string password) { var sql = $\"SELECT * FROM Users WHERE Username = \\'{username}\\' AND Password = \\'{password}\\'\"; }"
}'
```

**Response example:**
```
This code has a critical SQL injection vulnerability (CWE-89).

The username and password are directly concatenated into the SQL query string,
allowing an attacker to inject malicious SQL code.

Recommendation:
Use parameterized queries:
var sql = "SELECT * FROM Users WHERE Username = @username AND Password = @password";
command.Parameters.AddWithValue("@username", username);
command.Parameters.AddWithValue("@password", password);
```

**Performance на A100 48GB:**
```
Quantization: INT4
VRAM: ~10GB
Inference: ~50 tokens/sec
Latency: ~2-4s для analysis (100-200 tokens output)
```

---

### 🥈 Alternative: CodeLlama-70B-Instruct (максимальное качество)

**Model:** `codellama/CodeLlama-70b-Instruct-hf`

**Преимущества:**
- ✅ Самая большая CodeLlama модель
- ✅ Best для сложного reasoning
- ✅ Обучена Meta на 2 trillion tokens

**VRAM:**
- INT4: ~40GB ✅ Влезает!
- INT8: ~70GB ❌ Не влезет

**Setup с vLLM (fastest inference):**

```bash
docker run -d \
  --name vllm-codellama70b \
  --gpus all \
  -p 8000:8000 \
  -v /data/models:/models \
  vllm/vllm-openai:latest \
    --model codellama/CodeLlama-70b-Instruct-hf \
    --quantization awq \
    --dtype half \
    --max-model-len 4096 \
    --gpu-memory-utilization 0.95
```

**API compatible с OpenAI:**

```csharp
var client = new HttpClient { BaseAddress = new Uri("http://localhost:8000") };

var response = await client.PostAsJsonAsync("/v1/chat/completions", new
{
    model = "codellama/CodeLlama-70b-Instruct-hf",
    messages = new[]
    {
        new { role = "user", content = "Analyze this code for performance issues..." }
    },
    temperature = 0.1,
    max_tokens = 500
});
```

**Performance:**
```
VRAM: ~40GB (INT4)
Inference: ~30 tokens/sec
Latency: ~3-6s для analysis
```

---

### 💡 Recommended Setup для 48GB GPU

**Option A: Balanced (embeddings + medium LLM)**

```
Embeddings (3 models): ~1.7GB
DeepSeek-Coder-V2-Lite 16B (INT4): ~10GB
Total: ~12GB / 48GB

Remaining: 36GB free (для future expansion!)
```

**Option B: Maximum Quality (embeddings + large LLM)**

```
Embeddings (3 models): ~1.7GB
CodeLlama-70B (INT4): ~40GB
Total: ~42GB / 48GB

Utilization: 87% (почти полная!)
```

**Option C: Multi-LLM (embeddings + 2 LLMs)**

```
Embeddings (3 models): ~1.7GB
DeepSeek-Coder-V2-Lite 16B (INT4): ~10GB
CodeLlama-34B (INT4): ~20GB
Total: ~32GB / 48GB

Use case: Разные LLMs для разных задач!
- DeepSeek для analysis (faster, 128K context)
- CodeLlama для generation (better quality)
```

---

## 🚀 Production Setup Example

### docker-compose.yml (всё в одном!)

```yaml
version: '3.8'

services:
  # ========== Embedding Models ==========

  tei-codet5p:
    image: ghcr.io/huggingface/text-embeddings-inference:latest
    command:
      - --model-id=Salesforce/codet5p-110m-embedding
      - --port=8080
      - --max-batch-size=256
    ports: ["8080:8080"]
    deploy:
      resources:
        reservations:
          devices:
            - capabilities: [gpu]
              device_ids: ['0']

  tei-unixcoder:
    image: ghcr.io/huggingface/text-embeddings-inference:latest
    command:
      - --model-id=microsoft/unixcoder-base
      - --port=8080
      - --max-batch-size=128
    ports: ["8081:8080"]
    deploy:
      resources:
        reservations:
          devices:
            - capabilities: [gpu]
              device_ids: ['0']

  tei-graphcodebert:
    image: ghcr.io/huggingface/text-embeddings-inference:latest
    command:
      - --model-id=microsoft/graphcodebert-base
      - --port=8080
      - --max-batch-size=128
    ports: ["8082:8080"]
    deploy:
      resources:
        reservations:
          devices:
            - capabilities: [gpu]
              device_ids: ['0']

  # ========== LLM Model ==========

  vllm-deepseek:
    image: vllm/vllm-openai:latest
    command:
      - --model=deepseek-ai/DeepSeek-Coder-V2-Lite-Instruct
      - --quantization=awq
      - --dtype=half
      - --max-model-len=8192  # 128K too slow для production
      - --gpu-memory-utilization=0.20  # Только 20% GPU (10GB)
    ports: ["8000:8000"]
    volumes:
      - /data/models:/root/.cache/huggingface
    deploy:
      resources:
        reservations:
          devices:
            - capabilities: [gpu]
              device_ids: ['0']  # Same GPU!

  # ========== Overlord ==========

  ultrasharp-server:
    image: ultrasharp-tools-server:latest
    ports: ["3001:3001"]
    environment:
      - EMBEDDING_CODET5P_URL=http://tei-codet5p:8080
      - EMBEDDING_UNIXCODER_URL=http://tei-unixcoder:8080
      - EMBEDDING_GRAPHCODEBERT_URL=http://tei-graphcodebert:8080
      - LLM_DEEPSEEK_URL=http://vllm-deepseek:8000
    depends_on:
      - tei-codet5p
      - tei-unixcoder
      - tei-graphcodebert
      - vllm-deepseek
```

**Total resource usage:**
```
Embeddings (3x): ~1.7GB VRAM
LLM (DeepSeek 16B): ~10GB VRAM
Total: ~12GB / 48GB (25% utilization)

Можно добавить ещё модели!
```

---

## 📊 Model Selection Decision Tree

```
┌─ Нужна только векторизация (semantic search)?
│  └─ YES → CodeT5+ Embedding (~0.5GB)
│
├─ Нужен AI анализ кода?
│  ├─ YES, простой анализ (vulnerability scan)
│  │  └─ DeepSeek-Coder-V2-Lite 16B (~10GB)
│  │
│  └─ YES, сложный reasoning (architecture suggestions)
│     └─ CodeLlama-70B (~40GB)
│
├─ Ограничение по VRAM?
│  ├─ < 10GB → CodeT5+ Embedding only
│  ├─ 10-20GB → CodeT5+ + DeepSeek 16B
│  ├─ 20-40GB → CodeT5+ + CodeLlama 34B
│  └─ 40-48GB → All models + CodeLlama 70B
│
└─ Нужна максимальная точность?
   └─ Ensemble: 3x Embeddings + CodeLlama 70B
```

---

## 🎯 Recommended Configuration для вашей GPU (48GB)

### **Option A: Production Ready (рекомендуется!)**

```yaml
Embeddings:
  - CodeT5+ 110M (~0.5GB)
  - UniXcoder 125M (~0.6GB)
  - GraphCodeBERT 125M (~0.6GB)
  Total: ~1.7GB

LLM:
  - DeepSeek-Coder-V2-Lite 16B (INT4, ~10GB)

Total VRAM: ~12GB / 48GB
Free: ~36GB (для будущего scaling!)

Capabilities:
✅ Ensemble semantic search (best accuracy)
✅ AI code analysis (vulnerabilities, quality)
✅ 128K context window
✅ Fast inference (50 tokens/sec)
```

---

### **Option B: Maximum Quality**

```yaml
Embeddings:
  - CodeT5+ 110M (~0.5GB)
  - UniXcoder 125M (~0.6GB)

LLM:
  - CodeLlama-70B (INT4, ~40GB)

Total VRAM: ~41GB / 48GB
Utilization: 85%

Capabilities:
✅ Best LLM reasoning
✅ Slower inference (~30 tokens/sec)
✅ Highest quality analysis
```

---

### **Option C: Multi-Task Specialist**

```yaml
Embeddings:
  - CodeT5+ 110M (~0.5GB)

LLMs:
  - DeepSeek-Coder 16B (analysis, ~10GB)
  - CodeLlama-34B (generation, ~20GB)
  - Mistral-7B-Instruct (general, ~5GB)

Total VRAM: ~36GB / 48GB

Capabilities:
✅ Специализированные модели для разных задач
✅ Analysis → DeepSeek (fast, 128K context)
✅ Generation → CodeLlama (high quality)
✅ General reasoning → Mistral
```

---

## 📖 Вывод

**Для ultrasharp-tools Overlord с 48GB GPU:**

**Embeddings (обязательно):**
- ✅ **CodeT5+ 110M** - лучшая для code search
- ✅ **UniXcoder** - универсальная
- ✅ **GraphCodeBERT** - структурный анализ

**LLM (выберите один):**
- ✅ **DeepSeek-Coder-V2-Lite 16B** - best balance (рекомендуется!)
- ✅ **CodeLlama-70B** - maximum quality
- ✅ **Multi-LLM setup** - разные задачи

**Setup time:** ~2-3 часа (download models + configuration)

**Готов создать deployment конфигурации?**
