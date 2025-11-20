using BHMHockey.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        AppDbContext context,
        IConfiguration configuration,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendPushNotificationAsync(string pushToken, string title, string body, object? data = null)
    {
        // TODO: Implement Expo Push Notification integration
        // This is a stub for Phase 1
        _logger.LogInformation("Sending push notification to {PushToken}: {Title}", pushToken, title);
        await Task.CompletedTask;
    }

    public async Task SendBatchPushNotificationsAsync(List<string> pushTokens, string title, string body, object? data = null)
    {
        // TODO: Implement batch Expo Push Notification integration
        _logger.LogInformation("Sending batch push notification to {Count} tokens: {Title}", pushTokens.Count, title);
        await Task.CompletedTask;
    }

    public async Task NotifyOrganizationSubscribersAsync(Guid organizationId, string title, string body, object? data = null)
    {
        var subscribers = await _context.OrganizationSubscriptions
            .Include(s => s.User)
            .Where(s => s.OrganizationId == organizationId && s.NotificationEnabled)
            .Where(s => s.User.PushToken != null)
            .Select(s => s.User.PushToken!)
            .ToListAsync();

        if (subscribers.Any())
        {
            await SendBatchPushNotificationsAsync(subscribers, title, body, data);
        }
    }
}
