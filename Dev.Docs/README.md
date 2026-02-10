# Developer Documentation

Документация для разработчиков UltrasharpTools MCP.

**Версия проекта:** 3.7.2
**Статус:** Production Ready
**Последнее обновление:** 2025-11-27

---

## 📚 Начало работы

### Для пользователей

1. **[README.md](../README.md)** - Обзор проекта и quick start
2. **[ARCHITECTURE.md](../ARCHITECTURE.md)** - Архитектура системы ⭐ NEW
3. **[USAGE_GUIDE.md](../USAGE_GUIDE.md)** - Практическое руководство с примерами ⭐ NEW
4. **[ROADMAP.md](../ROADMAP.md)** - Планы развития проекта ⭐ NEW

### Для разработчиков

1. **[CLAUDE.md](CLAUDE.md)** - Инструкции для работы с Claude Code
2. **[Development/](Development/)** - Процессы разработки и TODO
3. **[Architecture/](Architecture/)** - Архитектурные решения
4. **[Features/](Features/)** - Документация по фичам
5. **[Performance/](Performance/)** - Benchmarks и оптимизации

---

## 📁 Структура документации

### 🏗️ Architecture/ - Архитектурные решения

Дизайн-документы и архитектурные концепции:

- **[HYBRID_MODE.md](Architecture/HYBRID_MODE.md)** - Hybrid Mode (local + remote) ✅ Implemented
- **[HYBRID_IMPLEMENTATION.md](Architecture/HYBRID_IMPLEMENTATION.md)** - Детали реализации
- **[UNIFIED_DUAL_MODE.md](Architecture/UNIFIED_DUAL_MODE.md)** - Концепция единого режима
- **[UNIVERSAL_SEMANTIC_MODE_SUMMARY.md](Architecture/UNIVERSAL_SEMANTIC_MODE_SUMMARY.md)** - Universal Semantic Mode ✅ Complete
- **[CODE_MODELS_SELECTION.md](Architecture/CODE_MODELS_SELECTION.md)** - Выбор embedding моделей
- **[PRECOMMIT_HOOK_DESIGN.md](Architecture/PRECOMMIT_HOOK_DESIGN.md)** - Pre-commit hook design
- **[GPU_DEPLOYMENT_OPTIONS.md](Architecture/GPU_DEPLOYMENT_OPTIONS.md)** - GPU deployment опции

**Ключевые концепции:**
- Три-проектная структура (Tools, Droid, Overlord)
- Layered Symbol Indexing (3-layer architecture)
- Universal Semantic Mode (Phase 12)
- Hybrid Mode для team collaboration

### 🚀 Features/ - Документация по фичам

Каждая фича документирована по полному циклу разработки:
1. **Design** - дизайн и архитектурные решения
2. **Examples** - примеры использования и тестовые сценарии
3. **Implementation** - детали имплементации
4. **Summary** - итоги и результаты

**Реализованные фичи:**

#### [SemanticMerge/](Features/SemanticMerge/) - Умное 3-way слияние
- ✅ Movement detection (методы перемещены между классами)
- ✅ Rename detection (класс/метод переименован)
- ✅ Conflict resolution с ML recommendations

#### [RAG/](Features/RAG/) - Semantic search integration
- ✅ Integration_Design.md - Дизайн векторного поиска

#### [Tracing/](Features/Tracing/) - Advanced static tracing
- ✅ ADVANCED_TRACING.md - CFG tracing, symbolic execution
- ✅ TRACING_OPTIMIZATION.md - Call graph caching (5-10x speedup)

#### [Semantic/](Features/Semantic/) - Semantic Mode Discovery ⭐ NEW
- ✅ SEMANTIC_MODE_DISCOVERY_ANALYSIS.md - AI capability discovery
- ✅ SEMANTIC_MODE_LIFECYCLE_ANALYSIS.md - Lifecycle management design
- ✅ PHASE_1_SEMANTIC_DISCOVERY.md - Phase 1 implementation (complete)

### ⚡ Performance/ - Производительность

Отчеты по оптимизациям и бенчмаркам:

- **[Test_Report.md](Performance/Test_Report.md)** - Комплексный отчёт по тестированию ⭐ LATEST
  - 890K символов, 112 git branches
  - Cache performance: 5-10x speedup
  - 80% tool pass rate

- **[Results.md](Performance/Results.md)** - Сводные результаты оптимизаций
- **[PARALLELIZATION_SUMMARY.md](Performance/PARALLELIZATION_SUMMARY.md)** - Параллелизация и ранний выход
- **[JSON_Hash.md](Performance/JSON_Hash.md)** - xxHash32 (2-3x faster)

**Ключевые метрики:**
- Solution load: 48.3s cold → 8.9s warm (5.4x speedup)
- Branch switch: 48.3s → 16.6ms (330x speedup!)
- TraceBackwards: 2-3s cold → 400-600ms warm (5-7x speedup)

### 🔧 Development/ - Процессы разработки

Инструкции и планы для разработчиков:

- **[TODO.md](Development/TODO.md)** - Актуальный список задач ⭐ ACTIVE
  - 10/26 задач выполнено
  - 155-220 часов работы осталось

- **[IMPLEMENTATION_SUMMARY.md](Development/IMPLEMENTATION_SUMMARY.md)** - Layered Indexing сводка
- **[LAYERED_INDEXING_DESIGN.md](Development/LAYERED_INDEXING_DESIGN.md)** - 3-layer архитектура
- **[LAYERED_INDEXING_USAGE.md](Development/LAYERED_INDEXING_USAGE.md)** - Usage guide
- **[THREE_LAYER_EXTENSION.md](Development/THREE_LAYER_EXTENSION.md)** - Layer 2 (working delta)
- **[INCREMENTAL_INDEXING_ANALYSIS.md](Development/INCREMENTAL_INDEXING_ANALYSIS.md)** - Incremental updates
- **[UNIFIED_IMPLEMENTATION_PLAN.md](Development/UNIFIED_IMPLEMENTATION_PLAN.md)** - Общий план
- **[Normalization.md](Development/Normalization.md)** - Code style рекомендации

### 📖 Специализированные документы

- **[CLAUDE.md](CLAUDE.md)** - Инструкции для Claude Code при работе с проектом
- **[TOKENIZER_CONVERTER_README.md](TOKENIZER_CONVERTER_README.md)** - Tokenizer conversion utilities
- **[ULTRA-SHARPED.md](ULTRA-SHARPED.md)** - Ultra-detailed changelog & feature list

---

## 🎯 Quick Links по задачам

### Хочу понять архитектуру
→ [../ARCHITECTURE.md](../ARCHITECTURE.md) - Complete architecture overview

### Хочу использовать инструмент
→ [../USAGE_GUIDE.md](../USAGE_GUIDE.md) - Практические примеры

### Хочу добавить новую фичу
→ [Development/TODO.md](Development/TODO.md) - Что планируется
→ [Architecture/](Architecture/) - Как проектировать

### Хочу оптимизировать производительность
→ [Performance/Test_Report.md](Performance/Test_Report.md) - Текущие benchmarks
→ [Development/LAYERED_INDEXING_DESIGN.md](Development/LAYERED_INDEXING_DESIGN.md) - Ключевая оптимизация

### Хочу понять Universal Semantic Mode
→ [Architecture/UNIVERSAL_SEMANTIC_MODE_SUMMARY.md](Architecture/UNIVERSAL_SEMANTIC_MODE_SUMMARY.md) - Полное резюме
→ [../UNIVERSAL_SEMANTIC_MODE.md](../UNIVERSAL_SEMANTIC_MODE.md) - Design doc

### Хочу поработать с semantic search
→ [Features/RAG/Integration_Design.md](Features/RAG/Integration_Design.md) - RAG integration
→ [../Run.Docs/Deployment/SEMANTIC_SETUP_GUIDE.md](../Run.Docs/Deployment/SEMANTIC_SETUP_GUIDE.md) - Setup guide

### Хочу понять Semantic Mode Discovery (AI capability detection)
→ [Features/Semantic/PHASE_1_SEMANTIC_DISCOVERY.md](Features/Semantic/PHASE_1_SEMANTIC_DISCOVERY.md) - Implementation summary
→ [Features/Semantic/SEMANTIC_MODE_DISCOVERY_ANALYSIS.md](Features/Semantic/SEMANTIC_MODE_DISCOVERY_ANALYSIS.md) - Analysis & design
→ [Features/Semantic/SEMANTIC_MODE_LIFECYCLE_ANALYSIS.md](Features/Semantic/SEMANTIC_MODE_LIFECYCLE_ANALYSIS.md) - Lifecycle management

### Хочу поработать с tracing tools
→ [Features/Tracing/ADVANCED_TRACING.md](Features/Tracing/ADVANCED_TRACING.md) - CFG tracing & symbolic execution
→ [Features/Tracing/TRACING_OPTIMIZATION.md](Features/Tracing/TRACING_OPTIMIZATION.md) - Call graph caching & performance

---

## 📝 Добавление новой фичи

При добавлении новой фичи создайте директорию в `Features/` с файлами:

```
Features/YourFeature/
├── 01_Design.md          # Дизайн и архитектура
├── 02_Examples.md        # Примеры использования
├── 03_Implementation.md  # Детали имплементации
└── 04_Summary.md         # Результаты и итоги
```

**Образец:** [Features/SemanticMerge/](Features/SemanticMerge/)

**Процесс:**
1. Создайте design doc (01_Design.md)
2. Напишите примеры использования (02_Examples.md)
3. Имплементируйте фичу
4. Задокументируйте реализацию (03_Implementation.md)
5. Напишите итоговый summary (04_Summary.md)
6. Добавьте ссылку в этот README

---

## 🗂️ Архивная документация

Документы по завершённым фазам (Phase 1-12.3) перемещены в:
→ [../Dev.Archive/](../Dev.Archive/README.md)

**Что там:**
- PHASE_*_COMPLETION.md (Phase 5-12.3)
- Старые архитектурные документы
- Completion summaries

**Зачем архивировать:**
- Чистота основного repo
- Историческая справка сохранена
- Актуальная документация легко найти

---

## 🔗 External Resources

### Roslyn
- [Roslyn API Documentation](https://learn.microsoft.com/en-us/dotnet/csharp/roslyn-sdk/)
- [Roslyn GitHub](https://github.com/dotnet/roslyn)

### MCP Protocol
- [Model Context Protocol](https://github.com/modelcontextprotocol)
- [MCP SDK (.NET)](https://github.com/modelcontextprotocol/sdk-dotnet)

### Related Tools
- [LibGit2Sharp](https://github.com/libgit2/libgit2sharp) - Git integration
- [CSharpier](https://csharpier.com/) - Code formatting
- [Z3 Solver](https://github.com/Z3Prover/z3) - Symbolic execution

---

## 📊 Documentation Status

| Категория | Статус | Покрытие |
|-----------|--------|----------|
| Architecture | ✅ Complete | 100% |
| Features | ✅ Complete | 100% documented |
| Performance | ✅ Complete | Latest benchmarks |
| Development | ✅ Active | TODO updated |
| API Reference | ⚠️ Partial | XML comments exist |

---

## 🤝 Contributing to Documentation

Нашли ошибку или хотите улучшить документацию?

1. Проверьте существующие документы
2. Следуйте структуре (Design → Examples → Implementation → Summary)
3. Используйте markdown best practices
4. Добавьте ссылки в этот README
5. Submit PR

---

**Версия:** 1.0
**Последнее обновление:** 2025-11-27
**Maintainers:** UltrasharpTools Team
