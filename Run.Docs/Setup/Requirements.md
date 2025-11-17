# Требования для Embedding System

## 📋 Что нужно обычным пользователям

### ✅ Минимальные требования (для всех провайдеров):

1. **NVIDIA драйвер** (Game Ready или Studio)
   - Проверка: `nvidia-smi`
   - Скачать: https://www.nvidia.com/download/index.aspx

### 🐳 Для TEI (8192 токена, рекомендуется для RTX 30xx+):

1. **Docker Desktop**
   - Windows: https://docs.docker.com/desktop/install/windows-install/
   - Linux: https://docs.docker.com/engine/install/
   - Проверка: `docker --version`

2. **nvidia-container-toolkit** (для доступа к GPU из Docker)

   ⚠️ **ВАЖНО:** Это НЕ расширение Docker Desktop!
   - На Windows устанавливается в **WSL2 Ubuntu**
   - Это пакет Linux, а не плагин Docker Desktop

   **Windows (Docker Desktop + WSL2) - АВТОМАТИЧЕСКАЯ УСТАНОВКА:**
   ```powershell
   # Просто запустите скрипт:
   .\setup-nvidia-container-toolkit.ps1
   ```

   Скрипт автоматически:
   - ✅ Найдет WSL2 Ubuntu
   - ✅ Установит nvidia-container-toolkit
   - ✅ Настроит Docker runtime
   - ✅ Перезапустит Docker Desktop
   - ✅ Проверит работоспособность

   **Или вручную (внутри WSL2):**
   ```bash
   wsl -d Ubuntu
   curl -fsSL https://nvidia.github.io/libnvidia-container/gpgkey | sudo gpg --dearmor -o /usr/share/keyrings/nvidia-container-toolkit-keyring.gpg
   curl -s -L https://nvidia.github.io/libnvidia-container/stable/deb/nvidia-container-toolkit.list | sed 's#deb https://#deb [signed-by=/usr/share/keyrings/nvidia-container-toolkit-keyring.gpg] https://#g' | sudo tee /etc/apt/sources.list.d/nvidia-container-toolkit.list
   sudo apt-get update
   sudo apt-get install -y nvidia-container-toolkit
   sudo nvidia-ctk runtime configure --runtime=docker
   ```

   **Linux (Ubuntu/Debian):**
   ```bash
   curl -fsSL https://nvidia.github.io/libnvidia-container/gpgkey | sudo gpg --dearmor -o /usr/share/keyrings/nvidia-container-toolkit-keyring.gpg
   curl -s -L https://nvidia.github.io/libnvidia-container/stable/deb/nvidia-container-toolkit.list | sed 's#deb https://#deb [signed-by=/usr/share/keyrings/nvidia-container-toolkit-keyring.gpg] https://#g' | sudo tee /etc/apt/sources.list.d/nvidia-container-toolkit.list
   sudo apt-get update
   sudo apt-get install -y nvidia-container-toolkit
   sudo nvidia-ctk runtime configure --runtime=docker
   sudo systemctl restart docker
   ```

   **Проверка:**
   ```bash
   docker run --rm --gpus all nvidia/cuda:12.0-base nvidia-smi
   ```

### 🦙 Для Ollama (512 токенов, проще установка):

1. **Ollama** - простая установка, без Docker
   - Windows: https://ollama.com/download/windows
   - Linux: `curl -fsSL https://ollama.com/install.sh | sh`
   - Проверка: `ollama --version`

### 💾 Для Memory Provider (fallback):

- Не требует дополнительных зависимостей
- Использует детерминистический hash вместо ML embeddings
- Всегда доступен как резервный вариант

---

## ❌ НЕ ТРЕБУЕТСЯ для обычных пользователей:

### CUDA Toolkit (3GB)
**Вам НЕ нужен CUDA Toolkit, если:**
- Вы не разработчик
- Не компилируете CUDA код
- Не используете PyTorch/TensorFlow локально (не в Docker)

**CUDA Toolkit нужен только для:**
- Компиляции CUDA приложений (`nvcc`)
- Разработки с TensorRT
- Локального обучения моделей

**Docker контейнеры (TEI/Ollama) уже включают CUDA runtime!**

---

## 🚀 Автоматическая установка

### Вариант 1: Всё в одном скрипте (рекомендуется)

**Windows (PowerShell):**
```powershell
.\setup-embeddings-interactive.ps1
```

**Linux/macOS (Bash):**
```bash
chmod +x setup-embeddings-interactive.sh
./setup-embeddings-interactive.sh
```

### Что делает скрипт:

1. ✅ Определяет GPU и Compute Capability
2. ✅ Рекомендует оптимальный провайдер (TEI/Ollama)
3. ✅ Проверяет Docker (для TEI)
4. ✅ **Проверяет nvidia-container-toolkit** (для GPU доступа в Docker)
5. ✅ **АВТОМАТИЧЕСКИ устанавливает nvidia-container-toolkit** (если нужно)
6. ✅ Автоматически устанавливает выбранный провайдер
7. ✅ Запускает и проверяет работоспособность

### Вариант 2: Только nvidia-container-toolkit

Если хотите установить только nvidia-container-toolkit:

**Windows:**
```powershell
.\setup-nvidia-container-toolkit.ps1
```

Затем запустите основной скрипт:
```powershell
.\setup-embeddings-interactive.ps1
```

---

## 🔍 Проверка установки

### Проверка Docker + GPU:
```bash
docker run --rm --gpus all nvidia/cuda:12.0-base nvidia-smi
```
Должен показать информацию о вашей видеокарте.

### Проверка TEI:
```bash
docker ps | grep tei-server
curl http://127.0.0.1:8080/health
```

### Проверка Ollama:
```bash
ollama list
curl http://127.0.0.1:11434/api/version
```

---

## 📊 Сравнение провайдеров

| Провайдер | Контекст | GPU требования | Установка | Размер |
|-----------|----------|----------------|-----------|--------|
| **TEI** | 8192 токена | RTX 30xx+ (CC 8.0+) | Docker + nvidia-container-toolkit | ~2 GB |
| **Ollama** | 512 токенов | Любая NVIDIA | Простая (без Docker) | ~200 MB |
| **Memory** | N/A | Нет | Не требуется | 0 MB |

---

## 💡 Рекомендации

- **RTX 3060/3070/3080/3090/4060/4070/4080/4090:** → TEI (8192 токена)
- **GTX 1650/1660, RTX 2060/2070:** → Ollama (512 токенов)
- **Без GPU / старая GPU:** → Memory (fallback)
- **Не хотите возиться с Docker:** → Ollama

---

## 🐛 Troubleshooting

### "Docker cannot access GPU"
**Решение:** Установите nvidia-container-toolkit (см. выше)

### "Container exits immediately"
**Проверка:**
```bash
docker logs tei-server
```
Возможно недостаточно VRAM или GPU не поддерживается

### "Ollama not responding"
**Решение:**
```bash
# Запустите Ollama вручную
ollama serve
```

### Хотите удалить CUDA Toolkit?
Если вы не разработчик - можете смело удалять CUDA Toolkit для освобождения 3GB.
Оставьте только NVIDIA драйвер.
