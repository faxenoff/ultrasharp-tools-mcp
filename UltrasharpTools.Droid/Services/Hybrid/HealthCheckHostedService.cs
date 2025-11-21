using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Droid.Services.Hybrid;

/// <summary>
/// Фоновый сервис для периодической проверки доступности Overlord
/// </summary>
public sealed class HealthCheckHostedService : BackgroundService
{
    private readonly ILogger<HealthCheckHostedService> _logger;
    private readonly IToolRouter _toolRouter;
    private readonly ConfigurationService _configService;
    private readonly string? _solutionPath;
    private TimeSpan _checkInterval;
    private bool _lastKnownStatus;

    public HealthCheckHostedService(
        ILogger<HealthCheckHostedService> _logger,
        IToolRouter toolRouter,
        ConfigurationService configService,
        string? solutionPath = null
    )
    {
        this._logger = _logger;
        _toolRouter = toolRouter;
        _configService = configService;
        _solutionPath = solutionPath;
        _lastKnownStatus = false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Загружаем конфигурацию
        var config = _configService.LoadOrCreateConfig(_solutionPath);
        _checkInterval = TimeSpan.FromSeconds(config.HealthCheckIntervalSeconds);

        _logger.LogInformation(
            "Health check service started. Interval: {Interval}s",
            config.HealthCheckIntervalSeconds
        );

        // Первая проверка сразу
        await CheckHealthAsync(stoppingToken);

        // Периодическая проверка
        using var timer = new PeriodicTimer(_checkInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                await CheckHealthAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Нормальное завершение
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in health check loop");
            }
        }

        _logger.LogInformation("Health check service stopped");
    }

    private async Task CheckHealthAsync(CancellationToken cancellationToken)
    {
        try
        {
            var isAvailable = await _toolRouter.IsOverlordAvailableAsync(cancellationToken);

            // Логируем только при изменении статуса
            if (isAvailable != _lastKnownStatus)
            {
                if (isAvailable)
                {
                    _logger.LogInformation("Overlord is now AVAILABLE");
                }
                else
                {
                    _logger.LogWarning(
                        "Overlord is now UNAVAILABLE - routing will fallback to LOCAL"
                    );
                }

                _lastKnownStatus = isAvailable;
            }
            else
            {
                _logger.LogTrace(
                    "Overlord status: {Status}",
                    isAvailable ? "AVAILABLE" : "UNAVAILABLE"
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");

            if (_lastKnownStatus)
            {
                _logger.LogWarning("Overlord status changed to UNAVAILABLE due to error");
                _lastKnownStatus = false;
            }
        }
    }
}
