using BHMHockey.Api.Models.DTOs;

namespace BHMHockey.Api.Services;

public interface IOrganizationAutoRosterService
{
    /// <summary>
    /// Get the organization's auto-roster list ordered by SortOrder. Admin only.
    /// </summary>
    Task<List<AutoRosterMemberDto>> GetAutoRosterAsync(Guid organizationId, Guid requesterId);

    /// <summary>
    /// Add a subscriber to the auto-roster list at the end of the order. Admin only.
    /// </summary>
    Task<AutoRosterMemberDto> AddMemberAsync(Guid organizationId, Guid userId, string position, Guid requesterId);

    /// <summary>
    /// Remove a user from the auto-roster list. Admin only. Returns false if not in the list.
    /// </summary>
    Task<bool> RemoveMemberAsync(Guid organizationId, Guid userId, Guid requesterId);

    /// <summary>
    /// Reorder the auto-roster list. All current members must be included. Admin only.
    /// </summary>
    Task<List<AutoRosterMemberDto>> ReorderAsync(Guid organizationId, List<Guid> orderedUserIds, Guid requesterId);
}
