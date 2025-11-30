using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

public class OrganizationAdminService : IOrganizationAdminService
{
    private readonly AppDbContext _context;

    public OrganizationAdminService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<bool> IsUserAdminAsync(Guid organizationId, Guid userId)
    {
        return await _context.OrganizationAdmins
            .AnyAsync(a => a.OrganizationId == organizationId && a.UserId == userId);
    }

    public async Task<List<OrganizationAdminDto>> GetAdminsAsync(Guid organizationId, Guid requesterId)
    {
        // Only admins can view the admin list
        var isAdmin = await IsUserAdminAsync(organizationId, requesterId);
        if (!isAdmin)
        {
            return new List<OrganizationAdminDto>();
        }

        var admins = await _context.OrganizationAdmins
            .Include(a => a.User)
            .Include(a => a.AddedByUser)
            .Where(a => a.OrganizationId == organizationId)
            .OrderBy(a => a.AddedAt)
            .ToListAsync();

        return admins.Select(a => new OrganizationAdminDto(
            a.Id,
            a.UserId,
            a.User.FirstName,
            a.User.LastName,
            a.User.Email,
            a.AddedAt,
            a.AddedByUserId,
            a.AddedByUser != null ? $"{a.AddedByUser.FirstName} {a.AddedByUser.LastName}" : null
        )).ToList();
    }

    public async Task<bool> AddAdminAsync(Guid organizationId, Guid userIdToAdd, Guid requesterId)
    {
        // Only existing admins can add new admins
        var isRequesterAdmin = await IsUserAdminAsync(organizationId, requesterId);
        if (!isRequesterAdmin)
        {
            return false;
        }

        // Check if user is already an admin
        var alreadyAdmin = await IsUserAdminAsync(organizationId, userIdToAdd);
        if (alreadyAdmin)
        {
            return false;
        }

        // Verify the user exists
        var userExists = await _context.Users.AnyAsync(u => u.Id == userIdToAdd);
        if (!userExists)
        {
            return false;
        }

        var admin = new OrganizationAdmin
        {
            OrganizationId = organizationId,
            UserId = userIdToAdd,
            AddedByUserId = requesterId
        };

        _context.OrganizationAdmins.Add(admin);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<bool> RemoveAdminAsync(Guid organizationId, Guid userIdToRemove, Guid requesterId)
    {
        // Only existing admins can remove admins
        var isRequesterAdmin = await IsUserAdminAsync(organizationId, requesterId);
        if (!isRequesterAdmin)
        {
            return false;
        }

        // Check admin count - cannot remove last admin
        var adminCount = await _context.OrganizationAdmins
            .CountAsync(a => a.OrganizationId == organizationId);

        if (adminCount <= 1)
        {
            throw new InvalidOperationException("Cannot remove the last admin from an organization.");
        }

        var admin = await _context.OrganizationAdmins
            .FirstOrDefaultAsync(a => a.OrganizationId == organizationId && a.UserId == userIdToRemove);

        if (admin == null)
        {
            return false;
        }

        _context.OrganizationAdmins.Remove(admin);
        await _context.SaveChangesAsync();

        return true;
    }
}
