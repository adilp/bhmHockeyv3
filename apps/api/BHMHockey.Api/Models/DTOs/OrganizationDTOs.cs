namespace BHMHockey.Api.Models.DTOs;

public record OrganizationDto(
    Guid Id,
    string Name,
    string? Description,
    string? Location,
    List<string>? SkillLevels,  // Gold, Silver, Bronze, D-League (multiple allowed)
    Guid CreatorId,
    int SubscriberCount,
    bool IsSubscribed,
    bool IsAdmin,      // True if current user is an admin of this organization
    DateTime CreatedAt,
    int? DefaultDayOfWeek,
    TimeSpan? DefaultStartTime,
    int? DefaultDurationMinutes,
    int? DefaultMaxPlayers,
    decimal? DefaultCost,
    string? DefaultVenue,
    string? DefaultVisibility
);

// Member/subscriber info - visible to all subscribers
// Email is only populated for admins viewing the list
public record OrganizationMemberDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? Email,  // Only populated for admin viewers
    Dictionary<string, string>? Positions,  // {"goalie": "Gold", "skater": "Silver"}
    DateTime SubscribedAt,
    bool IsAdmin,  // True if this member is an admin of the organization
    List<UserBadgeDto>? Badges = null,  // Top 3 badges by displayOrder
    int TotalBadgeCount = 0  // Total badges user has earned
);

public record CreateOrganizationRequest(
    string Name,
    string? Description,
    string? Location,
    List<string>? SkillLevels,  // Gold, Silver, Bronze, D-League (multiple allowed)
    int? DefaultDayOfWeek,
    TimeSpan? DefaultStartTime,
    int? DefaultDurationMinutes,
    int? DefaultMaxPlayers,
    decimal? DefaultCost,
    string? DefaultVenue,
    string? DefaultVisibility
);

public record UpdateOrganizationRequest(
    string? Name,
    string? Description,
    string? Location,
    List<string>? SkillLevels,  // Gold, Silver, Bronze, D-League (multiple allowed)
    int? DefaultDayOfWeek,
    TimeSpan? DefaultStartTime,
    int? DefaultDurationMinutes,
    int? DefaultMaxPlayers,
    decimal? DefaultCost,
    string? DefaultVenue,
    string? DefaultVisibility
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
