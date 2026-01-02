namespace BHMHockey.Api.Models.DTOs;

public record NotificationDto(
    Guid Id,
    string Type,
    string Title,
    string Body,
    Dictionary<string, string>? Data,
    Guid? OrganizationId,
    Guid? EventId,
    bool IsRead,
    DateTime? ReadAt,
    DateTime CreatedAt
);

public record NotificationListResponse(
    List<NotificationDto> Notifications,
    int UnreadCount,
    int TotalCount,
    bool HasMore
);

public record UnreadCountResponse(
    int UnreadCount
);
