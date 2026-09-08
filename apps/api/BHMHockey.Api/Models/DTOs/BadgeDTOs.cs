namespace BHMHockey.Api.Models.DTOs;

/// <summary>
/// DTO for badge type information
/// </summary>
public record BadgeTypeDto(
    Guid Id,
    string Code,
    string Name,
    string Description,
    string IconName,
    string Category
);

/// <summary>
/// DTO for a user's earned badge with context
/// </summary>
public record UserBadgeDto(
    Guid Id,
    BadgeTypeDto BadgeType,
    Dictionary<string, object>? Context,
    DateTime EarnedAt,
    int? DisplayOrder
);

/// <summary>
/// Request to update badge display order
/// </summary>
public record UpdateBadgeOrderRequest(
    List<Guid> BadgeIds
);

/// <summary>
/// DTO for an uncelebrated badge with rarity information
/// </summary>
public record UncelebratedBadgeDto(
    Guid Id,
    BadgeTypeDto BadgeType,
    Dictionary<string, object>? Context,
    DateTime EarnedAt,
    int TotalAwarded
);

/// <summary>
/// Create a badge type (organization admin). The icon must already exist in
/// the mobile app's icon map - a type whose IconName the app doesn't know
/// renders as a blank space in every trophy case, so new artwork ships in a
/// release BEFORE the type is created.
/// </summary>
public record CreateBadgeTypeRequest(
    string Code,
    string Name,
    string Description,
    string IconName,
    string Category = "achievement",
    int SortPriority = 0
);

/// <summary>
/// Award one badge type to a set of users in one call. Awarding is idempotent:
/// a user who already holds the badge is reported as such, not duplicated.
/// </summary>
public record AwardBadgeRequest(
    string BadgeCode,
    List<Guid> UserIds,
    /// Free text shown under the badge in the celebration and detail views,
    /// e.g. "AMP Ironman Game - July 25, 2026". Optional.
    string? Occasion = null
);

/// <summary>
/// What happened for one user in an award request. Bulk awards are partial by
/// design - one ineligible id shouldn't discard the other twenty.
/// </summary>
public record AwardBadgeOutcomeDto(
    Guid UserId,
    string Outcome,      // "awarded" | "already_held" | "not_eligible" | "unknown_user"
    Guid? UserBadgeId
);

public record AwardBadgeResponse(
    string BadgeCode,
    int Awarded,
    int AlreadyHeld,
    int Skipped,
    List<AwardBadgeOutcomeDto> Results
);
