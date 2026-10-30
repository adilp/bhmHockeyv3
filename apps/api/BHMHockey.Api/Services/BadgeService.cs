using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

public class BadgeService : IBadgeService
{
    /// Bulk awards are a roster at a time, not a mailing list.
    private const int MaxAwardBatch = 200;

    private readonly AppDbContext _context;
    private readonly IOrganizationAdminService _adminService;
    private readonly ILogger<BadgeService> _logger;

    public BadgeService(
        AppDbContext context,
        IOrganizationAdminService adminService,
        ILogger<BadgeService> logger)
    {
        _context = context;
        _adminService = adminService;
        _logger = logger;
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

    public async Task<List<UncelebratedBadgeDto>> GetUncelebratedBadgesAsync(Guid userId)
    {
        // Get all uncelebrated badges for the user
        var uncelebrated = await _context.UserBadges
            .Include(ub => ub.BadgeType)
            .Where(ub => ub.UserId == userId && ub.CelebratedAt == null)
            .OrderBy(ub => ub.EarnedAt)
            .ToListAsync();

        if (!uncelebrated.Any())
        {
            return new List<UncelebratedBadgeDto>();
        }

        // Calculate rarity counts for all badge types in the uncelebrated list
        var badgeTypeIds = uncelebrated.Select(ub => ub.BadgeTypeId).Distinct().ToList();
        var rarityCounts = await _context.UserBadges
            .Where(ub => badgeTypeIds.Contains(ub.BadgeTypeId))
            .GroupBy(ub => ub.BadgeTypeId)
            .Select(g => new { BadgeTypeId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BadgeTypeId, x => x.Count);

        // Map to DTOs with rarity counts
        return uncelebrated.Select(ub => new UncelebratedBadgeDto(
            Id: ub.Id,
            BadgeType: new BadgeTypeDto(
                Id: ub.BadgeType.Id,
                Code: ub.BadgeType.Code,
                Name: ub.BadgeType.Name,
                Description: ub.BadgeType.Description,
                IconName: ub.BadgeType.IconName,
                Category: ub.BadgeType.Category
            ),
            Context: ub.Context,
            EarnedAt: ub.EarnedAt,
            TotalAwarded: rarityCounts.GetValueOrDefault(ub.BadgeTypeId, 0)
        )).ToList();
    }

    public async Task CelebrateBadgeAsync(Guid userId, Guid userBadgeId)
    {
        var userBadge = await _context.UserBadges
            .FirstOrDefaultAsync(ub => ub.Id == userBadgeId && ub.UserId == userId);

        if (userBadge == null)
        {
            throw new InvalidOperationException("Badge not found or does not belong to user");
        }

        userBadge.CelebratedAt = DateTime.UtcNow;
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

    // ----------------------------------------------------------------- //
    // Administration - organization admins awarding and managing badges
    // ----------------------------------------------------------------- //

    private async Task RequireAdminAsync(Guid organizationId, Guid requesterId, string action)
    {
        if (!await _adminService.IsUserAdminAsync(organizationId, requesterId))
        {
            _logger.LogWarning(
                "{Action} denied for organization {OrganizationId}: user {UserId} is not an admin",
                action, organizationId, requesterId);
            throw new UnauthorizedAccessException($"Only organization admins can {action}");
        }
    }

    public async Task<List<BadgeTypeDto>> GetBadgeTypesAsync(Guid organizationId, Guid requesterId)
    {
        await RequireAdminAsync(organizationId, requesterId, "view badge types");

        var types = await _context.BadgeTypes
            .OrderBy(bt => bt.SortPriority)
            .ThenBy(bt => bt.Name)
            .ToListAsync();

        return types.Select(bt => new BadgeTypeDto(
            bt.Id, bt.Code, bt.Name, bt.Description, bt.IconName, bt.Category)).ToList();
    }

    public async Task<BadgeTypeDto> CreateBadgeTypeAsync(
        Guid organizationId, CreateBadgeTypeRequest request, Guid requesterId)
    {
        await RequireAdminAsync(organizationId, requesterId, "create badge types");

        var code = (request.Code ?? string.Empty).Trim().ToUpperInvariant();
        var name = (request.Name ?? string.Empty).Trim();
        var description = (request.Description ?? string.Empty).Trim();
        var iconName = (request.IconName ?? string.Empty).Trim();

        if (code.Length == 0 || name.Length == 0 || iconName.Length == 0)
        {
            throw new InvalidOperationException("Code, name and icon name are required");
        }

        if (await _context.BadgeTypes.AnyAsync(bt => bt.Code == code))
        {
            throw new InvalidOperationException($"A badge type with code '{code}' already exists");
        }

        var badgeType = new BadgeType
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            Description = description,
            IconName = iconName,
            Category = string.IsNullOrWhiteSpace(request.Category) ? "achievement" : request.Category.Trim(),
            SortPriority = request.SortPriority,
            CreatedAt = DateTime.UtcNow
        };

        _context.BadgeTypes.Add(badgeType);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Badge type {Code} created by user {UserId} via organization {OrganizationId}",
            code, requesterId, organizationId);

        return new BadgeTypeDto(badgeType.Id, badgeType.Code, badgeType.Name,
            badgeType.Description, badgeType.IconName, badgeType.Category);
    }

    public async Task<AwardBadgeResponse> AwardBadgeAsync(
        Guid organizationId, AwardBadgeRequest request, Guid requesterId)
    {
        await RequireAdminAsync(organizationId, requesterId, "award badges");

        var code = (request.BadgeCode ?? string.Empty).Trim().ToUpperInvariant();
        var badgeType = await _context.BadgeTypes.FirstOrDefaultAsync(bt => bt.Code == code)
            ?? throw new InvalidOperationException($"No badge type with code '{code}'");

        var userIds = (request.UserIds ?? new List<Guid>()).Distinct().ToList();
        if (userIds.Count == 0)
        {
            throw new InvalidOperationException("No users supplied");
        }
        if (userIds.Count > MaxAwardBatch)
        {
            throw new InvalidOperationException($"At most {MaxAwardBatch} users can be awarded in one call");
        }

        // Any real account can be awarded. Badge types are global rather than
        // owned by an organization, and a game like Hockey Fights Cancer draws
        // players from across the city who hold an account but belong to no org
        // here - the earlier members-and-players rule left them permanently
        // unawardable. Ghosts are placeholders for guests with no account: they
        // can hold a roster spot but not a badge, and neither can a deleted one.
        var awardable = (await _context.Users
            .Where(u => userIds.Contains(u.Id) && !u.IsGhostPlayer && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync()).ToHashSet();

        var alreadyHeld = await _context.UserBadges
            .Where(ub => ub.BadgeTypeId == badgeType.Id && userIds.Contains(ub.UserId))
            .ToDictionaryAsync(ub => ub.UserId, ub => ub.Id);

        var occasion = string.IsNullOrWhiteSpace(request.Occasion) ? null : request.Occasion.Trim();
        var results = new List<AwardBadgeOutcomeDto>();
        var toAdd = new List<UserBadge>();

        foreach (var userId in userIds)
        {
            if (alreadyHeld.TryGetValue(userId, out var existingId))
            {
                results.Add(new AwardBadgeOutcomeDto(userId, "already_held", existingId));
            }
            else if (!awardable.Contains(userId))
            {
                // No such account, a guest placeholder, or a deleted account
                results.Add(new AwardBadgeOutcomeDto(userId, "unknown_user", null));
            }
            else
            {
                var badge = new UserBadge
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    BadgeTypeId = badgeType.Id,
                    // Null CelebratedAt is what makes the app show the
                    // celebration modal the next time they open it
                    CelebratedAt = null,
                    EarnedAt = DateTime.UtcNow,
                    Context = occasion == null
                        ? new Dictionary<string, object>()
                        : new Dictionary<string, object> { ["description"] = occasion }
                };
                toAdd.Add(badge);
                results.Add(new AwardBadgeOutcomeDto(userId, "awarded", badge.Id));
            }
        }

        if (toAdd.Count > 0)
        {
            _context.UserBadges.AddRange(toAdd);
            await _context.SaveChangesAsync();
        }

        _logger.LogInformation(
            "Badge {Code}: {Awarded} awarded, {Held} already held, {Skipped} skipped by user {UserId} in organization {OrganizationId}",
            code, toAdd.Count, alreadyHeld.Count, results.Count - toAdd.Count - alreadyHeld.Count,
            requesterId, organizationId);

        return new AwardBadgeResponse(
            code,
            toAdd.Count,
            results.Count(r => r.Outcome == "already_held"),
            results.Count(r => r.Outcome is "not_eligible" or "unknown_user"),
            results);
    }

    public async Task<bool> RevokeBadgeAsync(Guid organizationId, Guid userBadgeId, Guid requesterId)
    {
        await RequireAdminAsync(organizationId, requesterId, "revoke badges");

        var badge = await _context.UserBadges.FirstOrDefaultAsync(ub => ub.Id == userBadgeId);
        if (badge == null)
        {
            return false;
        }

        // Symmetric with awarding: whatever an admin can give, they can take
        // back. Holding revoke to a narrower set than award would strand badges
        // on accounts nobody could correct - exactly the mistakes revoke exists
        // to fix. A deleted account's badge is revocable too, so one awarded
        // before the account closed can still be cleaned up.

        _context.UserBadges.Remove(badge);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Badge {BadgeId} revoked from user {HolderId} by user {UserId} in organization {OrganizationId}",
            userBadgeId, badge.UserId, requesterId, organizationId);

        return true;
    }
}
