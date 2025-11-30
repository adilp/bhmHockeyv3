using BHMHockey.Api.Models.DTOs;

namespace BHMHockey.Api.Services;

public interface IOrganizationAdminService
{
    /// <summary>
    /// Check if a user is an admin of the specified organization.
    /// </summary>
    Task<bool> IsUserAdminAsync(Guid organizationId, Guid userId);

    /// <summary>
    /// Get all admins for an organization. Only admins can view the admin list.
    /// </summary>
    Task<List<OrganizationAdminDto>> GetAdminsAsync(Guid organizationId, Guid requesterId);

    /// <summary>
    /// Add a user as an admin to an organization. Only existing admins can add new admins.
    /// </summary>
    Task<bool> AddAdminAsync(Guid organizationId, Guid userIdToAdd, Guid requesterId);

    /// <summary>
    /// Remove an admin from an organization. Only admins can remove other admins.
    /// Cannot remove the last admin.
    /// </summary>
    Task<bool> RemoveAdminAsync(Guid organizationId, Guid userIdToRemove, Guid requesterId);
}
