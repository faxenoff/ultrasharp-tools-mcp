namespace UltrasharpTools.Droid.Services.Hybrid;

/// <summary>
/// Сервис для векторизации кода через Ollama или TEI
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Векторизовать текст кода
    /// </summary>
    Task<float[]?> GetEmbeddingAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Проверить доступность embedding сервиса
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
