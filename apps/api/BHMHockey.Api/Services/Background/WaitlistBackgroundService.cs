namespace BHMHockey.Api.Services.Background;

/// <summary>
/// Background service that periodically checks for and processes
/// expired payment deadlines for promoted waitlist users.
/// Runs every 15 minutes to enforce the 2-hour payment window.
/// </summary>
public class WaitlistBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WaitlistBackgroundService> _logger;

    // Run every 15 minutes (per project philosophy - "good enough")
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);

    public WaitlistBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<WaitlistBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Waitlist background service started. Checking every {Interval} minutes.",
            CheckInterval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Payment deadlines no longer enforced - organizer manages manually
            // The ProcessExpiredDeadlinesAsync method is preserved for potential future use
            // try
            // {
            //     await ProcessExpiredDeadlinesAsync();
            // }
            // catch (Exception ex)
            // {
            //     // Log error but don't rethrow - keep the service running
            //     _logger.LogError(ex, "Error processing expired payment deadlines");
            // }

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

        _logger.LogInformation("Waitlist background service stopped");
    }

    private async Task ProcessExpiredDeadlinesAsync()
    {
        // Create a scope because BackgroundService is singleton
        // but WaitlistService is scoped (requires DbContext)
        using var scope = _serviceProvider.CreateScope();
        var waitlistService = scope.ServiceProvider.GetRequiredService<IWaitlistService>();

        _logger.LogDebug("Checking for expired payment deadlines");
        await waitlistService.ProcessExpiredPaymentDeadlinesAsync();
    }
}
