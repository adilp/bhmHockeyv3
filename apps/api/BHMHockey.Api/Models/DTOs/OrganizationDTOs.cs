namespace BHMHockey.Api.Models.DTOs;

public record OrganizationDto(
    Guid Id,
    string Name,
    string? Description,
    string? Location,
    string? SkillLevel,
    Guid CreatorId,
    int SubscriberCount,
    bool IsSubscribed,
    DateTime CreatedAt
);

public record CreateOrganizationRequest(
    string Name,
    string? Description,
    string? Location,
    string? SkillLevel
);

public record UpdateOrganizationRequest(
    string? Name,
    string? Description,
    string? Location,
    string? SkillLevel
);

public record OrganizationSubscriptionDto(
    Guid Id,
    OrganizationDto Organization,
    bool NotificationEnabled,
    DateTime SubscribedAt
);
