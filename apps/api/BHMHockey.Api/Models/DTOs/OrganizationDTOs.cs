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
    bool IsAdmin,      // True if current user is an admin of this organization
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
    DateTime SubscribedAt,
    bool IsAdmin  // True if this member is an admin of the organization
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

// Admin management DTOs
public record OrganizationAdminDto(
    Guid Id,
    Guid UserId,
    string FirstName,
    string LastName,
    string Email,
    DateTime AddedAt,
    Guid? AddedByUserId,
    string? AddedByName  // "FirstName LastName" of who added them, null for original creator
);

public record AddAdminRequest(
    Guid UserId
);
