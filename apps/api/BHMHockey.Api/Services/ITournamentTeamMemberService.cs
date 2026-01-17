using BHMHockey.Api.Models.DTOs;

namespace BHMHockey.Api.Services;

/// <summary>
/// Service for managing tournament team members with invitation workflow (TRN-011).
/// Supports organizer-driven team building with Pending/Accepted/Declined statuses.
/// </summary>
public interface ITournamentTeamMemberService
{
    /// <summary>
    /// Adds a player to a team with Pending status (requires admin).
    /// </summary>
    /// <param name="tournamentId">Tournament ID</param>
    /// <param name="teamId">Team ID</param>
    /// <param name="userId">User ID to add</param>
    /// <param name="adminUserId">Admin performing the action</param>
    /// <returns>Created team member DTO</returns>
    /// <exception cref="UnauthorizedAccessException">If user is not tournament admin</exception>
    /// <exception cref="InvalidOperationException">If team is full or player already on another team</exception>
    Task<TournamentTeamMemberDto> AddPlayerAsync(Guid tournamentId, Guid teamId, Guid userId, Guid adminUserId);

    /// <summary>
    /// Removes a player from a team by setting LeftAt (soft delete, requires admin).
    /// </summary>
    /// <param name="tournamentId">Tournament ID</param>
    /// <param name="teamId">Team ID</param>
    /// <param name="userId">User ID to remove</param>
    /// <param name="adminUserId">Admin performing the action</param>
    /// <returns>True if removed, false if not found</returns>
    /// <exception cref="UnauthorizedAccessException">If user is not tournament admin</exception>
    Task<bool> RemovePlayerAsync(Guid tournamentId, Guid teamId, Guid userId, Guid adminUserId);

    /// <summary>
    /// Player responds to team invitation. Accept creates TournamentRegistration.
    /// </summary>
    /// <param name="tournamentId">Tournament ID</param>
    /// <param name="teamId">Team ID</param>
    /// <param name="userId">User ID responding</param>
    /// <param name="accept">True to accept, false to decline</param>
    /// <param name="position">Position (required if accept=true): "Goalie" or "Skater"</param>
    /// <param name="customResponses">JSON string of custom registration responses</param>
    /// <returns>Updated team member DTO</returns>
    /// <exception cref="InvalidOperationException">If member not found or already responded</exception>
    Task<TournamentTeamMemberDto> RespondAsync(
        Guid tournamentId,
        Guid teamId,
        Guid userId,
        bool accept,
        string? position,
        string? customResponses);

    /// <summary>
    /// Gets all team members for a team (excludes removed members where LeftAt is set).
    /// </summary>
    /// <param name="tournamentId">Tournament ID</param>
    /// <param name="teamId">Team ID</param>
    /// <returns>List of team members with all statuses</returns>
    Task<List<TournamentTeamMemberDto>> GetTeamMembersAsync(Guid tournamentId, Guid teamId);
}
