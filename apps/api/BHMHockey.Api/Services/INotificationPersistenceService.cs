using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;

namespace BHMHockey.Api.Services;

public interface INotificationPersistenceService
{
    /// <summary>
    /// Create a notification for a single user
    /// </summary>
    Task<Notification> CreateAsync(
        Guid userId,
        string type,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        Guid? organizationId = null,
        Guid? eventId = null);

    /// <summary>
    /// Create notifications for multiple users (batch)
    /// </summary>
    Task CreateBatchAsync(
        IEnumerable<Guid> userIds,
        string type,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        Guid? organizationId = null,
        Guid? eventId = null);

    /// <summary>
    /// Get user's notifications with pagination
    /// </summary>
    Task<NotificationListResponse> GetUserNotificationsAsync(
        Guid userId,
        int offset = 0,
        int limit = 20,
        bool unreadOnly = false);

    /// <summary>
    /// Get unread notification count for a user
    /// </summary>
    Task<int> GetUnreadCountAsync(Guid userId);

    /// <summary>
    /// Mark a single notification as read
    /// </summary>
    Task MarkAsReadAsync(Guid notificationId, Guid userId);

    /// <summary>
    /// Mark all notifications as read for a user
    /// </summary>
    Task MarkAllAsReadAsync(Guid userId);

    /// <summary>
    /// Delete a notification
    /// </summary>
    Task DeleteAsync(Guid notificationId, Guid userId);

    /// <summary>
    /// Delete notifications older than specified days (for cleanup job)
    /// </summary>
    Task<int> DeleteOldNotificationsAsync(int olderThanDays = 30);
}
