using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

public class BadgeService : IBadgeService
{
    private readonly AppDbContext _context;

    public BadgeService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<UserBadgeDto>> GetUserBadgesAsync(Guid userId)
    {
        var userBadges = await _context.UserBadges
            .Include(ub => ub.BadgeType)
            .Where(ub => ub.UserId == userId)
            .OrderBy(ub => ub.DisplayOrder ?? int.MaxValue)
            .ThenBy(ub => ub.BadgeType.SortPriority)
            .ToListAsync();

        return userBadges.Select(MapToDto).ToList();
    }

    public async Task<List<UserBadgeDto>> GetUserTopBadgesAsync(Guid userId, int count = 3)
    {
        var userBadges = await _context.UserBadges
            .Include(ub => ub.BadgeType)
            .Where(ub => ub.UserId == userId)
            .OrderBy(ub => ub.DisplayOrder ?? int.MaxValue)
            .ThenBy(ub => ub.BadgeType.SortPriority)
            .Take(count)
            .ToListAsync();

        return userBadges.Select(MapToDto).ToList();
    }

    public async Task UpdateBadgeOrderAsync(Guid userId, List<Guid> badgeIds)
    {
        // Get all badge IDs owned by the user
        var userBadges = await _context.UserBadges
            .Where(ub => ub.UserId == userId)
            .ToListAsync();

        var userBadgeIds = userBadges.Select(ub => ub.Id).ToHashSet();

        // Validation: Check for duplicates
        if (badgeIds.Distinct().Count() != badgeIds.Count)
        {
            throw new InvalidOperationException("Duplicate badge IDs provided");
        }

        // Validation: Must include all badges
        if (badgeIds.Count != userBadgeIds.Count)
        {
            throw new InvalidOperationException("Must include all badges owned by user");
        }

        // Validation: All provided IDs must belong to the user
        var providedIds = badgeIds.ToHashSet();
        if (!providedIds.SetEquals(userBadgeIds))
        {
            throw new InvalidOperationException("Badge IDs do not match user's badges");
        }

        // Update display order based on array index
        for (int i = 0; i < badgeIds.Count; i++)
        {
            var badge = userBadges.First(ub => ub.Id == badgeIds[i]);
            badge.DisplayOrder = i;
        }

        await _context.SaveChangesAsync();
    }

    private static UserBadgeDto MapToDto(UserBadge userBadge)
    {
        return new UserBadgeDto(
            Id: userBadge.Id,
            BadgeType: new BadgeTypeDto(
                Id: userBadge.BadgeType.Id,
                Code: userBadge.BadgeType.Code,
                Name: userBadge.BadgeType.Name,
                Description: userBadge.BadgeType.Description,
                IconName: userBadge.BadgeType.IconName,
                Category: userBadge.BadgeType.Category
            ),
            Context: userBadge.Context,
            EarnedAt: userBadge.EarnedAt,
            DisplayOrder: userBadge.DisplayOrder
        );
    }
}
