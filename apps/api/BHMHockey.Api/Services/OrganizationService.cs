using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

public class OrganizationService : IOrganizationService
{
    private readonly AppDbContext _context;
    private readonly IOrganizationAdminService _adminService;
    private readonly IBadgeService _badgeService;
    private static readonly HashSet<string> ValidSkillLevels = new() { "Gold", "Silver", "Bronze", "D-League" };

    public OrganizationService(AppDbContext context, IOrganizationAdminService adminService, IBadgeService badgeService)
    {
        _context = context;
        _adminService = adminService;
        _badgeService = badgeService;
    }

    private void ValidateSkillLevels(List<string>? skillLevels)
    {
        if (skillLevels == null || skillLevels.Count == 0) return;

        foreach (var level in skillLevels)
        {
            if (!ValidSkillLevels.Contains(level))
            {
                throw new InvalidOperationException($"Invalid skill level: '{level}'. Valid values: Gold, Silver, Bronze, D-League");
            }
        }
    }

    private void ValidateEventDefaults(
        int? defaultDayOfWeek,
        int? defaultDurationMinutes,
        int? defaultMaxPlayers,
        decimal? defaultCost,
        string? defaultVenue,
        string? defaultVisibility)
    {
        if (defaultDayOfWeek.HasValue && (defaultDayOfWeek.Value < 0 || defaultDayOfWeek.Value > 6))
        {
            throw new InvalidOperationException("DefaultDayOfWeek must be between 0 (Sunday) and 6 (Saturday).");
        }

        if (defaultDurationMinutes.HasValue && (defaultDurationMinutes.Value < 15 || defaultDurationMinutes.Value > 480))
        {
            throw new InvalidOperationException("DefaultDurationMinutes must be between 15 and 480 minutes.");
        }

        if (defaultMaxPlayers.HasValue && (defaultMaxPlayers.Value < 2 || defaultMaxPlayers.Value > 100))
        {
            throw new InvalidOperationException("DefaultMaxPlayers must be between 2 and 100.");
        }

        if (defaultCost.HasValue && defaultCost.Value < 0)
        {
            throw new InvalidOperationException("DefaultCost must be greater than or equal to 0.");
        }

        if (defaultVenue != null && defaultVenue.Length > 200)
        {
            throw new InvalidOperationException("DefaultVenue must not exceed 200 characters.");
        }

        if (defaultVisibility != null)
        {
            var validVisibilities = new HashSet<string> { "Public", "OrganizationMembers", "InviteOnly" };
            if (!validVisibilities.Contains(defaultVisibility))
            {
                throw new InvalidOperationException($"Invalid DefaultVisibility: '{defaultVisibility}'. Valid values: Public, OrganizationMembers, InviteOnly");
            }
        }
    }

    public async Task<OrganizationDto> CreateAsync(CreateOrganizationRequest request, Guid creatorId)
    {
        ValidateSkillLevels(request.SkillLevels);
        ValidateEventDefaults(
            request.DefaultDayOfWeek,
            request.DefaultDurationMinutes,
            request.DefaultMaxPlayers,
            request.DefaultCost,
            request.DefaultVenue,
            request.DefaultVisibility);

        // Check for duplicate name
        var existingOrg = await _context.Organizations
            .FirstOrDefaultAsync(o => o.Name == request.Name);
        if (existingOrg != null)
        {
            throw new InvalidOperationException($"An organization with the name '{request.Name}' already exists.");
        }

        var organization = new Organization
        {
            Name = request.Name,
            Description = request.Description,
            Location = request.Location,
            SkillLevels = request.SkillLevels,
            CreatorId = creatorId,
            DefaultDayOfWeek = request.DefaultDayOfWeek,
            DefaultStartTime = request.DefaultStartTime,
            DefaultDurationMinutes = request.DefaultDurationMinutes,
            DefaultMaxPlayers = request.DefaultMaxPlayers,
            DefaultCost = request.DefaultCost,
            DefaultVenue = request.DefaultVenue,
            DefaultVisibility = request.DefaultVisibility
        };

        _context.Organizations.Add(organization);

        // Add creator as the first admin
        var admin = new OrganizationAdmin
        {
            OrganizationId = organization.Id,
            UserId = creatorId,
            AddedByUserId = null  // Original creator has no AddedBy
        };
        _context.OrganizationAdmins.Add(admin);

        // Also subscribe creator as a member
        var subscription = new OrganizationSubscription
        {
            OrganizationId = organization.Id,
            UserId = creatorId
        };
        _context.OrganizationSubscriptions.Add(subscription);

        await _context.SaveChangesAsync();

        return await MapToDto(organization, creatorId);
    }

    public async Task<List<OrganizationDto>> GetAllAsync(Guid? currentUserId = null)
    {
        var organizations = await _context.Organizations
            .Where(o => o.IsActive)
            .Include(o => o.Subscriptions)
            .ToListAsync();

        var dtos = new List<OrganizationDto>();
        foreach (var org in organizations)
        {
            dtos.Add(await MapToDto(org, currentUserId));
        }

        return dtos;
    }

    public async Task<OrganizationDto?> GetByIdAsync(Guid id, Guid? currentUserId = null)
    {
        var organization = await _context.Organizations
            .Include(o => o.Subscriptions)
            .FirstOrDefaultAsync(o => o.Id == id && o.IsActive);

        return organization == null ? null : await MapToDto(organization, currentUserId);
    }

    public async Task<OrganizationDto?> UpdateAsync(Guid id, UpdateOrganizationRequest request, Guid userId)
    {
        var organization = await _context.Organizations
            .FirstOrDefaultAsync(o => o.Id == id);

        if (organization == null) return null;

        // Check if user is an admin
        var isAdmin = await _adminService.IsUserAdminAsync(id, userId);
        if (!isAdmin) return null;

        ValidateEventDefaults(
            request.DefaultDayOfWeek,
            request.DefaultDurationMinutes,
            request.DefaultMaxPlayers,
            request.DefaultCost,
            request.DefaultVenue,
            request.DefaultVisibility);

        if (request.Name != null && request.Name != organization.Name)
        {
            // Check for duplicate name
            var existingOrg = await _context.Organizations
                .FirstOrDefaultAsync(o => o.Name == request.Name && o.Id != id);
            if (existingOrg != null)
            {
                throw new InvalidOperationException($"An organization with the name '{request.Name}' already exists.");
            }
            organization.Name = request.Name;
        }
        if (request.Description != null) organization.Description = request.Description;
        if (request.Location != null) organization.Location = request.Location;
        if (request.SkillLevels != null)
        {
            ValidateSkillLevels(request.SkillLevels);
            organization.SkillLevels = request.SkillLevels;
        }
        if (request.DefaultDayOfWeek != null) organization.DefaultDayOfWeek = request.DefaultDayOfWeek;
        if (request.DefaultStartTime != null) organization.DefaultStartTime = request.DefaultStartTime;
        if (request.DefaultDurationMinutes != null) organization.DefaultDurationMinutes = request.DefaultDurationMinutes;
        if (request.DefaultMaxPlayers != null) organization.DefaultMaxPlayers = request.DefaultMaxPlayers;
        if (request.DefaultCost != null) organization.DefaultCost = request.DefaultCost;
        if (request.DefaultVenue != null) organization.DefaultVenue = request.DefaultVenue;
        if (request.DefaultVisibility != null) organization.DefaultVisibility = request.DefaultVisibility;

        organization.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await MapToDto(organization, userId);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid userId)
    {
        var organization = await _context.Organizations
            .FirstOrDefaultAsync(o => o.Id == id);

        if (organization == null) return false;

        // Check if user is an admin
        var isAdmin = await _adminService.IsUserAdminAsync(id, userId);
        if (!isAdmin) return false;

        organization.IsActive = false;
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> SubscribeAsync(Guid organizationId, Guid userId)
    {
        var exists = await _context.OrganizationSubscriptions
            .AnyAsync(s => s.OrganizationId == organizationId && s.UserId == userId);

        if (exists) return false;

        var subscription = new OrganizationSubscription
        {
            OrganizationId = organizationId,
            UserId = userId
        };

        _context.OrganizationSubscriptions.Add(subscription);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> UnsubscribeAsync(Guid organizationId, Guid userId)
    {
        var subscription = await _context.OrganizationSubscriptions
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId && s.UserId == userId);

        if (subscription == null) return false;

        _context.OrganizationSubscriptions.Remove(subscription);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<List<OrganizationSubscriptionDto>> GetUserSubscriptionsAsync(Guid userId)
    {
        var subscriptions = await _context.OrganizationSubscriptions
            .Include(s => s.Organization)
            .Where(s => s.UserId == userId && s.Organization.IsActive)
            .ToListAsync();

        var dtos = new List<OrganizationSubscriptionDto>();
        foreach (var sub in subscriptions)
        {
            var orgDto = await MapToDto(sub.Organization, userId);
            dtos.Add(new OrganizationSubscriptionDto(
                sub.Id,
                orgDto,
                sub.NotificationEnabled,
                sub.SubscribedAt
            ));
        }

        return dtos;
    }

    public async Task<List<OrganizationDto>> GetUserAdminOrganizationsAsync(Guid userId)
    {
        // Get organization IDs where user is an admin
        var adminOrgIds = await _context.OrganizationAdmins
            .Where(a => a.UserId == userId)
            .Select(a => a.OrganizationId)
            .ToListAsync();

        var organizations = await _context.Organizations
            .Include(o => o.Subscriptions)
            .Where(o => adminOrgIds.Contains(o.Id) && o.IsActive)
            .OrderBy(o => o.Name)
            .ToListAsync();

        var dtos = new List<OrganizationDto>();
        foreach (var org in organizations)
        {
            dtos.Add(await MapToDto(org, userId));
        }

        return dtos;
    }

    public async Task<List<OrganizationMemberDto>> GetMembersAsync(Guid organizationId, Guid requesterId)
    {
        // Check if requester is subscribed to the organization
        var isSubscribed = await _context.OrganizationSubscriptions
            .AnyAsync(s => s.OrganizationId == organizationId && s.UserId == requesterId);

        // Check if requester is an admin
        var isAdmin = await _adminService.IsUserAdminAsync(organizationId, requesterId);

        // Only subscribers or admins can see the members list
        if (!isSubscribed && !isAdmin)
        {
            return new List<OrganizationMemberDto>();
        }

        // Get all admin user IDs for this organization
        var adminUserIds = await _context.OrganizationAdmins
            .Where(a => a.OrganizationId == organizationId)
            .Select(a => a.UserId)
            .ToListAsync();

        var subscriptions = await _context.OrganizationSubscriptions
            .Include(s => s.User)
            .Where(s => s.OrganizationId == organizationId)
            .OrderBy(s => s.User.LastName)
            .ThenBy(s => s.User.FirstName)
            .ToListAsync();

        // Build member DTOs with badges
        var members = new List<OrganizationMemberDto>();
        foreach (var s in subscriptions)
        {
            // Get top 3 badges for this user
            var topBadges = await _badgeService.GetUserTopBadgesAsync(s.User.Id, 3);
            var totalBadgeCount = await _context.UserBadges.CountAsync(ub => ub.UserId == s.User.Id);

            members.Add(new OrganizationMemberDto(
                s.User.Id,
                s.User.FirstName,
                s.User.LastName,
                isAdmin ? s.User.Email : null, // Only admins see email
                s.User.Positions,
                s.SubscribedAt,
                adminUserIds.Contains(s.User.Id),
                topBadges,
                totalBadgeCount
            ));
        }

        return members;
    }

    public async Task<bool> RemoveMemberAsync(Guid organizationId, Guid memberUserId, Guid requesterId)
    {
        // Verify requester is an organization admin
        var isAdmin = await _adminService.IsUserAdminAsync(organizationId, requesterId);
        if (!isAdmin)
        {
            return false;
        }

        // Find the subscription
        var subscription = await _context.OrganizationSubscriptions
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId && s.UserId == memberUserId);

        if (subscription == null)
        {
            return false;
        }

        // Remove the subscription
        _context.OrganizationSubscriptions.Remove(subscription);
        await _context.SaveChangesAsync();

        return true;
    }

    private async Task<OrganizationDto> MapToDto(Organization org, Guid? currentUserId)
    {
        var subscriberCount = org.Subscriptions?.Count ??
            await _context.OrganizationSubscriptions.CountAsync(s => s.OrganizationId == org.Id);

        var isSubscribed = currentUserId.HasValue &&
            (org.Subscriptions?.Any(s => s.UserId == currentUserId.Value) ??
             await _context.OrganizationSubscriptions.AnyAsync(s => s.OrganizationId == org.Id && s.UserId == currentUserId.Value));

        var isAdmin = currentUserId.HasValue &&
            await _adminService.IsUserAdminAsync(org.Id, currentUserId.Value);

        return new OrganizationDto(
            org.Id,
            org.Name,
            org.Description,
            org.Location,
            org.SkillLevels,
            org.CreatorId,
            subscriberCount,
            isSubscribed,
            isAdmin,
            org.CreatedAt,
            org.DefaultDayOfWeek,
            org.DefaultStartTime,
            org.DefaultDurationMinutes,
            org.DefaultMaxPlayers,
            org.DefaultCost,
            org.DefaultVenue,
            org.DefaultVisibility
        );
    }
}
