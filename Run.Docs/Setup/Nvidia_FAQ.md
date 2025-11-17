# nvidia-container-toolkit: Часто задаваемые вопросы

## ❓ Что это такое?

**nvidia-container-toolkit** — это набор инструментов, который позволяет Docker контейнерам использовать GPU.

## ❌ Что это НЕ:

- ❌ Это НЕ расширение Docker Desktop
- ❌ Это НЕ плагин для Docker
- ❌ Это НЕ программа для Windows
- ❌ Вы НЕ найдете его в списке расширений Docker Desktop

## ✅ Что это на самом деле:

**Это пакет Linux**, который устанавливается:
- **Windows:** В WSL2 Ubuntu (Linux внутри Windows)
- **Linux:** Напрямую в систему

## 🔍 Где искать?

### Windows (Docker Desktop):

**Проверка в WSL2:**
```bash
wsl -d Ubuntu
nvidia-ctk --version
```

Если команда не найдена → не установлен

### Linux:

```bash
nvidia-ctk --version
```

## 🚀 Автоматическая установка

### Windows (PowerShell):

**Вариант 1: Установить только nvidia-container-toolkit**
```powershell
.\setup-nvidia-container-toolkit.ps1
```

**Вариант 2: Установить всё (включая TEI/Ollama)**
```powershell
.\setup-embeddings-interactive.ps1
# При выборе TEI (option 1), скрипт спросит про nvidia-container-toolkit
# Выберите "1. Auto-install" для автоматической установки
```

## 🛠️ Ручная установка

### Windows (в WSL2):

```powershell
# Шаг 1: Войти в WSL2 Ubuntu
wsl -d Ubuntu

# Шаг 2: Установить nvidia-container-toolkit
curl -fsSL https://nvidia.github.io/libnvidia-container/gpgkey | sudo gpg --dearmor -o /usr/share/keyrings/nvidia-container-toolkit-keyring.gpg

curl -s -L https://nvidia.github.io/libnvidia-container/stable/deb/nvidia-container-toolkit.list | \
    sed 's#deb https://#deb [signed-by=/usr/share/keyrings/nvidia-container-toolkit-keyring.gpg] https://#g' | \
    sudo tee /etc/apt/sources.list.d/nvidia-container-toolkit.list

sudo apt-get update
sudo apt-get install -y nvidia-container-toolkit
sudo nvidia-ctk runtime configure --runtime=docker

# Шаг 3: Перезапустить Docker Desktop (в Windows)
exit
```

Затем перезапустите Docker Desktop в Windows.

### Linux (Ubuntu/Debian):

```bash
curl -fsSL https://nvidia.github.io/libnvidia-container/gpgkey | sudo gpg --dearmor -o /usr/share/keyrings/nvidia-container-toolkit-keyring.gpg

curl -s -L https://nvidia.github.io/libnvidia-container/stable/deb/nvidia-container-toolkit.list | \
    sed 's#deb https://#deb [signed-by=/usr/share/keyrings/nvidia-container-toolkit-keyring.gpg] https://#g' | \
    sudo tee /etc/apt/sources.list.d/nvidia-container-toolkit.list

sudo apt-get update
sudo apt-get install -y nvidia-container-toolkit
sudo nvidia-ctk runtime configure --runtime=docker
sudo systemctl restart docker
```

## ✅ Проверка работы

После установки проверьте GPU доступ в Docker:

```bash
docker run --rm --gpus all nvidia/cuda:12.0-base nvidia-smi
```

**Должно показать информацию о вашей видеокарте.**

Если видите ошибку:
```
docker: Error response from daemon: could not select device driver "" with capabilities: [[gpu]]
```

→ nvidia-container-toolkit не установлен или не настроен правильно

## 🔧 Настройки Docker Desktop (Windows)

После установки nvidia-container-toolkit убедитесь:

1. **Settings → General**
   - ✅ "Use the WSL 2 based engine" включено

2. **Settings → Resources → WSL Integration**
   - ✅ "Enable integration with my default WSL distro" включено
   - ✅ Ubuntu включен в списке

3. **Перезапустите Docker Desktop**

## 🐛 Troubleshooting

### "nvidia-ctk: command not found" в WSL

**Проблема:** nvidia-container-toolkit не установлен в WSL2

**Решение:**
```powershell
.\setup-nvidia-container-toolkit.ps1
```

### "could not select device driver" в Docker

**Проблема:** Docker не настроен для использования GPU

**Решение:**
1. Проверьте что nvidia-container-toolkit установлен (см. выше)
2. Перезапустите Docker Desktop
3. Проверьте настройки WSL Integration

### WSL2 Ubuntu не запускается

**Проблема:** WSL2 не установлен или поврежден

**Решение:**
```powershell
# Переустановить WSL2
wsl --install
# Или обновить
wsl --update
```

### Docker Desktop не видит GPU после установки

**Решение:**
1. Полностью закройте Docker Desktop
2. Откройте снова
3. Подождите 30 секунд пока Docker инициализируется
4. Проверьте снова

## 💡 Для чего нужен nvidia-container-toolkit?

**Без него:**
- Docker контейнеры работают только на CPU
- TEI будет медленным (~10-50x медленнее)
- Ollama будет медленным (~5-20x медленнее)

**С ним:**
- Docker контейнеры могут использовать GPU
- TEI работает на GPU → быстрые embeddings
- Ollama работает на GPU → быстрые embeddings

## 📚 Официальная документация

- https://docs.nvidia.com/datacenter/cloud-native/container-toolkit/
- https://github.com/NVIDIA/nvidia-container-toolkit

## 🎯 Рекомендация

**Просто используйте автоматический скрипт:**

```powershell
.\setup-embeddings-interactive.ps1
```

Он всё сделает за вас! 🚀
