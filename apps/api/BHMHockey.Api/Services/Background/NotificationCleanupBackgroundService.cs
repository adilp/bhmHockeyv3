namespace BHMHockey.Api.Services.Background;

/// <summary>
/// Background service that periodically deletes old notifications
/// to prevent database bloat. Runs once per day and deletes
/// notifications older than 30 days.
/// </summary>
public class NotificationCleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotificationCleanupBackgroundService> _logger;

    // Run once per day
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);

    // Delete notifications older than 30 days
    private const int RetentionDays = 30;

    public NotificationCleanupBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<NotificationCleanupBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Notification cleanup service started. Running every {Interval} hours, deleting notifications older than {Days} days.",
            CheckInterval.TotalHours, RetentionDays);

        // Wait a bit before first run to let app fully start
        await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupOldNotificationsAsync();
            }
            catch (Exception ex)
            {
                // Log error but don't rethrow - keep the service running
                _logger.LogError(ex, "Error cleaning up old notifications");
            }

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Expected when stopping - don't log as error
                break;
            }
        }

        _logger.LogInformation("Notification cleanup service stopped");
    }

    private async Task CleanupOldNotificationsAsync()
    {
        // Create a scope because BackgroundService is singleton
        // but NotificationPersistenceService is scoped (requires DbContext)
        using var scope = _serviceProvider.CreateScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationPersistenceService>();

        _logger.LogDebug("Running notification cleanup for notifications older than {Days} days", RetentionDays);
        var deletedCount = await notificationService.DeleteOldNotificationsAsync(RetentionDays);

        if (deletedCount > 0)
        {
            _logger.LogInformation("Deleted {Count} old notifications", deletedCount);
        }
    }
}
