using BHMHockey.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace BHMHockey.Api.Services;

public class NotificationService : INotificationService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<NotificationService> _logger;
    private readonly HttpClient _httpClient;
    private readonly INotificationPersistenceService _persistenceService;
    private const string ExpoPushUrl = "https://exp.host/--/api/v2/push/send";

    public NotificationService(
        AppDbContext context,
        IConfiguration configuration,
        ILogger<NotificationService> logger,
        IHttpClientFactory httpClientFactory,
        INotificationPersistenceService persistenceService)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("ExpoPush");
        _persistenceService = persistenceService;
    }

    public async Task SendPushNotificationAsync(
        string pushToken,
        string title,
        string body,
        object? data = null,
        Guid? userId = null,
        string? type = null,
        Guid? organizationId = null,
        Guid? eventId = null)
    {
        // Send push notification
        await SendBatchPushNotificationsAsync(new List<string> { pushToken }, title, body, data);

        // Persist to database if userId is provided
        if (userId.HasValue && !string.IsNullOrEmpty(type))
        {
            try
            {
                var dataDict = ConvertToDataDictionary(data);
                await _persistenceService.CreateAsync(
                    userId.Value,
                    type,
                    title,
                    body,
                    dataDict,
                    organizationId,
                    eventId);
            }
            catch (Exception ex)
            {
                // Don't fail the push notification if persistence fails
                _logger.LogError(ex, "Failed to persist notification for user {UserId}", userId);
            }
        }
    }

    private static Dictionary<string, string>? ConvertToDataDictionary(object? data)
    {
        if (data == null) return null;

        try
        {
            var json = JsonSerializer.Serialize(data);
            var dict = JsonSerializer.Deserialize<Dictionary<string, object>>(json);
            return dict?.ToDictionary(k => k.Key, v => v.Value?.ToString() ?? string.Empty);
        }
        catch
        {
            return null;
        }
    }

    public async Task SendBatchPushNotificationsAsync(List<string> pushTokens, string title, string body, object? data = null)
    {
        if (!pushTokens.Any())
        {
            _logger.LogInformation("No push tokens to send notifications to");
            return;
        }

        // Filter to only valid Expo push tokens
        var validTokens = pushTokens.Where(t => t.StartsWith("ExponentPushToken[")).ToList();
        if (!validTokens.Any())
        {
            _logger.LogWarning("No valid Expo push tokens found in batch of {Count}", pushTokens.Count);
            return;
        }

        _logger.LogInformation("Sending push notification to {Count} devices: {Title}", validTokens.Count, title);

        // Build notification messages for each token
        var messages = validTokens.Select(token => new
        {
            to = token,
            title = title,
            body = body,
            data = data,
            sound = "default",
            priority = "high"
        }).ToList();

        try
        {
            var json = JsonSerializer.Serialize(messages);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Add Expo access token if configured
            var expoAccessToken = _configuration["Expo:AccessToken"];
            if (!string.IsNullOrEmpty(expoAccessToken))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", expoAccessToken);
            }

            var response = await _httpClient.PostAsync(ExpoPushUrl, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully sent {Count} push notifications", validTokens.Count);
            }
            else
            {
                _logger.LogError("Failed to send push notifications. Status: {Status}, Response: {Response}",
                    response.StatusCode, responseBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending push notifications to Expo API");
        }
    }

    public async Task NotifyOrganizationSubscribersAsync(
        Guid organizationId,
        string title,
        string body,
        object? data = null,
        string? type = null,
        Guid? eventId = null)
    {
        // Get all subscribers with notifications enabled
        var subscribers = await _context.OrganizationSubscriptions
            .Include(s => s.User)
            .Where(s => s.OrganizationId == organizationId && s.NotificationEnabled)
            .Select(s => new { s.UserId, s.User.PushToken })
            .ToListAsync();

        var pushTokens = subscribers
            .Where(s => !string.IsNullOrEmpty(s.PushToken))
            .Select(s => s.PushToken!)
            .ToList();

        _logger.LogInformation("Found {Count} subscribers ({PushCount} with push tokens) for organization {OrgId}",
            subscribers.Count, pushTokens.Count, organizationId);

        // Send push notifications to those with tokens
        if (pushTokens.Any())
        {
            await SendBatchPushNotificationsAsync(pushTokens, title, body, data);
        }

        // Persist notifications for ALL subscribers (even those without push tokens)
        if (!string.IsNullOrEmpty(type) && subscribers.Any())
        {
            try
            {
                var dataDict = ConvertToDataDictionary(data);
                var userIds = subscribers.Select(s => s.UserId);
                await _persistenceService.CreateBatchAsync(
                    userIds,
                    type,
                    title,
                    body,
                    dataDict,
                    organizationId,
                    eventId);
            }
            catch (Exception ex)
            {
                // Don't fail if persistence fails
                _logger.LogError(ex, "Failed to persist notifications for organization {OrgId}", organizationId);
            }
        }
    }
}
