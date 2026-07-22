using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

public class OrganizationAutoRosterService : IOrganizationAutoRosterService
{
    private readonly AppDbContext _context;
    private readonly IOrganizationAdminService _adminService;
    private readonly ILogger<OrganizationAutoRosterService> _logger;

    public OrganizationAutoRosterService(
        AppDbContext context,
        IOrganizationAdminService adminService,
        ILogger<OrganizationAutoRosterService> logger)
    {
        _context = context;
        _adminService = adminService;
        _logger = logger;
    }

    public async Task<List<AutoRosterMemberDto>> GetAutoRosterAsync(Guid organizationId, Guid requesterId)
    {
        await EnsureAdminAsync(organizationId, requesterId);

        var members = await _context.OrganizationAutoRosterMembers
            .Include(m => m.User)
            .Where(m => m.OrganizationId == organizationId)
            .OrderBy(m => m.SortOrder)
            .ToListAsync();

        return members.Select(MapToDto).ToList();
    }

    public async Task<AutoRosterMemberDto> AddMemberAsync(Guid organizationId, Guid userId, string position, Guid requesterId)
    {
        await EnsureAdminAsync(organizationId, requesterId);

        var normalizedPosition = PositionNormalizer.Normalize(position);

        var isSubscriber = await _context.OrganizationSubscriptions
            .AnyAsync(s => s.OrganizationId == organizationId && s.UserId == userId);
        if (!isSubscriber)
        {
            _logger.LogWarning("Auto-roster add rejected: user {UserId} is not a subscriber of organization {OrganizationId}", userId, organizationId);
            throw new InvalidOperationException("User must be a member of this organization");
        }

        var alreadyInList = await _context.OrganizationAutoRosterMembers
            .AnyAsync(m => m.OrganizationId == organizationId && m.UserId == userId);
        if (alreadyInList)
        {
            _logger.LogWarning("Auto-roster add rejected: user {UserId} is already in the auto-roster for organization {OrganizationId}", userId, organizationId);
            throw new InvalidOperationException("User is already in the auto-roster");
        }

        var maxSortOrder = await _context.OrganizationAutoRosterMembers
            .Where(m => m.OrganizationId == organizationId)
            .MaxAsync(m => (int?)m.SortOrder) ?? -1;

        var member = new OrganizationAutoRosterMember
        {
            OrganizationId = organizationId,
            UserId = userId,
            Position = normalizedPosition,
            SortOrder = maxSortOrder + 1,
            AddedByUserId = requesterId
        };

        _context.OrganizationAutoRosterMembers.Add(member);
        await _context.SaveChangesAsync();

        await _context.Entry(member).Reference(m => m.User).LoadAsync();

        return MapToDto(member);
    }

    public async Task<bool> RemoveMemberAsync(Guid organizationId, Guid userId, Guid requesterId)
    {
        await EnsureAdminAsync(organizationId, requesterId);

        var member = await _context.OrganizationAutoRosterMembers
            .FirstOrDefaultAsync(m => m.OrganizationId == organizationId && m.UserId == userId);

        if (member == null)
        {
            _logger.LogWarning("Auto-roster remove failed: user {UserId} not in auto-roster for organization {OrganizationId}", userId, organizationId);
            return false;
        }

        _context.OrganizationAutoRosterMembers.Remove(member);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<List<AutoRosterMemberDto>> ReorderAsync(Guid organizationId, List<Guid> orderedUserIds, Guid requesterId)
    {
        await EnsureAdminAsync(organizationId, requesterId);

        var members = await _context.OrganizationAutoRosterMembers
            .Include(m => m.User)
            .Where(m => m.OrganizationId == organizationId)
            .ToListAsync();

        var orderedIdSet = orderedUserIds.ToHashSet();
        var memberIdSet = members.Select(m => m.UserId).ToHashSet();
        if (orderedUserIds.Count != orderedIdSet.Count || !orderedIdSet.SetEquals(memberIdSet))
        {
            _logger.LogWarning("Auto-roster reorder rejected for organization {OrganizationId}: ordered list does not match current members", organizationId);
            throw new InvalidOperationException("All auto-roster members must be included exactly once");
        }

        var membersByUserId = members.ToDictionary(m => m.UserId);
        for (var i = 0; i < orderedUserIds.Count; i++)
        {
            membersByUserId[orderedUserIds[i]].SortOrder = i;
        }

        await _context.SaveChangesAsync();

        return members.OrderBy(m => m.SortOrder).Select(MapToDto).ToList();
    }

    private async Task EnsureAdminAsync(Guid organizationId, Guid requesterId)
    {
        var isAdmin = await _adminService.IsUserAdminAsync(organizationId, requesterId);
        if (!isAdmin)
        {
            _logger.LogWarning("Auto-roster access denied: user {RequesterId} is not an admin of organization {OrganizationId}", requesterId, organizationId);
            throw new UnauthorizedAccessException("You don't have permission to manage this organization's auto-roster");
        }
    }

    private static AutoRosterMemberDto MapToDto(OrganizationAutoRosterMember member)
    {
        return new AutoRosterMemberDto(
            member.Id,
            member.UserId,
            member.User.FirstName,
            member.User.LastName,
            member.User.Positions,
            member.Position,
            member.SortOrder,
            member.AddedAt
        );
    }
}
