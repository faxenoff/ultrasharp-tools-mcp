# GPU & Model Deployment Options

**Варианты размещения embedding модели и GPU инфраструктуры.**

---

## 🎯 Обзор вариантов

Модель (TEI - Text Embeddings Inference) и GPU можно развернуть несколькими способами:

```
┌─────────────────────────────────────────────────────────────┐
│ Вариант 1: Co-located (в том же Kubernetes cluster)        │
│                                                             │
│  ┌───────────────────────────────────────────────┐         │
│  │ Kubernetes Cluster                            │         │
│  │                                               │         │
│  │  ┌──────────────┐      ┌──────────────┐      │         │
│  │  │ RemoteServer │ ───► │ TEI Pod      │      │         │
│  │  │ (CPU)        │ HTTP │ (GPU)        │      │         │
│  │  └──────────────┘      └──────────────┘      │         │
│  │                                               │         │
│  │  Внутри cluster - низкая latency!            │         │
│  └───────────────────────────────────────────────┘         │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│ Вариант 2: Separate VM/Server (dedicated inference)        │
│                                                             │
│  ┌──────────────────┐         ┌─────────────────────────┐  │
│  │ Kubernetes       │         │ Отдельная VM/Server     │  │
│  │                  │         │                         │  │
│  │ ┌──────────────┐ │  HTTP   │  ┌─────────────────┐   │  │
│  │ │RemoteServer  │─┼────────►│  │ TEI (standalone)│   │  │
│  │ │(CPU)         │ │  (ext)  │  │ + GPU           │   │  │
│  │ └──────────────┘ │         │  └─────────────────┘   │  │
│  └──────────────────┘         │                         │  │
│                               │  Dedicated GPU ресурсы!  │  │
│                               └─────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│ Вариант 3: Managed Service (внешний API)                   │
│                                                             │
│  ┌──────────────────┐         ┌─────────────────────────┐  │
│  │ Kubernetes       │         │ Managed Service         │  │
│  │                  │         │                         │  │
│  │ ┌──────────────┐ │  HTTPS  │  • Hugging Face         │  │
│  │ │RemoteServer  │─┼────────►│    Inference API        │  │
│  │ │(CPU)         │ │         │  • OpenAI Embeddings    │  │
│  │ └──────────────┘ │         │  • AWS Bedrock          │  │
│  └──────────────────┘         │                         │  │
│                               │  Pay-per-use, no setup!  │  │
│                               └─────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

---

## 📋 Вариант 1: Co-located (Same Kubernetes Cluster)

**Модель и RemoteServer в одном кластере, разные pods.**

### Архитектура

```
┌──────────────────────────────────────────────────────────┐
│ Kubernetes Cluster                                       │
│                                                          │
│  ┌─────────────────────────────────────────────────┐    │
│  │ Namespace: ultrasharp-tools                     │    │
│  │                                                 │    │
│  │  ┌───────────────────┐                          │    │
│  │  │ Pod: ultrasharp-  │                          │    │
│  │  │      server       │                          │    │
│  │  │                   │                          │    │
│  │  │ Container:        │                          │    │
│  │  │ - RemoteServer    │                          │    │
│  │  │   (CPU only)      │                          │    │
│  │  └─────────┬─────────┘                          │    │
│  │            │                                     │    │
│  │            │ HTTP (internal service)            │    │
│  │            ↓                                     │    │
│  │  ┌───────────────────┐                          │    │
│  │  │ Service: tei-svc  │                          │    │
│  │  │ (ClusterIP)       │                          │    │
│  │  │ Port: 8080        │                          │    │
│  │  └─────────┬─────────┘                          │    │
│  │            │                                     │    │
│  │            │ Load balances to pods:             │    │
│  │            ↓                                     │    │
│  │  ┌───────────────────┐    ┌───────────────────┐ │    │
│  │  │ Pod: tei-0        │    │ Pod: tei-1        │ │    │
│  │  │                   │    │                   │ │    │
│  │  │ Container:        │    │ Container:        │ │    │
│  │  │ - TEI             │    │ - TEI             │ │    │
│  │  │ - GPU: 1          │    │ - GPU: 1          │ │    │
│  │  └───────────────────┘    └───────────────────┘ │    │
│  │                                                 │    │
│  └─────────────────────────────────────────────────┘    │
│                                                          │
│  GPU Node Requirements:                                 │
│  - nvidia.com/gpu resource                              │
│  - NVIDIA Container Toolkit installed                   │
└──────────────────────────────────────────────────────────┘
```

### Конфигурация

**TEI Deployment (tei-deployment.yaml):**

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: tei-embedding
  namespace: ultrasharp-tools
spec:
  replicas: 2  # 2 instances на 2 GPUs
  selector:
    matchLabels:
      app: tei-embedding
  template:
    metadata:
      labels:
        app: tei-embedding
    spec:
      # Node selector: только nodes с GPU
      nodeSelector:
        accelerator: nvidia-gpu

      containers:
      - name: tei
        image: ghcr.io/huggingface/text-embeddings-inference:latest

        args:
          - --model-id=BAAI/bge-large-en-v1.5  # Large model, 1024-dim
          - --port=8080
          - --max-batch-size=128
          - --max-concurrent-requests=512

        ports:
        - containerPort: 8080
          name: http

        resources:
          requests:
            memory: "8Gi"
            cpu: "2"
            nvidia.com/gpu: 1  # ← Запрос GPU!
          limits:
            memory: "16Gi"
            cpu: "4"
            nvidia.com/gpu: 1  # ← Limit GPU!

        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 60
          periodSeconds: 10

        readinessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 5

---
apiVersion: v1
kind: Service
metadata:
  name: tei-service
  namespace: ultrasharp-tools
spec:
  type: ClusterIP  # ← Internal service (не доступен снаружи)
  selector:
    app: tei-embedding
  ports:
  - port: 8080
    targetPort: 8080
    name: http
```

**RemoteServer конфигурация:**

```json
// ultrasharp-config.json (в RemoteServer)
{
  "embedding": {
    "provider": "tei",
    "teiUrl": "http://tei-service.ultrasharp-tools.svc.cluster.local:8080",
    "model": "BAAI/bge-large-en-v1.5",
    "dimensions": 1024,
    "maxBatchSize": 128,
    "timeout": "30s"
  }
}
```

**GpuEmbeddingService.cs:**

```csharp
public class GpuEmbeddingService
{
    private readonly string _teiUrl;
    private readonly HttpClient _httpClient;

    public GpuEmbeddingService(IConfiguration config)
    {
        _teiUrl = config["embedding:teiUrl"];
        // "http://tei-service.ultrasharp-tools.svc.cluster.local:8080"

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_teiUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public async Task<float[]> GetEmbedding(string text)
    {
        var response = await _httpClient.PostAsJsonAsync("/embed", new
        {
            inputs = text
        });

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<float[]>();
    }

    public async Task<List<float[]>> GetEmbeddingsBatch(List<string> texts)
    {
        var response = await _httpClient.PostAsJsonAsync("/embed", new
        {
            inputs = texts  // ← Batch request
        });

        return await response.Content.ReadFromJsonAsync<List<float[]>>();
    }
}
```

### Преимущества Варианта 1

✅ **Низкая latency**
- Internal network (ClusterIP)
- No external network hops
- ~1-5ms network latency

✅ **Простая конфигурация**
- Kubernetes service discovery
- Автоматический DNS (tei-service.ultrasharp-tools.svc.cluster.local)
- Load balancing встроен

✅ **Масштабируемость**
- `kubectl scale deployment tei-embedding --replicas=4`
- Автоматический load balancing
- HorizontalPodAutoscaler поддержка

✅ **Безопасность**
- Internal service (ClusterIP)
- Нет external exposure
- Network policies

### Недостатки Варианта 1

⚠️ **Требует GPU nodes в кластере**
- Нужен Kubernetes cluster с GPU nodes
- NVIDIA Container Toolkit на каждом GPU node
- Дороже (GPU-enabled nodes)

⚠️ **Resource contention**
- GPU и CPU pods на одних nodes
- Может быть competition за CPU/memory

⚠️ **Vendor lock-in**
- Привязка к Kubernetes
- Сложнее мигрировать

### Cost Estimate (Вариант 1)

**Kubernetes cluster с GPU:**
```
AWS EKS:
- 2x g4dn.xlarge instances (NVIDIA T4 GPU): ~$0.526/hour * 2 = $1.05/hour
- Total: ~$760/месяц (24/7)

GCP GKE:
- 2x n1-standard-4 + NVIDIA T4: ~$0.50/hour * 2 = $1.00/hour
- Total: ~$720/месяц

Azure AKS:
- 2x NC6s_v3 (NVIDIA V100): ~$1.80/hour * 2 = $3.60/hour
- Total: ~$2,600/месяц (дороже!)
```

**Recommendation:** AWS g4dn.xlarge или GCP T4 (best price/performance).

---

## 📋 Вариант 2: Separate VM/Server (Dedicated Inference)

**Модель на отдельной VM с GPU, RemoteServer в Kubernetes.**

### Архитектура

```
┌────────────────────────────┐       ┌──────────────────────────┐
│ Kubernetes Cluster         │       │ Dedicated GPU Server     │
│                            │       │                          │
│  ┌──────────────────────┐  │       │  ┌────────────────────┐  │
│  │ Pod: ultrasharp-     │  │ HTTP  │  │ TEI (standalone)   │  │
│  │      server          │──┼──────►│  │                    │  │
│  │                      │  │ (ext) │  │ • Model: bge-large │  │
│  │ - RemoteServer (CPU) │  │       │  │ • GPU: NVIDIA T4   │  │
│  └──────────────────────┘  │       │  │ • Port: 8080       │  │
│                            │       │  └────────────────────┘  │
│  No GPU required!          │       │                          │
└────────────────────────────┘       │  OS: Ubuntu 22.04        │
                                     │  Docker + NVIDIA Runtime │
                                     │  Firewall: 8080 open     │
                                     └──────────────────────────┘
```

### Setup отдельной VM

**1. Выбор VM/сервера:**

**Cloud options:**

| Provider | Instance Type | GPU | vCPU | RAM | Price/hour |
|----------|---------------|-----|------|-----|------------|
| AWS | g4dn.xlarge | T4 (16GB) | 4 | 16GB | ~$0.526 |
| GCP | n1-standard-4 + T4 | T4 (16GB) | 4 | 15GB | ~$0.50 |
| Azure | NC6s_v3 | V100 (16GB) | 6 | 112GB | ~$1.80 |
| Hetzner | GPU-GL-M | RTX 4000 (8GB) | 6 | 32GB | ~€0.40 (~$0.45) |

**Recommendation:** Hetzner (самый дешевый!) или GCP T4.

**On-premise option:**
- Собственный сервер с NVIDIA GPU (RTX 3090, RTX 4090, A4000, и т.д.)
- Одноразовые затраты: ~$1500-3000
- Running cost: электричество (~$50-100/месяц)

---

**2. Установка на Ubuntu 22.04:**

```bash
#!/bin/bash
# install-tei-standalone.sh

# Update system
sudo apt update && sudo apt upgrade -y

# Install NVIDIA drivers
sudo apt install -y nvidia-driver-535

# Reboot (required!)
sudo reboot

# After reboot: Install Docker
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh
sudo usermod -aG docker $USER

# Install NVIDIA Container Toolkit
distribution=$(. /etc/os-release;echo $ID$VERSION_ID)
curl -s -L https://nvidia.github.io/nvidia-docker/gpgkey | sudo apt-key add -
curl -s -L https://nvidia.github.io/nvidia-docker/$distribution/nvidia-docker.list | \
  sudo tee /etc/apt/sources.list.d/nvidia-docker.list

sudo apt update
sudo apt install -y nvidia-container-toolkit
sudo systemctl restart docker

# Test GPU access
docker run --rm --gpus all nvidia/cuda:12.0-base nvidia-smi
# ← Should show GPU info!

# Pull TEI image
docker pull ghcr.io/huggingface/text-embeddings-inference:latest

# Create directory for models
mkdir -p /data/models

# Run TEI
docker run -d \
  --name tei-embedding \
  --gpus all \
  -p 8080:8080 \
  -v /data/models:/data \
  --restart unless-stopped \
  ghcr.io/huggingface/text-embeddings-inference:latest \
    --model-id BAAI/bge-large-en-v1.5 \
    --port 8080 \
    --max-batch-size 128 \
    --max-concurrent-requests 512

# Check logs
docker logs -f tei-embedding

# Test endpoint
curl http://localhost:8080/health
# {"status":"healthy"}

# Test embedding
curl -X POST http://localhost:8080/embed \
  -H "Content-Type: application/json" \
  -d '{"inputs": "Hello world"}'
# [0.123, 0.456, ..., 0.789]  ← 1024 floats
```

---

**3. Firewall configuration:**

```bash
# Open port 8080 для Kubernetes cluster IP
sudo ufw allow from <KUBERNETES_NODE_IP> to any port 8080

# Или для целой подсети
sudo ufw allow from 10.0.0.0/8 to any port 8080

# Enable firewall
sudo ufw enable
```

---

**4. RemoteServer конфигурация:**

```json
// ultrasharp-config.json (в Kubernetes pod)
{
  "embedding": {
    "provider": "tei",
    "teiUrl": "http://gpu-server.company.com:8080",  // ← External URL!
    "model": "BAAI/bge-large-en-v1.5",
    "dimensions": 1024,
    "maxBatchSize": 128,
    "timeout": "30s",
    "retryPolicy": {
      "maxRetries": 3,
      "backoffMs": 1000
    }
  }
}
```

---

**5. Мониторинг и health checks:**

```bash
# GPU monitoring
nvidia-smi -l 1  # Update every second

# Docker stats
docker stats tei-embedding

# Logs
docker logs -f tei-embedding --tail 100

# Health endpoint
watch -n 5 'curl -s http://localhost:8080/health | jq'
```

---

### Преимущества Варианта 2

✅ **Dedicated GPU ресурсы**
- Нет competition с другими workloads
- Predictable performance
- Full GPU utilization

✅ **Cheaper для single GPU**
- Hetzner: ~€290/месяц vs AWS $760/месяц
- Экономия ~60%!

✅ **Проще scaling GPU отдельно**
- Kubernetes cluster без GPU (дешевле)
- Scaling GPU независимо от CPU workloads
- Можно использовать on-premise GPU сервер

✅ **Flexibility**
- Легко менять GPU provider
- Можно использовать разные модели на разных серверах
- A/B testing embedding моделей

### Недостатки Варианта 2

⚠️ **Network latency**
- External network hop
- ~10-50ms latency (vs ~1-5ms internal)
- Зависит от network quality

⚠️ **Дополнительная инфраструктура**
- Нужно управлять отдельной VM
- Мониторинг, backups, security patches
- No Kubernetes orchestration

⚠️ **Security**
- External endpoint (нужен firewall)
- SSL/TLS для production
- API key authentication recommended

⚠️ **Single point of failure**
- Если VM down → embeddings не работают
- Нужен monitoring + alerting
- Можно добавить second VM + load balancer

### Cost Estimate (Вариант 2)

**Hetzner GPU Server:**
```
GPU-GL-M (RTX 4000 8GB):
- €290/месяц (~$320/месяц)
- Includes: 6 vCPU, 32GB RAM, GPU

Kubernetes cluster (без GPU):
- 3x CPX31 (4 vCPU, 8GB RAM): €30 * 3 = €90/месяц

Total: ~€380/месяц (~$420/месяц)
```

**vs Вариант 1 (AWS):**
```
Variant 1: $760/месяц
Variant 2: $420/месяц

Экономия: $340/месяц (45%!)
```

---

## 📋 Вариант 3: Managed Service (External API)

**Использовать внешний managed сервис для embeddings.**

### Архитектура

```
┌────────────────────────────┐       ┌──────────────────────────┐
│ Kubernetes Cluster         │       │ Managed Service          │
│                            │       │                          │
│  ┌──────────────────────┐  │ HTTPS │  ┌────────────────────┐  │
│  │ Pod: ultrasharp-     │──┼──────►│  │ Hugging Face       │  │
│  │      server          │  │       │  │ Inference API      │  │
│  │                      │  │       │  │                    │  │
│  │ - RemoteServer (CPU) │  │       │  │ • Pay per request  │  │
│  └──────────────────────┘  │       │  │ • No GPU setup     │  │
│                            │       │  │ • Auto-scaling     │  │
│  No GPU, no setup!         │       │  └────────────────────┘  │
└────────────────────────────┘       │                          │
                                     │  or OpenAI, Cohere, etc. │
                                     └──────────────────────────┘
```

### Опции Managed Services

#### Option A: Hugging Face Inference API

**Pricing:**
```
Standard Inference:
- $0.06 per 1K tokens input
- bge-large-en-v1.5 model available

Example:
- 1M embeddings/месяц
- Avg 50 tokens per input
- Cost: 1M * 50 / 1000 * $0.06 = $3,000/месяц ❌ Дорого!
```

**Configuration:**

```json
{
  "embedding": {
    "provider": "huggingface",
    "apiKey": "hf_xxxxxxxxxxxxx",
    "model": "BAAI/bge-large-en-v1.5",
    "apiUrl": "https://api-inference.huggingface.co/models/BAAI/bge-large-en-v1.5"
  }
}
```

**Code:**

```csharp
public class HuggingFaceEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public async Task<float[]> GetEmbedding(string text)
    {
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://api-inference.huggingface.co/models/{_model}");

        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Content = JsonContent.Create(new { inputs = text });

        var response = await _httpClient.SendAsync(request);
        return await response.Content.ReadFromJsonAsync<float[]>();
    }
}
```

---

#### Option B: OpenAI Embeddings API

**Pricing:**
```
text-embedding-3-large (3072 dimensions):
- $0.00013 per 1K tokens

Example:
- 1M embeddings/месяц
- Avg 50 tokens per input
- Cost: 1M * 50 / 1000 * $0.00013 = $6.50/месяц ✅ Дешево!
```

**Configuration:**

```json
{
  "embedding": {
    "provider": "openai",
    "apiKey": "sk-xxxxxxxxxxxxx",
    "model": "text-embedding-3-large",
    "dimensions": 3072
  }
}
```

**Code:**

```csharp
public class OpenAIEmbeddingService
{
    public async Task<float[]> GetEmbedding(string text)
    {
        var request = new
        {
            input = text,
            model = "text-embedding-3-large"
        };

        var response = await _httpClient.PostAsJsonAsync(
            "https://api.openai.com/v1/embeddings",
            request
        );

        var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>();
        return result.Data[0].Embedding;
    }
}
```

---

#### Option C: Cohere Embeddings API

**Pricing:**
```
embed-multilingual-v3.0:
- $0.10 per 1M tokens

Example:
- 1M embeddings/месяц
- Avg 50 tokens per input
- Cost: 1M * 50 / 1M * $0.10 = $5/месяц ✅ Дешево!
```

---

### Сравнение Managed Services

| Provider | Model | Dimensions | Price/1M tokens | Best for |
|----------|-------|------------|-----------------|----------|
| **OpenAI** | text-embedding-3-large | 3072 | $0.13 | ✅ Best price/quality |
| **Cohere** | embed-multilingual-v3.0 | 1024 | $0.10 | ✅ Multilingual |
| **Hugging Face** | bge-large-en-v1.5 | 1024 | $60.00 | ❌ Expensive |

---

### Преимущества Варианта 3

✅ **Zero infrastructure**
- No GPU setup
- No server management
- Instant start

✅ **Auto-scaling**
- Unlimited capacity
- No rate limits (usually)
- High availability built-in

✅ **Cheap для low volume**
- OpenAI: $6.50/месяц для 1M embeddings
- Pay only for usage
- No idle costs

### Недостатки Варианта 3

⚠️ **Expensive для high volume**
- 10M embeddings/месяц: ~$65 (OpenAI)
- 100M embeddings/месяц: ~$650
- vs self-hosted: ~$320-420/месяц fixed

⚠️ **Network latency**
- External API calls
- ~50-200ms latency
- Rate limits possible

⚠️ **Data privacy**
- Код отправляется external service
- Compliance issues (GDPR, etc.)
- Sensitive code?

⚠️ **Vendor lock-in**
- Зависимость от external provider
- API changes risk
- Pricing changes risk

---

## 📊 Сравнительная таблица

| Aspect | Variant 1: Co-located | Variant 2: Separate VM | Variant 3: Managed API |
|--------|----------------------|------------------------|------------------------|
| **Setup complexity** | 🟡 Medium (K8s GPU) | 🟢 Easy (single VM) | 🟢 Very Easy (config only) |
| **Latency** | 🟢 1-5ms | 🟡 10-50ms | 🔴 50-200ms |
| **Cost (low volume)** | 🔴 $760/month | 🟡 $420/month | 🟢 $10-50/month |
| **Cost (high volume)** | 🟢 $760/month (fixed) | 🟢 $420/month (fixed) | 🔴 $500-5000/month |
| **Scalability** | 🟢 Excellent (K8s) | 🟡 Manual (add VMs) | 🟢 Unlimited |
| **Data privacy** | 🟢 Internal | 🟢 Your infra | 🔴 External service |
| **Maintenance** | 🟡 K8s management | 🟡 VM management | 🟢 Zero |
| **Failure handling** | 🟢 K8s auto-recovery | 🟡 Manual | 🟢 Provider SLA |

---

## 🎯 Рекомендации

### Для команды < 5 разработчиков, low volume

**Рекомендация: Variant 3 (OpenAI API)**

```
Volume: ~100K embeddings/месяц
Cost: ~$0.65/месяц
Setup: 10 минут
```

**Почему:**
- ✅ Минимальная стоимость
- ✅ Zero setup
- ✅ Instant start

---

### Для команды 5-20 разработчиков, medium volume

**Рекомендация: Variant 2 (Hetzner dedicated GPU)**

```
Volume: ~5-10M embeddings/месяц
Cost: ~€290/месяц (~$320)
Setup: 2-3 часа
```

**Почему:**
- ✅ Фиксированная стоимость
- ✅ Data privacy (ваша инфраструктура)
- ✅ Predictable performance
- ✅ Дешевле чем AWS/GCP

---

### Для команды 20+ разработчиков, high volume, enterprise

**Рекомендация: Variant 1 (Kubernetes + GPU nodes)**

```
Volume: 50M+ embeddings/месяц
Cost: ~$760/месяц (AWS) или ~$720 (GCP)
Setup: 1-2 дня
```

**Почему:**
- ✅ Best integration (все в K8s)
- ✅ Auto-scaling
- ✅ High availability
- ✅ Enterprise support

---

## 🔧 Hybrid Approach (Best of All Worlds!)

**Можно комбинировать варианты:**

```json
{
  "embedding": {
    "primary": {
      "provider": "tei",
      "teiUrl": "http://gpu-server.company.com:8080"  // Variant 2
    },
    "fallback": {
      "provider": "openai",
      "apiKey": "sk-xxxxx"  // Variant 3
    }
  }
}
```

**Логика:**

```csharp
public async Task<float[]> GetEmbedding(string text)
{
    try
    {
        // Try primary (self-hosted)
        return await _primaryService.GetEmbedding(text);
    }
    catch (Exception ex)
    {
        _logger.LogWarning("Primary embedding service failed, using fallback");

        // Fallback to managed API
        return await _fallbackService.GetEmbedding(text);
    }
}
```

**Преимущества:**
- ✅ Reliability (auto-failover)
- ✅ Cost optimization (primary = cheap, fallback = expensive)
- ✅ Development flexibility (local → managed → self-hosted)

---

## 📖 Документация и примеры

Создам дополнительные конфигурации для каждого варианта.

**Нужны ещё детали по какому-то из вариантов?**
