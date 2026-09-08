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

    /// <summary>
    /// Every badge type in the system, for an admin choosing one to award.
    /// </summary>
    Task<List<BadgeTypeDto>> GetBadgeTypesAsync(Guid organizationId, Guid requesterId);

    /// <summary>
    /// Create a badge type (organization admin only). Types are global - they
    /// are not owned by the creating organization - so the code must be unique
    /// system-wide.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">Requester is not an admin of the organization</exception>
    /// <exception cref="InvalidOperationException">Code already exists, or a field is missing</exception>
    Task<BadgeTypeDto> CreateBadgeTypeAsync(Guid organizationId, CreateBadgeTypeRequest request, Guid requesterId);

    /// <summary>
    /// Award one badge type to several users (organization admin only).
    ///
    /// Targets must be tied to the organization - a member, or someone who has
    /// played in one of its events - which keeps an admin's reach to the people
    /// they actually run games for. Partial by design: an ineligible id is
    /// reported, not fatal.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">Requester is not an admin of the organization</exception>
    /// <exception cref="InvalidOperationException">Unknown badge code, or no users supplied</exception>
    Task<AwardBadgeResponse> AwardBadgeAsync(Guid organizationId, AwardBadgeRequest request, Guid requesterId);

    /// <summary>
    /// Remove an awarded badge (organization admin only), for fixing a mistake.
    /// The holder must be tied to the organization, on the same rule as awarding.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">Requester is not an admin, or the holder isn't tied to the org</exception>
    /// <returns>False when the badge doesn't exist</returns>
    Task<bool> RevokeBadgeAsync(Guid organizationId, Guid userBadgeId, Guid requesterId);
}
