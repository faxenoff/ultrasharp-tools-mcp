namespace UltrasharpTools.Overlord.Services;

/// <summary>
/// Сервис для векторизации текста через Ollama или TEI
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Векторизовать текст
    /// </summary>
    Task<float[]?> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверить доступность embedding сервиса
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
