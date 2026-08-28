using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

/// <summary>
/// Shared "rarest badges first" ranking for the compact rows that only have
/// room for three badges - the event roster row and the org member row. Both
/// lead with the badges the fewest people hold, so a rare badge makes someone
/// stand out instead of every row repeating the same common one. (The trophy
/// case is unaffected: it shows everything in each owner's own DisplayOrder.)
/// </summary>
public static class BadgeRanking
{
    /// <summary>
    /// For each requested user, their <paramref name="take"/> rarest badges
    /// (fewest global holders) plus their total badge count. Ties break by
    /// SortPriority, then most-recently earned, so the order is deterministic.
    /// Every requested user is present in the result; one with no badges gets
    /// an empty list and a count of 0. Runs two queries total regardless of how
    /// many users are passed (no N+1).
    /// </summary>
    public static async Task<Dictionary<Guid, (List<UserBadgeDto> TopBadges, int TotalCount)>>
        TopRarestByUserAsync(AppDbContext context, IEnumerable<Guid> userIds, int take = 3)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, (List<UserBadgeDto>, int)>();
        }

        // One query for every badge these users hold...
        var allBadges = await context.UserBadges
            .Include(ub => ub.BadgeType)
            .Where(ub => ids.Contains(ub.UserId))
            .ToListAsync();

        // ...and one to count, globally, how many people hold each of those
        // badge types - that count is what "rarest" is measured against.
        var badgeTypeIds = allBadges.Select(ub => ub.BadgeTypeId).Distinct().ToList();
        var awardCounts = badgeTypeIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await context.UserBadges
                .Where(ub => badgeTypeIds.Contains(ub.BadgeTypeId))
                .GroupBy(ub => ub.BadgeTypeId)
                .Select(g => new { BadgeTypeId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.BadgeTypeId, x => x.Count);

        var badgesByUser = allBadges
            .GroupBy(ub => ub.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = new Dictionary<Guid, (List<UserBadgeDto> TopBadges, int TotalCount)>();
        foreach (var userId in ids)
        {
            var userBadges = badgesByUser.GetValueOrDefault(userId) ?? new List<UserBadge>();

            var topBadges = userBadges
                // Rarest first; SortPriority then earned-date keep it deterministic
                .OrderBy(ub => awardCounts.GetValueOrDefault(ub.BadgeTypeId, int.MaxValue))
                .ThenBy(ub => ub.BadgeType.SortPriority)
                .ThenByDescending(ub => ub.EarnedAt)
                .Take(take)
                .Select(ub => new UserBadgeDto(
                    ub.Id,
                    new BadgeTypeDto(
                        ub.BadgeType.Id,
                        ub.BadgeType.Code,
                        ub.BadgeType.Name,
                        ub.BadgeType.Description,
                        ub.BadgeType.IconName,
                        ub.BadgeType.Category
                    ),
                    ub.Context,
                    ub.EarnedAt,
                    ub.DisplayOrder
                ))
                .ToList();

            result[userId] = (topBadges, userBadges.Count);
        }

        return result;
    }
}
