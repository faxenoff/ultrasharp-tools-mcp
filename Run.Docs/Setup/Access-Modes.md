# UltrasharpTools MCP - Режимы доступа к файлам

**Полное руководство по работе с файлами в Local и Remote режимах.**

---

## 📋 Содержание

- [Обзор режимов](#обзор-режимов)
- [Local Mode (Stdio)](#local-mode-stdio)
- [Remote Mode (HTTP/SSE)](#remote-mode-httpsse)
- [Сравнение режимов](#сравнение-режимов)
- [Практические примеры](#практические-примеры)
- [Troubleshooting](#troubleshooting)
- [FAQ](#faq)

---

## Обзор режимов

UltrasharpTools MCP поддерживает два режима работы, которые **принципиально отличаются** доступом к файловой системе.

### Ключевое различие

**Local Mode (Stdio):**
- Процесс запускается **на вашей машине**
- **Прямой доступ** к файловой системе
- Работает с **вашими локальными путями**

**Remote Mode (HTTP/SSE):**
- Процесс запускается **в контейнере/кластере**
- **Изолированная** файловая система
- **НЕТ доступа** к вашим локальным файлам
- Нужно **явно предоставить** файлы через volumes

---

## Local Mode (Stdio)

### Как работает

```
┌──────────────────────────────────────────────┐
│ Claude Desktop (ваша машина)                 │
│                                              │
│ claude_desktop_config.json:                  │
│ {                                            │
│   "command": "D:/path/to/Comm.exe"           │
│ }                                            │
└──────────┬───────────────────────────────────┘
           │ запускает как child process
           ↓
┌──────────────────────────────────────────────┐
│ UltrasharpTools.Comm.exe (stdio bridge)      │
│ (~5 MB, минимальный процесс)                 │
│                                              │
│ └───→ Named Pipe IPC                         │
│         ↓                                    │
│ UltrasharpTools.Droid.exe (singleton)        │
│ (Roslyn сервер, ~500 MB)                     │
│                                              │
│ Working Directory: где был запущен           │
│ Permissions: ваши user permissions           │
│ Environment: ваши env variables              │
└──────────┬───────────────────────────────────┘
           │ ПРЯМОЙ доступ к файловой системе
           ↓
┌──────────────────────────────────────────────┐
│ Ваша файловая система                        │
│                                              │
│ D:\Projects\MyApp\                           │
│ C:\Users\YourName\.nuget\packages\           │
│ /home/user/projects/                         │
│ ... вся ваша файловая система ...            │
└──────────────────────────────────────────────┘
```

> **💡 Почему три процесса?** Comm.exe — лёгкий stdio bridge, который позволяет
> нескольким редакторам/агентам подключаться к одному Droid+VectorDB, экономя
> 2-3 GB RAM на каждый инстанс.

### Конфигурация

**Windows (`%USERPROFILE%\.claude.json`):**
```json
{
  "mcpServers": {
    "ultrasharp-tools": {
      "command": "D:/Tools/UltrasharpTools/Comm/UltrasharpTools.Comm.exe"
    }
  }
}
```

**macOS/Linux (`~/.claude.json`):**
```json
{
  "mcpServers": {
    "ultrasharp-tools": {
      "command": "/home/user/ultrasharp-tools/Comm/UltrasharpTools.Comm"
    }
  }
}
```

> ⚠️ **Важно:** Запускается `Comm.exe`, а не `Droid.exe`!

### Доступ к файлам

**Что происходит при load_solution:**

```csharp
// Claude запрашивает:load_solutionn("D:/MyProjects/MyApp/MyApp.sln")

// Droid.exe (работает локально):
1. Читает D:/MyProjects/MyApp/MyApp.sln (прямой file read)
2. Парсит .sln → находит .csproj файлы
3. Загружает каждый .csproj:
   - D:/MyProjects/MyApp/MyApp.Core/MyApp.Core.csproj
   - D:/MyProjects/MyApp/MyApp.Web/MyApp.Web.csproj
4. Загружает referenced assemblies:
   - C:/Users/YourName/.nuget/packages/newtonsoft.json/13.0.1/
   - D:/MyProjects/MyApp/MyApp.Core/bin/Debug/net10.0/
5. Парсит все .cs файлы в solution
6. Строит Symbol Index
7. Сохраняет cache в %TEMP%/UltrasharpTools/SymbolCache/
```

**Результат:** Всё работает "как обычно", прямой доступ к файлам.

### Преимущества

✅ **Простота настройки**
- Один JSON файл
- Нет Docker/Kubernetes
- Работает "из коробки"

✅ **Максимальная скорость**
- Нет сетевых задержек
- Прямой I/O к диску
- Локальный CPU/RAM

✅ **Безопасность**
- Всё локально
- Ничего не уходит в сеть
- Работает под вашими permissions

✅ **Удобство разработки**
- Используете свои привычные пути
- Git репозиторий на месте
- Изменения видны сразу

### Ограничения

⚠️ **Один пользователь**
- Нельзя поделиться с командой
- Каждый настраивает сам

⚠️ **Производительность машины**
- Зависит от вашего CPU/RAM
- Большие solutions могут быть медленными

⚠️ **Нет изоляции**
- Процесс имеет ваши permissions
- Может читать/писать что угодно

### Use Cases

Идеально для:
- ✅ Индивидуальной разработки
- ✅ Локального тестирования
- ✅ Отладки и рефакторинга
- ✅ Работы с локальными проектами

---

## Remote Mode (HTTP/SSE)

### Как работает

```
┌────────────────────────────────────────────────┐
│ Ваша машина (Claude Desktop)                   │
│                                                │
│ HTTP/SSE client → подключается к remote server │
│                                                │
│ Ваш код: D:\MyProject\ ← НЕ ДОСТУПЕН серверу! │
└────────────┬───────────────────────────────────┘
             │
             ↓ через сеть (HTTP/SSE)
             │
┌────────────────────────────────────────────────┐
│ Kubernetes Cluster / Docker Host               │
│                                                │
│  ┌──────────────────────────────────────────┐ │
│  │ Pod: ultrasharp-tools-server             │ │
│  │                                          │ │
│  │ UltrasharpTools.Overlord             │ │
│  │ - Изолированная ФС контейнера            │ │
│  │ - НЕТ доступа к D:\MyProject\            │ │
│  │ - Доступ к /app/projects/ (volume)       │ │
│  └──────────┬───────────────────────────────┘ │
│             │ volume mount                    │
│             ↓                                  │
│  ┌──────────────────────────────────────────┐ │
│  │ PersistentVolume                         │ │
│  │ /app/projects/                           │ │
│  │   ├─ myproject/ ← git cloned             │ │
│  │   │   ├─ MyApp.sln                       │ │
│  │   │   ├─ src/                            │ │
│  │   │   └─ ...                             │ │
│  │   └─ other-project/                      │ │
│  └──────────────────────────────────────────┘ │
└────────────────────────────────────────────────┘
```

### ⚠️ Критически важно понять

**Remote сервер работает в контейнере с ИЗОЛИРОВАННОЙ файловой системой.**

```bash
# На вашей машине:
D:\MyProject\MyApp.sln  ← это существует

# В Kubernetes pod:
ls D:\  # ← такого диска НЕТ!
ls /app/projects/  # ← вот что доступно
```

**Сервер НЕ МОЖЕТ получить доступ к вашим локальным файлам автоматически!**

### Как предоставить файлы серверу

#### Вариант 1: Git Clone (🥇 рекомендуется)

**Лучший подход для production и команд.**

**Автоматический clone через init container:**

```yaml
# kubernetes/deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: ultrasharp-tools-server
spec:
  template:
    spec:
      # Init container клонирует репозитории перед запуском main container
      initContainers:
      - name: git-clone
        image: alpine/git:latest
        command: ['sh', '-c']
        args:
          - |
            # Проверяем и клонируем все нужные репозитории
            cd /app/projects

            # Проект 1
            if [ ! -d "myapp" ]; then
              echo "Cloning myapp..."
              git clone https://github.com/mycompany/myapp.git
            else
              echo "Updating myapp..."
              cd myapp && git pull && cd ..
            fi

            # Проект 2
            if [ ! -d "shared-libs" ]; then
              echo "Cloning shared-libs..."
              git clone https://github.com/mycompany/shared-libs.git
            else
              echo "Updating shared-libs..."
              cd shared-libs && git pull && cd ..
            fi

            echo "Git repositories ready!"
        volumeMounts:
        - name: projects-storage
          mountPath: /app/projects
        env:
        # Для private репозиториев:
        - name: GIT_USERNAME
          valueFrom:
            secretKeyRef:
              name: git-credentials
              key: username
        - name: GIT_PASSWORD
          valueFrom:
            secretKeyRef:
              name: git-credentials
              key: password

      containers:
      - name: ultrasharp-server
        image: ultrasharp-tools-server:latest
        volumeMounts:
        - name: projects-storage
          mountPath: /app/projects

      volumes:
      - name: projects-storage
        persistentVolumeClaim:
          claimName: projects-pvc
```

**Создание Git credentials secret:**

```bash
kubectl create secret generic git-credentials \
  --from-literal=username=YOUR_USERNAME \
  --from-literal=password=YOUR_PERSONAL_ACCESS_TOKEN \
  -n ultrasharp-tools
```

**Использование:**

```
# После deploy pod автоматически клонирует репозитории
# Claude может использовать:
LoadSolution("/app/projects/myapp/MyApp.sln")
```

**Преимущества:**
- ✅ Автоматическое обновление при restart pod
- ✅ Version control встроен
- ✅ Легко откатить изменения
- ✅ Работает с private repositories

---

#### Вариант 2: NFS/SMB Mount

**Хорошо для shared team storage.**

**Настройка NFS сервера (example):**

```bash
# На NFS сервере (Ubuntu):
sudo apt install nfs-kernel-server
sudo mkdir -p /export/team-projects
sudo chown nobody:nogroup /export/team-projects

# /etc/exports
/export/team-projects 192.168.1.0/24(rw,sync,no_subtree_check,no_root_squash)

sudo exportfs -ra
sudo systemctl restart nfs-kernel-server
```

**Kubernetes PersistentVolume с NFS:**

```yaml
# pv-nfs.yaml
apiVersion: v1
kind: PersistentVolume
metadata:
  name: projects-nfs-pv
spec:
  capacity:
    storage: 100Gi
  accessModes:
    - ReadWriteMany  # Multiple pods can read/write
  persistentVolumeReclaimPolicy: Retain
  nfs:
    server: nfs-server.example.com  # IP или hostname NFS сервера
    path: "/export/team-projects"    # exported path
```

**PersistentVolumeClaim:**

```yaml
# pvc-nfs.yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: projects-nfs-pvc
  namespace: ultrasharp-tools
spec:
  accessModes:
    - ReadWriteMany
  resources:
    requests:
      storage: 100Gi
  storageClassName: ""  # пустой для использования существующего PV
  volumeName: projects-nfs-pv
```

**Deployment:**

```yaml
# deployment.yaml
spec:
  volumes:
  - name: projects-storage
    persistentVolumeClaim:
      claimName: projects-nfs-pvc

  containers:
  - name: ultrasharp-server
    volumeMounts:
    - name: projects-storage
      mountPath: /app/projects
```

**Загрузка файлов на NFS:**

```bash
# С вашей машины:
scp -r D:\MyProject user@nfs-server:/export/team-projects/MyProject

# Или mount NFS локально:
# Windows:
mount \\nfs-server\team-projects Z:
xcopy D:\MyProject Z:\MyProject\ /E

# Linux/Mac:
sudo mount -t nfs nfs-server:/export/team-projects /mnt/nfs
cp -r ~/MyProject /mnt/nfs/
```

**Преимущества:**
- ✅ Shared storage для всей команды
- ✅ Прямой доступ к файлам с любой машины
- ✅ ReadWriteMany - multiple pods
- ✅ Persistence даже при удалении pods

---

#### Вариант 3: kubectl cp

**Быстрый способ для тестирования.**

```bash
# 1. Найдите pod name
kubectl get pods -n ultrasharp-tools
# Вывод: ultrasharp-tools-server-7d8f9c5b6-xyz12

# 2. Скопируйте проект
kubectl cp ./MyLocalProject/ \
  ultrasharp-tools/ultrasharp-tools-server-7d8f9c5b6-xyz12:/app/projects/MyProject/

# 3. Проверьте
kubectl exec -it ultrasharp-tools-server-7d8f9c5b6-xyz12 -n ultrasharp-tools -- \
  ls /app/projects/MyProject/
```

**⚠️ Ограничения:**
- Нужно повторять при каждом изменении кода
- Не работает если pod перезапустится
- Медленно для больших проектов

**Use case:** Быстрое тестирование, debug single file.

---

#### Вариант 4: Sidecar Container с sync

**Продвинутый вариант: автоматическая синхронизация.**

```yaml
# deployment.yaml
spec:
  containers:
  # Main MCP server
  - name: ultrasharp-server
    image: ultrasharp-tools-server:latest
    volumeMounts:
    - name: projects-storage
      mountPath: /app/projects

  # Sidecar: Git sync
  - name: git-sync
    image: k8s.gcr.io/git-sync/git-sync:v3.6.3
    args:
      - --repo=https://github.com/mycompany/myproject.git
      - --branch=main
      - --root=/app/projects
      - --period=30s  # Sync каждые 30 секунд
    volumeMounts:
    - name: projects-storage
      mountPath: /app/projects
    env:
    - name: GIT_SYNC_USERNAME
      valueFrom:
        secretKeyRef:
          name: git-credentials
          key: username
    - name: GIT_SYNC_PASSWORD
      valueFrom:
        secretKeyRef:
          name: git-credentials
          key: password

  volumes:
  - name: projects-storage
    emptyDir: {}  # или PVC
```

**Преимущества:**
- ✅ Автоматическая синхронизация с Git
- ✅ Всегда актуальный код
- ✅ Не нужно manually update

---

### NuGet Packages в Remote Mode

**Проблема:** NuGet packages могут отсутствовать в контейнере.

**Решение 1: Restore в Dockerfile**

```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0

# Copy и restore перед копированием исходников
COPY MyApp.sln .
COPY MyApp/*.csproj MyApp/
RUN dotnet restore MyApp.sln

# Теперь копируем исходники
COPY . .
```

**Решение 2: Init container с restore**

```yaml
initContainers:
- name: nuget-restore
  image: mcr.microsoft.com/dotnet/sdk:10.0
  command: ['sh', '-c']
  args:
    - |
      cd /app/projects/myproject
      dotnet restore MyApp.sln
  volumeMounts:
  - name: projects-storage
    mountPath: /app/projects
  - name: nuget-cache
    mountPath: /root/.nuget/packages
```

**Решение 3: Shared NuGet cache volume**

```yaml
volumes:
- name: nuget-cache
  persistentVolumeClaim:
    claimName: nuget-cache-pvc

containers:
- name: ultrasharp-server
  volumeMounts:
  - name: nuget-cache
    mountPath: /root/.nuget/packages  # стандартный путь NuGet cache
```

---

### Проверка доступа к файлам

**Checklist после deployment:**

```bash
# 1. Проверьте что pod запущен
kubectl get pods -n ultrasharp-tools
# STATUS должен быть "Running"

# 2. Подключитесь к pod
kubectl exec -it POD_NAME -n ultrasharp-tools -- /bin/bash

# 3. Проверьте mounted volumes
df -h
# Должен быть /app/projects

# 4. Проверьте наличие проектов
ls -la /app/projects/
# Должны быть ваши проекты

# 5. Найдите .sln файлы
find /app/projects -name "*.sln"

# 6. Проверьте что можно читать файлы
cat /app/projects/myproject/MyApp.sln

# 7. Проверьте NuGet packages
ls -la /root/.nuget/packages/

# 8. Попробуйте restore (если нужно)
cd /app/projects/myproject
dotnet restore MyApp.sln
```

---

## Сравнение режимов

### Производительность

| Операция | Local (Stdio) | Remote (HTTP/SSE) |
|----------|---------------|-------------------|
| **LoadSolution** | 4.8s (with cache) | 5.5-8s (network + storage) |
| **File I/O** | Local disk speed | Network + PV speed |
| **Symbol search** | < 100ms | < 150ms (+ network latency) |
| **Code modification** | Instant write | Network latency + write |
| **NuGet restore** | Local cache | Depends on cache setup |

### Безопасность

| Аспект | Local (Stdio) | Remote (HTTP/SSE) |
|--------|---------------|-------------------|
| **Изоляция** | Нет (ваши permissions) | Да (container isolation) |
| **Network exposure** | Нет | Да (требуется защита) |
| **Access control** | OS permissions | Kubernetes RBAC |
| **Audit logs** | Нет | Kubernetes logs |

### Стоимость

| Ресурс | Local (Stdio) | Remote (HTTP/SSE) |
|--------|---------------|-------------------|
| **CPU/RAM** | Ваша машина | Cluster resources |
| **Storage** | Локальный диск | PersistentVolume ($) |
| **Network** | Нет | Egress traffic ($) |
| **Maintenance** | Вы сами | DevOps time |

---

## Практические примеры

### Пример 1: Solo Developer

**Задача:** Локальная разработка на своей машине.

**Решение: Local Mode**

```json
// ~/.claude.json
{
  "mcpServers": {
    "ultrasharp-tools": {
      "command": "D:/tools/ultrasharp/Comm/UltrasharpTools.Comm.exe"
    }
  }
}
```

**Workflow:**
```
1. Открываете Claude Desktop
2. "LoadSolution D:/MyProjects/MyApp/MyApp.sln"
3. Работаете с кодом
4. Изменения сохраняются локально
5. Git commit/push как обычно
```

---

### Пример 2: Development Team

**Задача:** 5 разработчиков, shared code review server.

**Решение: Remote Mode + Git Clone**

```yaml
# deployment.yaml
initContainers:
- name: git-clone
  image: alpine/git
  command: ['sh', '-c']
  args:
    - |
      cd /app/projects
      git clone https://github.com/company/project1.git
      git clone https://github.com/company/project2.git
  volumeMounts:
  - name: projects
    mountPath: /app/projects
```

**Workflow:**
```
1. Разработчик создает PR на GitHub
2. Claude подключается к remote server
3. "LoadSolution /app/projects/project1/App.sln"
4. "Review changed files in PR #123"
5. Claude анализирует код и дает feedback
```

---

### Пример 3: CI/CD Pipeline

**Задача:** Автоматический code review при каждом PR.

**Решение: Remote Mode + Git Sync Sidecar**

```yaml
# ci-job.yaml
apiVersion: batch/v1
kind: Job
metadata:
  name: code-review-pr-123
spec:
  template:
    spec:
      initContainers:
      - name: checkout-pr
        image: alpine/git
        command: ['sh', '-c']
        args:
          - |
            cd /workspace
            git clone https://github.com/company/project.git
            cd project
            git fetch origin pull/123/head:pr-123
            git checkout pr-123
        volumeMounts:
        - name: workspace
          mountPath: /workspace

      containers:
      - name: code-reviewer
        image: ultrasharp-tools-server:latest
        command: ['sh', '-c']
        args:
          - |
            # Run code analysis
            # Results posted to PR comments
        volumeMounts:
        - name: workspace
          mountPath: /app/projects
```

---

## Troubleshooting

### Local Mode: "Cannot find solution file"

**Проблема:**
```
LoadSolution("D:/MyProject/App.sln")
→ Error: File not found
```

**Причины и решения:**

**1. Неправильный путь:**
```bash
# Проверьте путь
ls "D:/MyProject/App.sln"

# Используйте абсолютный путь
LoadSolution("D:/MyProject/App.sln")  # ✅
LoadSolution("./App.sln")              # ❌ relative paths могут не работать
```

**2. Encoding/special characters:**
```bash
# Избегайте пробелов и спецсимволов
D:/My Project/App.sln  # ❌ пробел
D:/MyProject/App.sln   # ✅
```

**3. Permissions:**
```bash
# Проверьте permissions
# Windows:
icacls "D:/MyProject/App.sln"

# Linux/Mac:
ls -l ~/MyProject/App.sln
```

---

### Remote Mode: "No projects found"

**Проблема:**
```
LoadSolution("/app/projects/myapp/App.sln")
→ Error: File not found
```

**Диагностика:**

```bash
# 1. Подключитесь к pod
kubectl exec -it POD_NAME -n ultrasharp-tools -- /bin/bash

# 2. Проверьте volumes
df -h
# Должен быть /app/projects

# 3. Проверьте файлы
ls -la /app/projects/
# Пусто? → Volume не mounted или Git clone не сработал

# 4. Проверьте init container logs
kubectl logs POD_NAME -c git-clone -n ultrasharp-tools
```

**Решения:**

**Если volume не mounted:**
```bash
# Проверьте PVC
kubectl get pvc -n ultrasharp-tools
# STATUS должен быть "Bound"

# Проверьте deployment
kubectl describe deployment ultrasharp-tools-server -n ultrasharp-tools
# Смотрите секцию "Mounts"
```

**Если Git clone не сработал:**
```bash
# Проверьте init container
kubectl describe pod POD_NAME -n ultrasharp-tools
# Смотрите "Init Containers" статус

# Проверьте logs
kubectl logs POD_NAME -c git-clone -n ultrasharp-tools

# Manually clone для debug
kubectl exec -it POD_NAME -n ultrasharp-tools -- /bin/bash
cd /app/projects
git clone https://github.com/your/repo.git
```

---

### Remote Mode: "NuGet packages not found"

**Проблема:**
```
LoadSolution("/app/projects/myapp/App.sln")
→ Warning: Missing packages: Newtonsoft.Json 13.0.1
```

**Решение:**

```bash
# В pod:
kubectl exec -it POD_NAME -n ultrasharp-tools -- /bin/bash

cd /app/projects/myapp
dotnet restore App.sln

# Если успешно, добавьте в init container:
```

```yaml
initContainers:
- name: nuget-restore
  image: mcr.microsoft.com/dotnet/sdk:10.0
  command: ['sh', '-c']
  args:
    - |
      cd /app/projects/myapp
      dotnet restore App.sln
  volumeMounts:
  - name: projects
    mountPath: /app/projects
```

---

## FAQ

### Q: Можно ли использовать оба режима одновременно?

**A:** Да! Можно настроить два разных MCP servers:

```json
{
  "mcpServers": {
    "ultrasharp-local": {
      "command": "D:/tools/ultrasharp/Comm/UltrasharpTools.Comm.exe"
    },
    "ultrasharp-remote": {
      "type": "sse",
      "url": "https://ultrasharp.company.com/sse"
    }
  }
}
```

Используйте local для разработки, remote для code review.

---

### Q: Как sync изменения между local и remote?

**A:** Через Git:

```bash
# Local (ваша машина):
git commit -am "Feature implementation"
git push origin feature-branch

# Remote (init container подтянет при restart):
# или через git-sync sidecar (автоматически каждые 30s)
```

---

### Q: Сколько стоит remote mode?

**A:** Зависит от инфраструктуры:

**Self-hosted Kubernetes:**
- CPU/RAM: ~$20-50/месяц (small VM)
- Storage: ~$5-10/месяц (50GB PV)
- Total: ~$25-60/месяц

**Managed Kubernetes (GKE/EKS/AKS):**
- Node: ~$50-100/месяц
- PV: ~$10-20/месяц
- Total: ~$60-120/месяц

**vs Local:** $0 (используете свою машину)

---

### Q: Безопасно ли remote mode для proprietary кода?

**A:** Да, если правильно настроить:

✅ **Network isolation** (private Kubernetes cluster)
✅ **RBAC** (кто может access pods)
✅ **Encrypted volumes** (data at rest)
✅ **TLS** (data in transit)
✅ **Audit logging** (кто что делал)
✅ **No external access** (только internal network)

**Рекомендация:** Для очень sensitive кода используйте Local Mode.

---

### Q: Какой режим быстрее?

**A:** Local Mode быстрее:

| Операция | Local | Remote |
|----------|-------|--------|
| File I/O | 100-200 MB/s | 10-50 MB/s (network) |
| Latency | ~0ms | 5-50ms |
| LoadSolution | 4.8s | 5.5-8s |

**Но:** Remote может использовать более мощное железо (16+ cores).

---

### Q: Можно ли remote mode без Kubernetes?

**A:** Да! Используйте Docker:

```bash
docker run -d \
  -p 3001:3001 \
  -v /path/to/projects:/app/projects \
  ultrasharp-tools-overlord:latest
```

Проще чем Kubernetes, но без orchestration, scaling, etc.

---

## Заключение

**Для большинства разработчиков:**
→ Используйте **Local Mode** (простота + скорость)

**Для команд и CI/CD:**
→ Используйте **Remote Mode** с Git-based workflow

**Hybrid approach:**
→ Local для development, Remote для code review

**Документация:**
- [Main README](../../README.md)
- [Deployment Guide](../Deployment/README.md)
- [MCP Configuration](../Configuration/MCP_Sharp.md)
