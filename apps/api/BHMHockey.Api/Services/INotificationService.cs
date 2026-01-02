namespace BHMHockey.Api.Services;

public interface INotificationService
{
    /// <summary>
    /// Send push notification to a single user and optionally persist to database
    /// </summary>
    Task SendPushNotificationAsync(
        string pushToken,
        string title,
        string body,
        object? data = null,
        Guid? userId = null,
        string? type = null,
        Guid? organizationId = null,
        Guid? eventId = null);

    /// <summary>
    /// Send push notifications to multiple users (no persistence - use NotifyOrganizationSubscribersAsync for that)
    /// </summary>
    Task SendBatchPushNotificationsAsync(List<string> pushTokens, string title, string body, object? data = null);

    /// <summary>
    /// Send push notifications to all organization subscribers AND persist notifications
    /// </summary>
    Task NotifyOrganizationSubscribersAsync(
        Guid organizationId,
        string title,
        string body,
        object? data = null,
        string? type = null,
        Guid? eventId = null);
}
