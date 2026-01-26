namespace BHMHockey.Api.Services.Background;

/// <summary>
/// Background service that handles roster auto-publish and organizer reminders:
/// - Auto-publish 2 hours before event if not manually published
/// - Organizer reminders at 24h, 8h, 5h before event if not published
/// Runs every 15 minutes.
/// </summary>
public class RosterPublishBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RosterPublishBackgroundService> _logger;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);

    public RosterPublishBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<RosterPublishBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Roster publish background service started. Checking every {Interval} minutes.",
            CheckInterval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in roster publish background service");
            }

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Roster publish background service stopped");
    }

    private async Task ProcessAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var publishService = scope.ServiceProvider.GetRequiredService<IRosterPublishService>();

        _logger.LogDebug("Checking for roster publish tasks");

        // Send organizer reminders first (24h, 8h, 5h)
        await publishService.SendOrganizerPublishRemindersAsync();

        // Then auto-publish events within 2 hours
        await publishService.ProcessAutoPublishAsync();
    }
}
