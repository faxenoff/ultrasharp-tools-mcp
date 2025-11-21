using Microsoft.Extensions.Logging;
using UltrasharpTools.Overlord.Models.Notifications;

namespace UltrasharpTools.Overlord.Services;

/// <summary>
/// Сервис для автоматического обнаружения дубликатов и конфликтов кода
/// </summary>
public sealed class ConflictDetectionService : IConflictDetectionService
{
    private readonly ILogger<ConflictDetectionService> _logger;
    private readonly IMultiProjectVectorStoreService _vectorStore;
    private readonly INotificationService _notificationService;

    public bool AutoNotifyEnabled { get; set; } = true;
    public double AutoNotifyThreshold { get; set; } = 0.85;

    public ConflictDetectionService(
        ILogger<ConflictDetectionService> logger,
        IMultiProjectVectorStoreService vectorStore,
        INotificationService notificationService
    )
    {
        _logger = logger;
        _vectorStore = vectorStore;
        _notificationService = notificationService;
    }

    public async Task<List<DuplicateMatch>> DetectDuplicatesAsync(
        string project,
        string branch,
        string file,
        float[] vectors,
        string? content,
        double duplicateThreshold = 0.85,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug(
            "Detecting duplicates for {Project}/{Branch}/{File}",
            project,
            branch,
            file
        );

        try
        {
            // Поиск похожего кода во всех проектах (кроме текущего файла)
            var matches = await _vectorStore.SearchAcrossProjectsAsync(
                queryVector: vectors,
                threshold: duplicateThreshold,
                limit: 10,
                projects: null, // Поиск во всех проектах
                cancellationToken: cancellationToken
            );

            var duplicates = new List<DuplicateMatch>();

            foreach (var match in matches)
            {
                // Пропускаем сам файл
                if (match.Project == project && match.Branch == branch && match.FilePath == file)
                {
                    continue;
                }

                duplicates.Add(
                    new DuplicateMatch
                    {
                        Project = match.Project,
                        Branch = match.Branch,
                        File = match.FilePath,
                        Line = match.Line,
                        Similarity = match.Similarity,
                        Code = match.Code,
                    }
                );
            }

            // Автоматические уведомления о значимых дубликатах
            if (AutoNotifyEnabled && duplicates.Any())
            {
                await SendDuplicateNotificationsAsync(
                    project,
                    branch,
                    file,
                    duplicates,
                    cancellationToken
                );
            }

            _logger.LogInformation(
                "Found {Count} duplicates for {Project}/{File}",
                duplicates.Count,
                project,
                file
            );

            return duplicates;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect duplicates for {Project}/{File}", project, file);
            return new List<DuplicateMatch>();
        }
    }

    private async Task SendDuplicateNotificationsAsync(
        string project,
        string branch,
        string file,
        List<DuplicateMatch> duplicates,
        CancellationToken cancellationToken
    )
    {
        // Отправляем уведомления только о самых значимых дубликатах
        var significantDuplicates = duplicates
            .Where(d => d.Similarity >= AutoNotifyThreshold)
            .OrderByDescending(d => d.Similarity)
            .Take(3) // Максимум 3 уведомления
            .ToList();

        foreach (var duplicate in significantDuplicates)
        {
            var notification = new DuplicateDetectedNotification
            {
                Project = project,
                Message = $"Duplicate code detected in {duplicate.Project}",
                Similarity = duplicate.Similarity,
                Location = $"{duplicate.Project}/{duplicate.File}:{duplicate.Line}",
                DuplicateProject = duplicate.Project,
                DuplicateFile = duplicate.File,
                DuplicateLine = duplicate.Line,
                CodeSnippet = TruncateCode(duplicate.Code, 200),
            };

            // Отправляем уведомление проекту, в котором обнаружен дубликат
            await _notificationService.SendToProjectAsync(project, notification, cancellationToken);

            _logger.LogInformation(
                "Sent duplicate notification: {Project} -> {DuplicateProject} (similarity: {Similarity:P0})",
                project,
                duplicate.Project,
                duplicate.Similarity
            );
        }

        // Рекомендация по переиспользованию, если найден дубликат в другом проекте
        var crossProjectDuplicate = duplicates
            .Where(d => d.Project != project)
            .OrderByDescending(d => d.Similarity)
            .FirstOrDefault();

        if (crossProjectDuplicate != null && crossProjectDuplicate.Similarity >= 0.90)
        {
            var recommendation = new CodeReuseRecommendationNotification
            {
                Project = project,
                Message = $"Consider reusing existing code from {crossProjectDuplicate.Project}",
                SourceProject = crossProjectDuplicate.Project,
                SourceFile = crossProjectDuplicate.File,
                Similarity = crossProjectDuplicate.Similarity,
                Description =
                    $"Found highly similar code (similarity: {crossProjectDuplicate.Similarity:P0})",
            };

            await _notificationService.SendToProjectAsync(
                project,
                recommendation,
                cancellationToken
            );
        }
    }

    private static string? TruncateCode(string? code, int maxLength)
    {
        if (string.IsNullOrEmpty(code))
            return code;

        if (code.Length <= maxLength)
            return code;

        return code[..maxLength] + "...";
    }
}
