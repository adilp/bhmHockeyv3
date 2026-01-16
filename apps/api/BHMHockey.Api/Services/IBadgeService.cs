using BHMHockey.Api.Models.DTOs;

namespace BHMHockey.Api.Services;

public interface IBadgeService
{
    /// <summary>
    /// Gets all badges for a user, sorted by displayOrder (then sortPriority fallback)
    /// </summary>
    Task<List<UserBadgeDto>> GetUserBadgesAsync(Guid userId);

    /// <summary>
    /// Gets top N badges for a user, for inclusion in user summaries
    /// </summary>
    Task<List<UserBadgeDto>> GetUserTopBadgesAsync(Guid userId, int count = 3);

    /// <summary>
    /// Updates the display order of a user's badges
    /// </summary>
    /// <param name="userId">The user whose badges to reorder</param>
    /// <param name="badgeIds">Ordered list of badge IDs (position = displayOrder)</param>
    /// <exception cref="InvalidOperationException">Thrown if validation fails</exception>
    Task UpdateBadgeOrderAsync(Guid userId, List<Guid> badgeIds);

    /// <summary>
    /// Gets all uncelebrated badges for a user (CelebratedAt is null) with rarity counts
    /// </summary>
    /// <param name="userId">The user whose uncelebrated badges to retrieve</param>
    /// <returns>List of uncelebrated badges sorted by EarnedAt ascending</returns>
    Task<List<UncelebratedBadgeDto>> GetUncelebratedBadgesAsync(Guid userId);

    /// <summary>
    /// Marks a badge as celebrated by setting CelebratedAt to the current time
    /// </summary>
    /// <param name="userId">The user who owns the badge</param>
    /// <param name="userBadgeId">The badge to mark as celebrated</param>
    /// <exception cref="InvalidOperationException">Thrown if badge not found or doesn't belong to user</exception>
    Task CelebrateBadgeAsync(Guid userId, Guid userBadgeId);
}
