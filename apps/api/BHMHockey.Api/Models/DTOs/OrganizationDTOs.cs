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
    bool IsCreator,      // True if current user created this organization
    DateTime CreatedAt
);

// Member/subscriber info for admin view
public record OrganizationMemberDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? SkillLevel,
    string? Position,
    DateTime SubscribedAt
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
