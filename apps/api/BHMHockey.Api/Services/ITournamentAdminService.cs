using BHMHockey.Api.Models.DTOs;

namespace BHMHockey.Api.Services;

public interface ITournamentAdminService
{
    /// <summary>
    /// Gets all admins for a tournament.
    /// Returns list of admins with user details.
    /// </summary>
    Task<List<TournamentAdminDto>> GetAdminsAsync(Guid tournamentId);

    /// <summary>
    /// Adds a new admin to the tournament.
    /// Only Owner can add admins.
    /// Cannot add someone who is already an admin.
    /// Cannot add with Owner role (use TransferOwnership instead).
    /// </summary>
    Task<TournamentAdminDto> AddAdminAsync(Guid tournamentId, Guid userIdToAdd, string role, Guid requesterId);

    /// <summary>
    /// Updates an admin's role.
    /// Only Owner can update roles.
    /// Cannot change to/from Owner role (use TransferOwnership).
    /// </summary>
    Task<TournamentAdminDto?> UpdateRoleAsync(Guid tournamentId, Guid userId, string newRole, Guid requesterId);

    /// <summary>
    /// Removes an admin from the tournament.
    /// Only Owner can remove admins.
    /// Cannot remove the Owner (must transfer ownership first).
    /// Cannot remove the last admin.
    /// </summary>
    Task<bool> RemoveAdminAsync(Guid tournamentId, Guid userIdToRemove, Guid requesterId);

    /// <summary>
    /// Transfers ownership to another admin.
    /// Only Owner can transfer.
    /// New owner must already be an admin.
    /// Old owner becomes Admin role.
    /// </summary>
    Task<TournamentAdminDto> TransferOwnershipAsync(Guid tournamentId, Guid newOwnerUserId, Guid requesterId);
}
