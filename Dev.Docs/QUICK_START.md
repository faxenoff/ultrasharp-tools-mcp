# UltraSharpTools - Быстрый старт

## 🚀 Установка за 3 шага

### 1. Сборка

```cmd
cd D:\github\ultrasharp-tools-mcp
build-all.cmd
```

**Время сборки:** ~30-60 секунд
**Результат:** Все компоненты в `Run.Build\Droid\`

### 2. Настройка Claude Desktop

Откройте файл конфигурации:
```
%APPDATA%\Claude\claude_desktop_config.json
```

Добавьте:
```json
{
  "mcpServers": {
    "ultrasharp-tools": {
      "command": "D:\\github\\ultrasharp-tools-mcp\\Run.Build\\Droid\\UltraSharp-tools.com"
    }
  }
}
```

**Важно:**
- Используйте двойные обратные слэши `\\` в пути!
- `UltraSharp-tools.com` — универсальный Comm (~700 KB, работает на Win/Linux/macOS)

### 3. Перезапуск

Перезапустите Claude Desktop.

## ✅ Проверка

Откройте чат в Claude Desktop и проверьте:
- Иконка молотка (инструменты) должна быть доступна
- Должны быть видны инструменты: `load_solution`, `view_definition`, и другие

## 🔧 Основные команды

### Загрузка проекта

```
Загрузи решение D:\MyProject\MyProject.sln
```

Или используйте команду `load_solution`:
```
load_solution("D:\\MyProject\\MyProject.sln")
```

### Просмотр кода

```
Покажи определение класса MyNamespace.MyClass
```

### Изменение кода

```
Добавь метод GetUserById в класс UserService
```

## 📋 Полезные инструменты

| Инструмент | Описание |
|------------|----------|
| `load_solution` | Загрузить .sln файл |
| `load_project` | Получить карту проекта (namespaces → types) |
| `view_definition` | Просмотреть код метода/класса |
| `get_members` | Получить список членов типа |
| `find_references` | Найти все использования символа |
| `modify_code` | Изменить код (с Git commit) |
| `format_code` | Отформатировать код через CSharpier |
| `analyze_code_style` | Найти проблемы качества кода |

## 🐛 Troubleshooting

### Comm не запускается

**Проблема:** Отсутствует `UltrasharpTools.Droid.exe` в той же папке

**Решение:**
```cmd
build-all.cmd
```

### Долгий первый запуск (~30 секунд)

**Это нормально!** Droid инициализирует Roslyn workspace при первом старте.
Последующие подключения будут мгновенными.

### Инструменты не появились в Claude Desktop

1. Проверьте путь в `claude_desktop_config.json`
2. Убедитесь, что используете `\\` вместо `\`
3. Перезапустите Claude Desktop полностью (закройте все окна)

### Ошибка "Native AOT requires Visual Studio"

**Решение:** Используйте Debug сборку вместо Release:
```cmd
build-all.cmd       # ✅ Debug (работает без VS)
build-all-release.cmd  # ❌ Release (требует VS)
```

## 📚 Дополнительная документация

- **BUILD_SCRIPTS.md** - полная документация по сборке
- **Run.Build/Droid/README_COMM.md** - детали про Comm прокси
- **Dev.Docs/CLAUDE.md** - руководство для разработки

## 🔗 Полезные ссылки

- **Логи Droid:** `D:\github\ultrasharp-tools-mcp\.ultrasharp\logs\`
- **Конфигурация:** `Run.Build\Droid\Config\`

## 💡 Советы

1. **Git интеграция включена по умолчанию**: Все изменения кода создают ветку `ultrasharptools/YYYYMMDD-HHMMSS` и коммитятся автоматически

2. **Symbol cache ускоряет загрузку**: Первая загрузка .sln может занять минуту, но последующие будут быстрее

3. **FQN поиск с fuzzy matching**: Вы можете использовать неточные имена типов, система найдёт ближайшее совпадение

4. **Один Droid для всех клиентов**: Comm автоматически переиспользует запущенный Droid процесс

Приятного использования! 🚀
