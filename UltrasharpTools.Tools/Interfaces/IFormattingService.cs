namespace UltrasharpTools.Tools.Interfaces;

/// <summary>
/// Сервис для форматирования кода с использованием CSharpier
/// </summary>
public interface IFormattingService
{
/// <summary>
/// Форматирует файлы по указанному пути
/// </summary>
/// <param name="path">Путь к файлу или директории</param>
/// <param name="checkOnly">Только проверка без применения изменений</param>
/// <param name="cancellationToken">Токен отмены</param>
/// <returns>Результат форматирования с информацией о файлах</returns>
Task<FormattingResult> FormatAsync(string path, bool checkOnly, CancellationToken cancellationToken = default);
}

/// <summary>
/// Результат операции форматирования
/// </summary>
public record FormattingResult
{
/// <summary>
/// Файлы, требующие форматирования
/// </summary>
public required List<string> FilesNeedingFormatting { get; init; }

/// <summary>
/// Отформатированные файлы (только если checkOnly = false)
/// </summary>
public required List<string> FilesFormatted { get; init; }

/// <summary>
/// Общее количество проверенных файлов
/// </summary>
public required int TotalFilesChecked { get; init; }

/// <summary>
/// Ошибки форматирования
/// </summary>
public List<(string FilePath, string Error)> Errors { get; init; } = [];
}
