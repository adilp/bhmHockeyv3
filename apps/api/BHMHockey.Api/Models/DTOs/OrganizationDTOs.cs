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
    string? DefaultVisibility,
    string? GroupMeLink = null,  // Org-wide GroupMe chat link (events fall back to this)
    bool? DefaultShowWaitlistBeforePublish = null,  // Pre-fills ShowWaitlistBeforePublish on new events
    bool IsPrivate = false,  // Private orgs require admin approval to join
    string? MyJoinRequestStatus = null,  // Current user's join request: null | "Pending" | "Approved" | "Denied"
    int? PendingJoinRequestCount = null  // ADMIN-ONLY - null for everyone else
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
    int TotalBadgeCount = 0,  // Total badges user has earned
    bool? HasAcceptedCurrentWaiver = null  // null when the org has no active waiver
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
    string? DefaultVisibility,
    string? GroupMeLink = null,  // Org-wide GroupMe chat link
    bool? DefaultShowWaitlistBeforePublish = null,
    bool? IsPrivate = null  // null defaults to public
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
    string? DefaultVisibility,
    string? GroupMeLink = null,  // Empty/whitespace clears the link; null leaves it unchanged
    bool? DefaultShowWaitlistBeforePublish = null,  // null leaves unchanged
    bool? IsPrivate = null  // null leaves unchanged
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

// Auto-roster DTOs - org "regulars" auto-added to new org events
public record AutoRosterMemberDto(
    Guid Id,
    Guid UserId,
    string FirstName,
    string LastName,
    Dictionary<string, string>? Positions,  // User's profile positions {"goalie": "Gold", "skater": "Silver"}
    string Position,                        // "Goalie" or "Skater" - position they'll be auto-added as
    int SortOrder,
    DateTime AddedAt
);

public record AddAutoRosterMemberRequest(
    Guid UserId,
    string Position                         // "Goalie" or "Skater"
);

public record ReorderAutoRosterRequest(
    List<Guid> OrderedUserIds               // All auto-roster member user IDs in the new order
);

// Join request DTOs - private orgs require an admin to approve membership

/// <summary>
/// Outcome of a subscribe attempt. Public orgs subscribe instantly; private orgs
/// produce a join request an admin must approve.
/// </summary>
public enum SubscribeOutcome
{
    Subscribed,
    AlreadySubscribed,
    JoinRequestCreated,
    JoinRequestAlreadyPending
}

// Body of POST /organizations/{id}/subscribe. Status is "Subscribed" or "JoinRequestPending".
public record SubscribeResponse(
    string Status,
    string Message
);

public record OrganizationJoinRequestDto(
    Guid Id,
    Guid OrganizationId,
    Guid UserId,
    string FirstName,
    string LastName,
    string Status,  // "Pending" | "Approved" | "Denied"
    DateTime RequestedAt,
    DateTime? DecidedAt
);
