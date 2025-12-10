namespace BHMHockey.Api.Services.Background;

/// <summary>
/// Background service that periodically checks for and sends event reminders:
/// - Player reminders 1 hour before game (game time, venue, team)
/// - Organizer payment reminders 5 hours before game (if unpaid players exist)
/// Runs every 15 minutes.
/// </summary>
public class EventReminderBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EventReminderBackgroundService> _logger;

    // Run every 15 minutes
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);

    public EventReminderBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<EventReminderBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Event reminder background service started. Checking every {Interval} minutes.",
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
                _logger.LogError(ex, "Error processing event reminders");
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

        _logger.LogInformation("Event reminder background service stopped");
    }

    private async Task ProcessRemindersAsync()
    {
        // Create a scope because BackgroundService is singleton
        // but EventReminderService is scoped (requires DbContext)
        using var scope = _serviceProvider.CreateScope();
        var reminderService = scope.ServiceProvider.GetRequiredService<IEventReminderService>();

        _logger.LogDebug("Checking for event reminders to send");

        // Send player reminders (1 hour before)
        await reminderService.SendPlayerRemindersAsync();

        // Send organizer payment reminders (5 hours before)
        await reminderService.SendOrganizerPaymentRemindersAsync();
    }
}
