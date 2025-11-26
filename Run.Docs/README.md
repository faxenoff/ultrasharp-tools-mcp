# User Documentation

Документация для пользователей UltrasharpTools MCP.

## 📁 Структура

### Tools/ - Руководства по MCP инструментам

Подробная документация по каждой категории MCP tools:

**Основные категории:**
- [**Analysis.md**](Tools/Analysis.md) - Инструменты анализа кода
  - Поиск символов, референсов, имплементаций
  - Анализ наследования и зависимостей
  - Complexity analysis
- [**Modification.md**](Tools/Modification.md) - Модификация кода
  - Добавление/изменение членов
  - Переименование символов
  - Find & Replace
- [**Quality.md**](Tools/Quality.md) - Инструменты качества кода
  - FormatCode (CSharpier)
  - AnalyzeCodeStyle (Roslyn analyzers)
  - ApplyCodeFixes
- [**Tracing.md**](Tools/Tracing.md) - Debugging & Diagnostics
  - TraceExecution - статический трейсинг
  - TraceBackwards - обратный трейсинг от краша
  - AnalyzePathFeasibility - символьное выполнение (Z3)
- [**LogAnalysis.md**](Tools/LogAnalysis.md) - Анализ лог-файлов
- [**Document.md**](Tools/Document.md) - Работа с файлами
- [**Solution.md**](Tools/Solution.md) - Загрузка и навигация по решению

### Setup/ - Инструкции по настройке

Пошаговые инструкции для развертывания:
- [**Access-Modes.md**](Setup/Access-Modes.md) - ⭐ **Режимы работы и доступ к файлам** (Local vs Remote)
- [**Embeddings.md**](Setup/Embeddings.md) - Настройка векторных embeddings (Ollama/TEI)
- [**Docker.md**](Setup/Docker.md) - Docker setup для компонентов
- [**Requirements.md**](Setup/Requirements.md) - Требования к оборудованию
- [**Nvidia_FAQ.md**](Setup/Nvidia_FAQ.md) - FAQ по NVIDIA Container Toolkit

### Configuration/ - Настройка клиентовПримеры конфигурации для различных MCP клиентов:- [**MCP_Claude.md**](Configuration/MCP_Claude.md) - Claude Desktop и Claude Code- [**MCP_Sharp.md**](Configuration/MCP_Sharp.md) - Полное руководство по UltrasharpTools### Deployment/ - Развертывание OverlordПолное руководство по развертыванию UltrasharpTools Overlord в Kubernetes:- [**README.md**](Deployment/README.md) - Главное руководство по развертыванию- **kubernetes/** - K8s манифесты (deployment, service, configmap, ingress, PVC)- **helm/ultrasharp-tools/** - Helm chart для гибкого развертывания- **UltrasharpTools.Overlord/Dockerfile** - Multi-stage Docker образ- **.github/workflows/docker-publish.yml** - CI/CD для автоматической публикации в GHCR

## 🚀 Quick Start

1. **Установка:** См. [главный README](../README.md#prerequisites)
2. **Настройка клиента:** [Configuration/MCP_Claude.md](Configuration/MCP_Claude.md)
3. **Первые шаги:** [Tools/Solution.md](Tools/Solution.md) - загрузка решения
4. **Работа с кодом:** [MCP_Sharp.md](Configuration/MCP_Sharp.md) - полный гайд

## 📖 Рекомендуемый порядок чтения

### Для новых пользователей:
1. [MCP_Sharp.md](Configuration/MCP_Sharp.md) - общий обзор возможностей
2. [Solution.md](Tools/Solution.md) - как начать работу
3. [Analysis.md](Tools/Analysis.md) - навигация по кодовой базе
4. [Modification.md](Tools/Modification.md) - как изменять код

### Для продвинутых сценариев:
- [Tracing.md](Tools/Tracing.md) - статический дебаггинг и анализ
- [Quality.md](Tools/Quality.md) - автоматическое улучшение кода
- [LogAnalysis.md](Tools/LogAnalysis.md) - анализ логов и крашей

## 💡 Полезные ссылки

- [Главный README](../README.md) - общее описание проекта
- [Developer Docs](../Dev.Docs/) - документация для разработчиков
- [GitHub Issues](https://github.com/anthropics/ultrasharp-tools-mcp/issues) - баг-репорты и запросы
