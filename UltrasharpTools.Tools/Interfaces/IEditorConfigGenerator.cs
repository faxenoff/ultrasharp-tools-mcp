using UltrasharpTools.Tools.Models.SemanticEnrichment;

namespace UltrasharpTools.Tools.Interfaces;

/// <summary>
/// Сервис для генерации .editorconfig рекомендаций (Phase 2)
/// </summary>
public interface IEditorConfigGenerator
{
    /// <summary>
    /// Генерирует .editorconfig рекомендации на основе semantic enrichment
    /// </summary>
    /// <param name="enrichmentResult">Результат semantic enrichment (Phase 1)</param>
    /// <param name="options">Опции генерации</param>
    /// <returns>Рекомендации для .editorconfig</returns>
    Task<EditorConfigRecommendations> GenerateAsync(
        SemanticEnrichmentResult enrichmentResult,
        EditorConfigOptions options,
        CancellationToken cancellationToken = default
    );
}
