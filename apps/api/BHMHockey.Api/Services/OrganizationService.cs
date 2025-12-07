using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

public class OrganizationService : IOrganizationService
{
    private readonly AppDbContext _context;
    private readonly IOrganizationAdminService _adminService;
    private static readonly HashSet<string> ValidSkillLevels = new() { "Gold", "Silver", "Bronze", "D-League" };

    public OrganizationService(AppDbContext context, IOrganizationAdminService adminService)
    {
        _context = context;
        _adminService = adminService;
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

    public async Task<OrganizationDto> CreateAsync(CreateOrganizationRequest request, Guid creatorId)
    {
        ValidateSkillLevels(request.SkillLevels);

        var organization = new Organization
        {
            Name = request.Name,
            Description = request.Description,
            Location = request.Location,
            SkillLevels = request.SkillLevels,
            CreatorId = creatorId
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

        if (request.Name != null) organization.Name = request.Name;
        if (request.Description != null) organization.Description = request.Description;
        if (request.Location != null) organization.Location = request.Location;
        if (request.SkillLevels != null)
        {
            ValidateSkillLevels(request.SkillLevels);
            organization.SkillLevels = request.SkillLevels;
        }

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
        // Verify requester is an organization admin
        var isAdmin = await _adminService.IsUserAdminAsync(organizationId, requesterId);
        if (!isAdmin)
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

        return subscriptions.Select(s => new OrganizationMemberDto(
            s.User.Id,
            s.User.FirstName,
            s.User.LastName,
            s.User.Email,
            s.User.Positions,
            s.SubscribedAt,
            adminUserIds.Contains(s.User.Id)
        )).ToList();
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
            org.CreatedAt
        );
    }
}
