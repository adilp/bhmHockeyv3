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
