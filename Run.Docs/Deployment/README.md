# UltrasharpTools Overlord - Deployment Guide

> ⚠️ **Статус:** Overlord находится в активном тестировании. API может меняться.

Полное руководство по развертыванию UltrasharpTools MCP Remote Server в Kubernetes кластере.

## 📋 Содержание

- [Требования](#требования)
- [Docker Сборка](#docker-сборка)
- [Kubernetes Развертывание](#kubernetes-развертывание)
- [Helm Chart Развертывание](#helm-chart-развертывание)
- [GitHub Container Registry](#github-container-registry)
- [Конфигурация](#конфигурация)
- [Мониторинг и Логи](#мониторинг-и-логи)
- [Troubleshooting](#troubleshooting)

---

## ⚠️ Важно: Доступ к файлам в Remote Mode

**Remote MCP Server работает в контейнере и НЕ ИМЕЕТ доступа к файлам на вашей локальной машине!**

### Как это работает?

```
┌─────────────────────────────────────────────────────────────────┐
│ Ваша машина (Claude Desktop)                                    │
│   - Ваш код: D:\MyProject\                                      │
│   - HTTP/SSE запросы к remote серверу                           │
└─────────────────┬───────────────────────────────────────────────┘
                  │
                  ↓ через сеть (HTTP/SSE)
┌─────────────────────────────────────────────────────────────────┐
│ Kubernetes Pod / Docker Container                               │
│   - Файловая система контейнера (изолирована!)                  │
│   - НЕТ доступа к D:\MyProject\                                 │
│   - Доступ ТОЛЬКО к /app/projects/ (mounted volume)             │
└─────────────────┬───────────────────────────────────────────────┘
                  │
                  ↓ volume mount
┌─────────────────────────────────────────────────────────────────┐
│ PersistentVolume / NFS / Git Clone                              │
│   - /app/projects/myproject/ ← клонирован из Git                │
│   - load_solution("/app/projects/myproject/App.sln") работает!   │
└─────────────────────────────────────────────────────────────────┘
```

### ✅ Как предоставить код remote серверу?

**Выберите один из вариантов:**

#### 🥇 Вариант 1: Git Clone (рекомендуется)

**Лучший вариант для большинства случаев.**

```bash
# В init container или вручную в pod:
kubectl exec -it ultrasharp-tools-server-xxx -n ultrasharp-tools -- /bin/bash
cd /app/projects
git clone https://github.com/mycompany/myproject.git
```

После этого Claude может использовать:
```
LoadSolution("/app/projects/myproject/MyApp.sln")
```

**Автоматизация через init container:**

```yaml
# deployment.yaml
spec:
  initContainers:
  - name: git-clone
    image: alpine/git
    command: ['sh', '-c']
    args:
      - |
        cd /app/projects
        if [ ! -d "myproject" ]; then
          git clone https://github.com/mycompany/myproject.git
        else
          cd myproject && git pull
        fi
    volumeMounts:
    - name: projects-storage
      mountPath: /app/projects
```

#### 🥈 Вариант 2: NFS/SMB Mount

**Хорошо для shared team storage.**

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
    - ReadWriteMany
  nfs:
    server: nfs-server.example.com  # ваш NFS сервер
    path: "/exported/team-projects"   # папка с проектами
```

После mount к `/app/projects`:
```
LoadSolution("/app/projects/MyApp/MyApp.sln")
```

#### 🥉 Вариант 3: kubectl cp

**Для быстрого тестирования.**

```bash
# Скопировать локальный проект в pod
kubectl cp ./MyLocalProject/ \
  ultrasharp-tools-server-xxx:/app/projects/MyProject/ \
  -n ultrasharp-tools
```

**⚠️ Минусы:** Нужно повторять при каждом изменении кода.

#### 🏅 Вариант 4: Persistent Volume Claim + manual upload

```bash
# 1. Создать PVC
kubectl apply -f kubernetes/pvc.yaml

# 2. Создать временный pod для upload
kubectl run -it --rm upload-pod \
  --image=alpine \
  --overrides='{"spec":{"volumes":[{"name":"storage","persistentVolumeClaim":{"claimName":"ultrasharp-data-pvc"}}],"containers":[{"name":"upload","image":"alpine","volumeMounts":[{"name":"storage","mountPath":"/data"}],"stdin":true,"tty":true}]}}' \
  -- /bin/sh

# 3. В pod: установить rsync/scp и загрузить файлы
```

### 📋 Checklist перед использованием Remote Mode

- [ ] **Определились с методом доступа** (Git clone / NFS / kubectl cp)
- [ ] **Создан PersistentVolume** для хранения проектов
- [ ] **Код загружен** в `/app/projects/` через выбранный метод
- [ ] **Проверили доступ:** `kubectl exec -it pod -- ls /app/projects/`
- [ ] **NuGet packages восстановлены** (если нужно)
- [ ] **Git credentials настроены** (для private repos)

### 🔍 Проверка доступа к файлам

```bash
# Подключитесь к pod
kubectl exec -it ultrasharp-tools-server-xxx -n ultrasharp-tools -- /bin/bash

# Проверьте наличие проектов
ls -la /app/projects/

# Попробуйте найти .sln файлы
find /app/projects -name "*.sln"

# Проверьте что код есть
cat /app/projects/myproject/Program.cs
```

---

## Требования

### Локальная Разработка
- Docker 20.10+
- .NET 10 SDK (для локальной сборки)
- Git

### Production Развертывание
- Kubernetes cluster 1.26+
- kubectl configured
- Helm 3.x (опционально, для Helm deployment)
- 10GB+ persistent storage
- Ingress controller (nginx, traefik) - опционально

---

## Docker Сборка

### 1. Сборка образа локально

**Debug build:**
```bash
docker build -t ultrasharp-tools-server:debug \
  --build-arg BUILD_CONFIGURATION=Debug \
  -f UltrasharpTools.Overlord/Dockerfile \
  .
```

**Release build:**
```bash
docker build -t ultrasharp-tools-server:release \
  --build-arg BUILD_CONFIGURATION=Release \
  -f UltrasharpTools.Overlord/Dockerfile \
  .
```

### 2. Запуск контейнера локально

```bash
docker run -d \
  --name ultrasharp-server \
  -p 3001:3001 \
  -v $(pwd)/data:/app/data \
  -v $(pwd)/logs:/app/logs \
  -e LOG_LEVEL=Information \
  ultrasharp-tools-server:release
```

**Health check:**
```bash
curl http://localhost:3001/health
# Expected: {"status":"healthy"}
```

### 3. Остановка и очистка

```bash
docker stop ultrasharp-server
docker rm ultrasharp-server
```

---

## Kubernetes Развертывание

Манифесты находятся в директории `kubernetes/`.

### Быстрый старт

**1. Создайте namespace:**
```bash
kubectl create namespace ultrasharp-tools
```

**2. Создайте ConfigMap:**
```bash
kubectl apply -f kubernetes/configmap.yaml -n ultrasharp-tools
```

**3. Создайте PersistentVolumeClaim:**
```bash
kubectl apply -f kubernetes/pvc.yaml -n ultrasharp-tools
```

**4. Разверните приложение:**
```bash
kubectl apply -f kubernetes/deployment.yaml -n ultrasharp-tools
kubectl apply -f kubernetes/service.yaml -n ultrasharp-tools
```

**5. (Опционально) Настройте Ingress:**
```bash
# Отредактируйте kubernetes/ingress.yaml (замените ultrasharp.example.com)
kubectl apply -f kubernetes/ingress.yaml -n ultrasharp-tools
```

### Проверка развертывания

```bash
# Статус подов
kubectl get pods -n ultrasharp-tools

# Логи
kubectl logs -f deployment/ultrasharp-tools-server -n ultrasharp-tools

# Port forwarding для локального доступа
kubectl port-forward svc/ultrasharp-tools-server 3001:3001 -n ultrasharp-tools
```

### Обновление образа

```bash
# Обновить образ до latest
kubectl set image deployment/ultrasharp-tools-server \
  ultrasharp-tools=ghcr.io/YOUR_ORG/ultrasharp-tools-overlord:latest \
  -n ultrasharp-tools

# Проверить rollout
kubectl rollout status deployment/ultrasharp-tools-server -n ultrasharp-tools
```

### Откат

```bash
# Откатить к предыдущей версии
kubectl rollout undo deployment/ultrasharp-tools-server -n ultrasharp-tools

# История rollout
kubectl rollout history deployment/ultrasharp-tools-server -n ultrasharp-tools
```

---

## Helm Chart Развертывание

Helm chart находится в `helm/ultrasharp-tools/`.

### Быстрый старт

**1. Установите chart:**
```bash
helm install ultrasharp-tools ./helm/ultrasharp-tools \
  --namespace ultrasharp-tools \
  --create-namespace
```

**2. Кастомизируйте через values:**

Создайте файл `my-values.yaml`:
```yaml
# my-values.yaml
image:
  repository: ghcr.io/YOUR_ORG/ultrasharp-tools-overlord
  tag: "latest"

ingress:
  enabled: true
  hosts:
    - host: ultrasharp.yourdomain.com
      paths:
        - path: /
          pathType: Prefix
  tls:
    - secretName: ultrasharp-tls
      hosts:
        - ultrasharp.yourdomain.com

config:
  logging:
    level: "Debug"
  build:
    configuration: "Release"

resources:
  requests:
    memory: "1Gi"
    cpu: "500m"
  limits:
    memory: "8Gi"
    cpu: "4000m"

persistence:
  size: 50Gi
  storageClassName: "fast-ssd"
```

**3. Установите с custom values:**
```bash
helm install ultrasharp-tools ./helm/ultrasharp-tools \
  -f my-values.yaml \
  --namespace ultrasharp-tools \
  --create-namespace
```

### Управление Helm Release

**Обновление:**
```bash
helm upgrade ultrasharp-tools ./helm/ultrasharp-tools \
  -f my-values.yaml \
  --namespace ultrasharp-tools
```

**Откат:**
```bash
helm rollback ultrasharp-tools -n ultrasharp-tools
```

**Удаление:**
```bash
helm uninstall ultrasharp-tools -n ultrasharp-tools
```

**Проверка значений:**
```bash
helm get values ultrasharp-tools -n ultrasharp-tools
```

---

## GitHub Container Registry

### Настройка GitHub Actions

GitHub Actions автоматически собирает и публикует Docker образы при push в `dev` или `release` ветки.

**Workflow:** `.github/workflows/docker-publish.yml`

**Автоматические теги:**
- `dev` branch → `ghcr.io/YOUR_ORG/ultrasharp-tools-overlord:dev`
- `release` branch → `ghcr.io/YOUR_ORG/ultrasharp-tools-overlord:latest`

### Использование образа из GHCR

**1. Создайте Personal Access Token (PAT) на GitHub:**
   - Settings → Developer settings → Personal access tokens → Tokens (classic)
   - Выберите scopes: `read:packages`

**2. Создайте Secret в Kubernetes:**
```bash
kubectl create secret docker-registry ghcr-secret \
  --docker-server=ghcr.io \
  --docker-username=YOUR_GITHUB_USERNAME \
  --docker-password=YOUR_GITHUB_PAT \
  --docker-email=YOUR_EMAIL \
  -n ultrasharp-tools
```

**3. Используйте secret в deployment:**
```yaml
spec:
  imagePullSecrets:
  - name: ghcr-secret
```

### Ручная публикация в GHCR

```bash
# Login
echo $GITHUB_PAT | docker login ghcr.io -u YOUR_USERNAME --password-stdin

# Tag
docker tag ultrasharp-tools-server:release \
  ghcr.io/YOUR_ORG/ultrasharp-tools-overlord:v3.6.2

# Push
docker push ghcr.io/YOUR_ORG/ultrasharp-tools-overlord:v3.6.2
```

---

## Конфигурация

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ASPNETCORE_URLS` | Server URLs | `http://+:3001` |
| `ASPNETCORE_ENVIRONMENT` | ASP.NET environment | `Production` |
| `GIT_AUTHOR_NAME` | Git commit author name | `UltrasharpTools Bot` |
| `GIT_AUTHOR_EMAIL` | Git commit author email | `ultrasharp-bot@example.com` |
| `LOG_LEVEL` | Log level (Trace, Debug, Information, Warning, Error, Critical) | `Information` |
| `BUILD_CONFIGURATION` | MSBuild configuration | `Debug` |

### ConfigMap Customization

Отредактируйте `kubernetes/configmap.yaml` или Helm `values.yaml`:

```yaml
# Kubernetes ConfigMap
data:
  git.author.name: "Your Name"
  git.author.email: "your@email.com"
  log.level: "Debug"
  build.configuration: "Release"

# Helm values.yaml
config:
  git:
    authorName: "Your Name"
    authorEmail: "your@email.com"
  logging:
    level: "Debug"
  build:
    configuration: "Release"
```

### Persistent Storage

**PVC Configuration:**
```yaml
# kubernetes/pvc.yaml
spec:
  resources:
    requests:
      storage: 50Gi  # Увеличьте для больших проектов
  storageClassName: fast-ssd  # Укажите storage class
```

**Helm values:**
```yaml
persistence:
  enabled: true
  size: 50Gi
  storageClassName: fast-ssd
  accessMode: ReadWriteOnce
```

---

## Мониторинг и Логи

### Health Checks

**Liveness probe:** `/health` (каждые 30s)
**Readiness probe:** `/health` (каждые 10s)

```bash
# Manual health check
kubectl exec -it POD_NAME -n ultrasharp-tools -- curl localhost:3001/health
```

### Логи

**Viewing logs:**
```bash
# Tail logs
kubectl logs -f deployment/ultrasharp-tools-server -n ultrasharp-tools

# Last 100 lines
kubectl logs --tail=100 deployment/ultrasharp-tools-server -n ultrasharp-tools

# Logs from all pods
kubectl logs -l app=ultrasharp-tools-server -n ultrasharp-tools --all-containers
```

**Log levels:**
- `Verbose` - все детали (для debugging)
- `Debug` - детальная информация
- `Information` - стандартный уровень (рекомендуется)
- `Warning` - предупреждения
- `Error` - только ошибки
- `Fatal` - критические ошибки

### Metrics (optional)

Для мониторинга можно добавить Prometheus metrics:

```yaml
# Add to deployment
ports:
- name: metrics
  containerPort: 9090

# ServiceMonitor (если используете Prometheus Operator)
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: ultrasharp-tools
spec:
  selector:
    matchLabels:
      app: ultrasharp-tools-server
  endpoints:
  - port: metrics
```

---

## Troubleshooting

### Pod не стартует

**1. Проверьте статус:**
```bash
kubectl describe pod POD_NAME -n ultrasharp-tools
```

**2. Частые проблемы:**
- **ImagePullBackOff:** Проверьте `imagePullSecrets` для GHCR
- **CrashLoopBackOff:** Проверьте логи: `kubectl logs POD_NAME -n ultrasharp-tools`
- **Pending:** Проверьте PVC: `kubectl get pvc -n ultrasharp-tools`

### Health checks fail

```bash
# Check pod logs
kubectl logs POD_NAME -n ultrasharp-tools

# Check service endpoints
kubectl get endpoints -n ultrasharp-tools

# Manual health check inside pod
kubectl exec -it POD_NAME -n ultrasharp-tools -- curl localhost:3001/health
```

### Out of Memory (OOM)

**Увеличьте лимиты памяти:**

```yaml
# Kubernetes
resources:
  limits:
    memory: "8Gi"
  requests:
    memory: "2Gi"

# Helm values
resources:
  limits:
    memory: "8Gi"
  requests:
    memory: "2Gi"
```

### Persistent storage issues

```bash
# Check PVC status
kubectl get pvc -n ultrasharp-tools

# Describe PVC
kubectl describe pvc ultrasharp-data-pvc -n ultrasharp-tools

# Check if PV is bound
kubectl get pv | grep ultrasharp
```

### Ingress не работает

```bash
# Check ingress
kubectl get ingress -n ultrasharp-tools
kubectl describe ingress ultrasharp-tools-ingress -n ultrasharp-tools

# Check ingress controller logs
kubectl logs -n ingress-nginx -l app.kubernetes.io/name=ingress-nginx

# Test internal connectivity
kubectl run -it --rm debug --image=curlimages/curl --restart=Never -- \
  curl http://ultrasharp-tools-server.ultrasharp-tools.svc.cluster.local:3001/health
```

---

## 📚 Дополнительные ресурсы

- [Main README](../../README.md)
- [MCP Configuration Guide](../Configuration/MCP_Sharp.md)
- [Docker Setup](../Setup/Docker.md)
- **[Semantic Embedding Setup Guide](SEMANTIC_SETUP_GUIDE.md)** - Настройка semantic search
- [GitHub Container Registry Docs](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry)
- [Kubernetes Documentation](https://kubernetes.io/docs/)
- [Helm Documentation](https://helm.sh/docs/)

---

## 🤝 Поддержка

Если возникли проблемы:
1. Проверьте [Troubleshooting](#troubleshooting) секцию
2. Изучите логи: `kubectl logs -f deployment/ultrasharp-tools-server -n ultrasharp-tools`
3. Создайте issue на GitHub: https://github.com/faxenoff/ultrasharp-tools-mcp/issues
