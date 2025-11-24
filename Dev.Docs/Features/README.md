# Features Documentation

Документация по фичам UltrasharpTools с полным циклом разработки.

## 📋 Структура документации фичи

Каждая фича документирована в 4 этапа:

1. **01_Design.md** - Дизайн и архитектурные решения
   - Проблема и мотивация
   - Архитектурный дизайн
   - Технические решения
   - API и интерфейсы

2. **02_Examples.md** - Примеры использования
   - Use cases
   - Примеры кода
   - Тестовые сценарии
   - Edge cases

3. **03_Implementation.md** - Детали имплементации
   - Структура кода
   - Ключевые алгоритмы
   - Интеграция с существующим кодом
   - Проблемы и решения

4. **04_Summary.md** - Итоги и результаты
   - Что реализовано
   - Метрики и производительность
   - Известные ограничения
   - Планы на будущее

## 🎯 Доступные фичи

### [SemanticMerge/](SemanticMerge/)
**Semantic 3-Way Code Merging**

Интеллектуальное слияние кода на основе семантического анализа вместо текстового diff.

- ✅ Полная документация (Design → Examples → Implementation → Summary)
- 🎯 Образец качественной документации фичи
- 📊 6 фаз разработки с детальными отчетами

**Ключевые возможности:**
- Multi-format parsing (C#, JSON, YAML, PowerShell, Shell)
- Structural fingerprinting для точного матчинга
- Movement detection и semantic alignment
- Conflict resolution с ML-подсказками

### [RAG/](RAG/)
**RAG Integration**

Интеграция с RAG (Retrieval-Augmented Generation) для векторного поиска по кодовой базе.

- 📖 [Integration_Design.md](RAG/Integration_Design.md) - дизайн-документ

**Ключевые возможности:**
- Векторные embeddings для кода
- Семантический поиск похожих методов/классов
- Интеграция с Ollama/TEI

### [Tracing/](Tracing/)
**Advanced Static Tracing (Phases 5-6)**

Расширенные возможности статического трейсинга и анализа выполнения.

- 📖 [ADVANCED_TRACING.md](Tracing/ADVANCED_TRACING.md) - Advanced tracing features
- 📖 [TRACING_OPTIMIZATION.md](Tracing/TRACING_OPTIMIZATION.md) - Tracing optimizations

**Ключевые возможности:**
- TraceExecution - статический CFG-based трейсинг
- TraceBackwards - обратный трейсинг с кешированием
- AnalyzePathFeasibility - символьное выполнение (Z3)
- Persistent SQLite caching (5-10x speedup)

## 🚀 Добавление новой фичи

1. Создайте директорию: `mkdir Features/YourFeature`
2. Создайте 4 файла:
   ```
   01_Design.md
   02_Examples.md
   03_Implementation.md
   04_Summary.md
   ```
3. Следуйте структуре SemanticMerge как образцу
4. Обновите этот README, добавив описание

## 📝 Шаблон для новой фичи

См. [SemanticMerge/](SemanticMerge/) - полный образец всех 4 документов.

**Минимальный набор:**
- Design: Проблема, решение, архитектура
- Examples: 3-5 use cases с кодом
- Implementation: Ключевые классы и алгоритмы
- Summary: Результаты, метрики, ограничения
