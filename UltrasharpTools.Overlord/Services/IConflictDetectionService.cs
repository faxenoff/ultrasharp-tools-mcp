namespace UltrasharpTools.Overlord.Services;

/// <summary>
/// Сервис для обнаружения конфликтов и дубликатов кода
/// </summary>
public interface IConflictDetectionService
{
    /// <summary>
    /// Проверяет новый код на наличие дубликатов в других проектах
    /// </summary>
    /// <param name="project">Проект, в котором изменен код</param>
    /// <param name="branch">Ветка</param>
    /// <param name="file">Файл</param>
    /// <param name="vectors">Векторное представление кода</param>
    /// <param name="content">Содержимое файла</param>
    /// <param name="duplicateThreshold">Порог для обнаружения дубликата (по умолчанию 0.85)</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Список обнаруженных дубликатов</returns>
    Task<List<DuplicateMatch>> DetectDuplicatesAsync(
        string project,
        string branch,
        string file,
        float[] vectors,
        string? content,
        double duplicateThreshold = 0.85,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Включить/выключить автоматические уведомления о дубликатах
    /// </summary>
    bool AutoNotifyEnabled { get; set; }

    /// <summary>
    /// Минимальный порог похожести для автоматических уведомлений
    /// </summary>
    double AutoNotifyThreshold { get; set; }
}

/// <summary>
/// Информация о найденном дубликате
/// </summary>
public sealed class DuplicateMatch
{
    public required string Project { get; init; }
    public required string Branch { get; init; }
    public required string File { get; init; }
    public int Line { get; init; }
    public double Similarity { get; init; }
    public string? Code { get; init; }
}
