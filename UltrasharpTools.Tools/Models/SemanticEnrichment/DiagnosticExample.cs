namespace UltrasharpTools.Tools.Models.SemanticEnrichment;

/// <summary>
/// Пример диагностики для репрезентации кластера
/// </summary>
public class DiagnosticExample
{
    /// <summary>
    /// Абсолютный путь к файлу
    /// </summary>
    public required string FilePath { get; set; }

    /// <summary>
    /// Номер строки
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// Фрагмент кода (snippet)
    /// </summary>
    public string? Snippet { get; set; }

    /// <summary>
    /// Сходство с центроидом кластера (0.0-1.0)
    /// </summary>
    public double SimilarityToCentroid { get; set; }
}
