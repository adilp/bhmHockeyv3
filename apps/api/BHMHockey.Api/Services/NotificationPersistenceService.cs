using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;

namespace BHMHockey.Api.Services;

public class NotificationPersistenceService : INotificationPersistenceService
{
    private readonly AppDbContext _context;
    private readonly ILogger<NotificationPersistenceService> _logger;

    public NotificationPersistenceService(AppDbContext context, ILogger<NotificationPersistenceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Notification> CreateAsync(
        Guid userId,
        string type,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        Guid? organizationId = null,
        Guid? eventId = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            Data = data != null ? JsonSerializer.Serialize(data) : null,
            OrganizationId = organizationId,
            EventId = eventId
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Created notification {NotificationId} of type {Type} for user {UserId}",
            notification.Id, type, userId);

        return notification;
    }

    public async Task CreateBatchAsync(
        IEnumerable<Guid> userIds,
        string type,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        Guid? organizationId = null,
        Guid? eventId = null)
    {
        var userIdList = userIds.ToList();
        if (userIdList.Count == 0) return;

        var dataJson = data != null ? JsonSerializer.Serialize(data) : null;

        var notifications = userIdList.Select(userId => new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Body = body,
            Data = dataJson,
            OrganizationId = organizationId,
            EventId = eventId
        }).ToList();

        _context.Notifications.AddRange(notifications);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Created {Count} notifications of type {Type} for organization {OrganizationId}",
            notifications.Count, type, organizationId);
    }

    public async Task<NotificationListResponse> GetUserNotificationsAsync(
        Guid userId,
        int offset = 0,
        int limit = 20,
        bool unreadOnly = false)
    {
        // Clamp limit to prevent abuse
        limit = Math.Min(limit, 50);

        var query = _context.Notifications
            .Where(n => n.UserId == userId);

        if (unreadOnly)
            query = query.Where(n => n.ReadAt == null);

        var totalCount = await query.CountAsync();

        var unreadCount = await _context.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .CountAsync();

        // Fetch from database first, then map in memory (can't use JsonSerializer in EF expressions)
        var rawNotifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        var notifications = rawNotifications.Select(n => new NotificationDto(
            n.Id,
            n.Type,
            n.Title,
            n.Body,
            n.Data != null ? JsonSerializer.Deserialize<Dictionary<string, string>>(n.Data) : null,
            n.OrganizationId,
            n.EventId,
            n.ReadAt.HasValue,
            n.ReadAt,
            n.CreatedAt
        )).ToList();

        return new NotificationListResponse(
            notifications,
            unreadCount,
            totalCount,
            offset + limit < totalCount
        );
    }

    public async Task<int> GetUnreadCountAsync(Guid userId)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .CountAsync();
    }

    public async Task MarkAsReadAsync(Guid notificationId, Guid userId)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification == null)
            throw new InvalidOperationException("Notification not found");

        if (notification.ReadAt == null)
        {
            notification.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogDebug(
                "Marked notification {NotificationId} as read for user {UserId}",
                notificationId, userId);
        }
    }

    public async Task MarkAllAsReadAsync(Guid userId)
    {
        var count = await _context.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(n => n.SetProperty(x => x.ReadAt, DateTime.UtcNow));

        _logger.LogInformation(
            "Marked {Count} notifications as read for user {UserId}",
            count, userId);
    }

    public async Task DeleteAsync(Guid notificationId, Guid userId)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification == null)
            throw new InvalidOperationException("Notification not found");

        _context.Notifications.Remove(notification);
        await _context.SaveChangesAsync();

        _logger.LogDebug(
            "Deleted notification {NotificationId} for user {UserId}",
            notificationId, userId);
    }

    public async Task<int> DeleteOldNotificationsAsync(int olderThanDays = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);

        var count = await _context.Notifications
            .Where(n => n.CreatedAt < cutoffDate)
            .ExecuteDeleteAsync();

        if (count > 0)
        {
            _logger.LogInformation(
                "Deleted {Count} notifications older than {Days} days",
                count, olderThanDays);
        }

        return count;
    }
}
