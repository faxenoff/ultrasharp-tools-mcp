namespace UltrasharpTools.Tools.Replace.Models;

/// <summary>
/// Уровень контекста для извлечения кода.
/// </summary>
public enum ReplaceScope
{
    /// <summary>Только statement где найден паттерн</summary>
    Statement,

    /// <summary>Весь блок (if/while/try/switch)</summary>
    Block,

    /// <summary>Весь метод/property/field</summary>
    Member,

    /// <summary>Весь класс/struct/interface/record</summary>
    Type,

    /// <summary>Весь файл</summary>
    File
}

/// <summary>
/// Режим поиска паттернов.
/// </summary>
public enum SearchMode
{
    /// <summary>Regex поиск по тексту</summary>
    Regex,

    /// <summary>Roslyn semantic search по FQN</summary>
    Roslyn,

    /// <summary>Embedding-based семантический поиск</summary>
    Semantic
}

/// <summary>
/// Режим применения изменений.
/// </summary>
public enum ApplyMode
{
    /// <summary>Откат всех изменений при любой ошибке</summary>
    AllOrNothing,

    /// <summary>Применить успешные, пропустить failed</summary>
    BestEffort
}
