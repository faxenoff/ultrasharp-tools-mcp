namespace UltrasharpTools.Tools.Merge.Git {
    /// <summary>
    /// Статус изменения файла в git diff.
    /// </summary>
    public enum GitFileStatus {
        Added,      // A - файл добавлен
        Modified,   // M - файл изменён
        Deleted,    // D - файл удалён
        Renamed,    // R - файл переименован
        Copied,     // C - файл скопирован
        Unknown     // Неизвестный статус
    }

    /// <summary>
    /// Информация об изменённом файле с его статусом.
    /// </summary>
    public sealed record GitFileChange(string Path, GitFileStatus Status, string? OldPath = null);    // Namespace content
}
