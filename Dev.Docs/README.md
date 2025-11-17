# Developer Documentation

Документация для разработчиков UltrasharpTools MCP.

## 📁 Структура

### Features/ - Документация по фичам

Каждая фича документирована по полному циклу разработки:
1. **Design** - дизайн и архитектурные решения
2. **Examples** - примеры использования и тестовые сценарии
3. **Implementation** - детали имплементации
4. **Summary** - итоги и результаты

**Доступные фичи:**
- [**SemanticMerge/**](Features/SemanticMerge/) - Семантическое 3-way слияние кода
- [**RAG/**](Features/RAG/) - Интеграция с RAG для векторного поиска
- [**Tracing/**](Features/Tracing/) - Расширенный статический трейсинг (Phases 5-6)

### Performance/ - Отчеты о производительности

Детальные отчеты по оптимизациям и бенчмаркам:
- [JSON_Hash.md](Performance/JSON_Hash.md) - Переход на xxHash для ускорения
- [Phase4_Summary.md](Performance/Phase4_Summary.md) - Параллелизация и ранний выход
- [Results.md](Performance/Results.md) - Сводные результаты оптимизаций
- [Test_Report.md](Performance/Test_Report.md) - Детальные тесты производительности

### Development/ - Разработка

Инструкции и планы для разработчиков:
- [TODO.md](Development/TODO.md) - Список задач и идей
- [Normalization.md](Development/Normalization.md) - Рекомендации по нормализации кода

### CLAUDE.md

Инструкции для Claude Code при работе с этим проектом.

## 🚀 Quick Start для новых разработчиков

1. Прочитайте [SemanticMerge Design](Features/SemanticMerge/01_Design.md) как образец полной документации фичи
2. Изучите [Performance Results](Performance/Results.md) для понимания текущих оптимизаций
3. Проверьте [TODO](Development/TODO.md) для идей по улучшению

## 📝 Добавление новой фичи

При добавлении новой фичи создайте директорию в `Features/` с файлами:

```
Features/YourFeature/
├── 01_Design.md          # Дизайн и архитектура
├── 02_Examples.md        # Примеры использования
├── 03_Implementation.md  # Детали имплементации
└── 04_Summary.md         # Результаты и итоги
```

Образец: [Features/SemanticMerge/](Features/SemanticMerge/)
