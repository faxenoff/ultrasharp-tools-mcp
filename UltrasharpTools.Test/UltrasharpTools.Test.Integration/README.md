# Integration Tests & Benchmarks для Universal Semantic Mode

Этот проект содержит интеграционные тесты и performance benchmarks для всех 15 enrichment стратегий Phase 12.

## Структура

- **EnrichmentIntegrationTests.cs** - Интеграционные тесты (23 теста)
- **EnrichmentBenchmarks.cs** - Performance benchmarks (30+ бенчмарков)

## Запуск интеграционных тестов

```bash
# Из корня репозитория
dotnet test UltrasharpTools.Test/UltrasharpTools.Test.Integration

# С подробным выводом
dotnet test UltrasharpTools.Test/UltrasharpTools.Test.Integration --verbosity normal
```

### Покрытие тестами

#### Phase 12.1 - Core Strategies (7 тестов)
1. ✅ `view_definition` - ViewDefinitionEnrichmentStrategy
2. ✅ `find_references` - FindReferencesEnrichmentStrategy
3. ✅ `overwrite_member` - ModifyCodeEnrichmentStrategy
4. ✅ `add_member` - ModifyCodeEnrichmentStrategy
5. ✅ `rename_symbol` - ModifyCodeEnrichmentStrategy
6. ✅ `get_members` - GetMembersEnrichmentStrategy
7. ✅ `analyze_complexity` - AnalyzeComplexityEnrichmentStrategy

#### Phase 12.2 - Extended Strategies (8 тестов)
8. ✅ `find_all_references` - FindAllReferencesEnrichmentStrategy
9. ✅ `list_types` - ListTypesEnrichmentStrategy
10. ✅ `search_symbols` - SearchSymbolsEnrichmentStrategy
11. ✅ `trace_execution` - TraceExecutionEnrichmentStrategy
12. ✅ `analyze_code_style` - AnalyzeCodeStyleEnrichmentStrategy
13. ✅ `get_type_hierarchy` - GetTypeHierarchyEnrichmentStrategy
14. ✅ `get_project_structure` - GetProjectStructureEnrichmentStrategy
15. ✅ `find_usages` - FindUsagesEnrichmentStrategy
16. ✅ `get_diagnostics` - GetDiagnosticsEnrichmentStrategy
17. ✅ `apply_code_fixes` - ApplyCodeFixesEnrichmentStrategy

#### Cross-Strategy Tests (3 теста)
- ✅ Consistency check для всех 15 стратегий
- ✅ Configuration disabled behavior
- ✅ Graceful handling of missing arguments

## Запуск benchmarks

```bash
# Запуск всех бенчмарков (Release mode обязателен!)
dotnet run -c Release --project UltrasharpTools.Test/UltrasharpTools.Test.Integration

# Результаты сохраняются в BenchmarkDotNet.Artifacts/results/
```

### Категории бенчмарков

#### Core Strategies (7 бенчмарков)
- `Benchmark_ViewDefinition`
- `Benchmark_FindReferences`
- `Benchmark_OverwriteMember`
- `Benchmark_AddMember`
- `Benchmark_RenameSymbol`
- `Benchmark_GetMembers`
- `Benchmark_AnalyzeComplexity`

#### Extended Strategies (10 бенчмарков)
- `Benchmark_FindAllReferences`
- `Benchmark_ListTypes`
- `Benchmark_SearchSymbols`
- `Benchmark_TraceExecution`
- `Benchmark_AnalyzeCodeStyle`
- `Benchmark_GetTypeHierarchy`
- `Benchmark_GetProjectStructure`
- `Benchmark_FindUsages`
- `Benchmark_GetDiagnostics`
- `Benchmark_ApplyCodeFixes`

#### Comparative Benchmarks (4 бенчмарка)
- `Baseline_NoEnrichment` - Baseline для сравнения
- `Benchmark_CheckAvailability` - Overhead проверки доступности
- `Benchmark_GetEmbedding` - Overhead получения вектора
- `Benchmark_AvailabilityWithCache` - Overhead с кешированием

#### Stress Tests (4 бенчмарка)
- `Stress_Sequential_10_ViewDefinitions` - 10 последовательных вызовов
- `Stress_Parallel_10_ViewDefinitions` - 10 параллельных вызовов
- `Stress_MixedStrategies_Sequential` - Микс стратегий последовательно
- `Stress_MixedStrategies_Parallel` - Микс стратегий параллельно

## Особенности

### Mock Embedding Service
Тесты используют mock embedding service, который:
- Генерирует векторы размерности 768 (как nomic-embed-text)
- Детерминирован на основе hash текста
- Не требует реального embedding endpoint

### Без Overlord
Тесты работают только в **Local mode** (без Overlord):
- Не требуют развёрнутого Overlord сервера
- Проверяют работу с локальным embedding service
- Используют `SemanticModeSource.Local`

### Performance Target
Ожидаемые показатели (с mock embedding):
- Baseline: < 1ms
- Individual strategy: < 50ms
- Sequential 10x: < 500ms
- Parallel 10x: < 200ms

### Memory
BenchmarkDotNet измеряет:
- Allocated memory per operation
- Gen0/Gen1/Gen2 collections
- Memory efficiency comparison

## Анализ результатов

После запуска бенчмарков смотрите:
1. **Mean** - среднее время выполнения
2. **Allocated** - выделенная память
3. **Ratio** - отношение к baseline
4. **Gen0/Gen1/Gen2** - сборки мусора

Примерный вывод:
```
| Method                           | Mean      | Allocated |
|--------------------------------- |----------:|----------:|
| Baseline_NoEnrichment           |   0.05 us |         - |
| Benchmark_ViewDefinition        |  45.23 us |   12.5 KB |
| Benchmark_FindReferences        |  43.67 us |   12.1 KB |
| Stress_Parallel_10_ViewDefs     | 187.45 us |  125.0 KB |
```

## CI/CD Integration

Добавьте в pipeline:
```yaml
- name: Run Integration Tests
  run: dotnet test UltrasharpTools.Test/UltrasharpTools.Test.Integration

- name: Run Benchmarks
  run: dotnet run -c Release --project UltrasharpTools.Test/UltrasharpTools.Test.Integration

- name: Upload Benchmark Results
  uses: actions/upload-artifact@v3
  with:
    name: benchmark-results
    path: UltrasharpTools.Test/UltrasharpTools.Test.Integration/BenchmarkDotNet.Artifacts/
```

## Troubleshooting

### Тесты падают с timeout
- Увеличьте `config.Enrichment.TimeoutSeconds` в тестах
- Проверьте, что mock embedding service настроен правильно

### Бенчмарки не запускаются
- Убедитесь, что используете Release mode: `-c Release`
- Проверьте, что BenchmarkDotNet установлен

### High memory allocation
- Проверьте размер векторов (должен быть 768)
- Убедитесь, что кеширование работает
- Проверьте, нет ли memory leaks в стратегиях
