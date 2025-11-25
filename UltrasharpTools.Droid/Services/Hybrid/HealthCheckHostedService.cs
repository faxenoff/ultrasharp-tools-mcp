using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UltrasharpTools.Droid.Services.Hybrid;

/// <summary>
/// Фоновый сервис для периодической проверки доступности Overlord
/// </summary>
public sealed partial class HealthCheckHostedService : BackgroundService
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

        LogServiceStarted(config.HealthCheckIntervalSeconds);

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
                LogHealthCheckLoopError(ex);
            }
        }

        LogServiceStopped();
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
                    LogOverlordAvailable();
                }
                else
                {
                    LogOverlordUnavailable();
                }

                _lastKnownStatus = isAvailable;
            }
            else
            {
                LogOverlordStatus(isAvailable ? "AVAILABLE" : "UNAVAILABLE");
            }
        }
        catch (Exception ex)
        {
            LogHealthCheckFailed(ex);

            if (_lastKnownStatus)
            {
                LogOverlordUnavailableDueToError();
                _lastKnownStatus = false;
            }
        }
    }
}
