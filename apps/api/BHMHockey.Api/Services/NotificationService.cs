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
    private const string ExpoPushUrl = "https://exp.host/--/api/v2/push/send";

    public NotificationService(
        AppDbContext context,
        IConfiguration configuration,
        ILogger<NotificationService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("ExpoPush");
    }

    public async Task SendPushNotificationAsync(string pushToken, string title, string body, object? data = null)
    {
        await SendBatchPushNotificationsAsync(new List<string> { pushToken }, title, body, data);
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

    public async Task NotifyOrganizationSubscribersAsync(Guid organizationId, string title, string body, object? data = null)
    {
        var subscribers = await _context.OrganizationSubscriptions
            .Include(s => s.User)
            .Where(s => s.OrganizationId == organizationId && s.NotificationEnabled)
            .Where(s => s.User.PushToken != null)
            .Select(s => s.User.PushToken!)
            .ToListAsync();

        _logger.LogInformation("Found {Count} subscribers with push tokens for organization {OrgId}",
            subscribers.Count, organizationId);

        if (subscribers.Any())
        {
            await SendBatchPushNotificationsAsync(subscribers, title, body, data);
        }
    }
}
