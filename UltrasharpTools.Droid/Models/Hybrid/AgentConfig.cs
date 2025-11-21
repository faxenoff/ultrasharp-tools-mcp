namespace UltrasharpTools.Droid.Models.Hybrid;

/// <summary>
/// Конфигурация для hybrid mode
/// </summary>
public sealed class AgentConfig
{
    /// <summary>
    /// Имя проекта
    /// </summary>
    public required string ProjectName { get; init; }

    /// <summary>
    /// Путь к репозиторию
    /// </summary>
    public required string RepositoryPath { get; init; }

    /// <summary>
    /// URL Overlord сервера
    /// </summary>
    public required string ServerUrl { get; init; }

    /// <summary>
    /// URL embedding сервиса (Ollama/TEI)
    /// </summary>
    public string EmbeddingUrl { get; init; } = "http://localhost:11434";

    /// <summary>
    /// Название модели для embedding
    /// </summary>
    public string EmbeddingModel { get; init; } = "nomic-embed-text";

    /// <summary>
    /// Интервал проверки Git изменений (мс)
    /// </summary>
    public int GitCheckIntervalMs { get; init; } = 5000;

    /// <summary>
    /// Debounce для файловых изменений (мс)
    /// </summary>
    public int FileWatcherDebounceMs { get; init; } = 500;

    /// <summary>
    /// Включить автоматическую векторизацию
    /// </summary>
    public bool AutoVectorizeEnabled { get; init; } = true;

    /// <summary>
    /// Паттерны файлов для отслеживания
    /// </summary>
    public string[] WatchPatterns { get; init; } = new[] { "*.cs" };

    /// <summary>
    /// Паттерны файлов для игнорирования
    /// </summary>
    public string[] IgnorePatterns { get; init; } =
        new[] { "obj/**", "bin/**", ".git/**", ".vs/**", "*.Designer.cs", "*.generated.cs" };
}
