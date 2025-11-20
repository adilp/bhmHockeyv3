namespace BHMHockey.Api.Services;

public interface INotificationService
{
    Task SendPushNotificationAsync(string pushToken, string title, string body, object? data = null);
    Task SendBatchPushNotificationsAsync(List<string> pushTokens, string title, string body, object? data = null);
    Task NotifyOrganizationSubscribersAsync(Guid organizationId, string title, string body, object? data = null);
}
