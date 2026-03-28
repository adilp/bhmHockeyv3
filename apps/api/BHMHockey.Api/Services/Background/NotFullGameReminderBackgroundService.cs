namespace BHMHockey.Api.Services.Background;

/// <summary>
/// Background service that sends push notifications for games that aren't full,
/// approximately 2 days before the event. Runs every 15 minutes.
/// </summary>
public class NotFullGameReminderBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<NotFullGameReminderBackgroundService> _logger;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);

    public NotFullGameReminderBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<NotFullGameReminderBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Not-full game reminder background service started. Checking every {Interval} minutes.",
            CheckInterval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessRemindersAsync();
            }
            catch (Exception ex)
            {
                // Log error but don't rethrow - keep the service running
                _logger.LogError(ex, "Error processing not-full game reminders");
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

        _logger.LogInformation("Not-full game reminder background service stopped");
    }

    private async Task ProcessRemindersAsync()
    {
        // Create a scope because BackgroundService is singleton
        // but NotFullGameReminderService is scoped (requires DbContext)
        using var scope = _serviceProvider.CreateScope();
        var reminderService = scope.ServiceProvider.GetRequiredService<INotFullGameReminderService>();

        _logger.LogDebug("Checking for not-full game reminders to send");

        await reminderService.SendNotFullRemindersAsync();
    }
}
