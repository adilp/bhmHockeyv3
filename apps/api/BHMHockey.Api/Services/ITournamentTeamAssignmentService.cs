using BHMHockey.Api.Models.DTOs;

namespace BHMHockey.Api.Services;

/// <summary>
/// Service for managing team assignments in tournaments.
/// Handles manual assignment, auto-assignment, and bulk team creation operations.
/// </summary>
public interface ITournamentTeamAssignmentService
{
    /// <summary>
    /// Assigns a single player registration to a specific team.
    /// Only allowed when tournament is in Draft, Open, or RegistrationClosed status (before InProgress).
    /// Requires the user to be a tournament admin.
    /// </summary>
    /// <param name="tournamentId">The tournament ID</param>
    /// <param name="registrationId">The registration ID to assign</param>
    /// <param name="teamId">The team ID to assign the player to</param>
    /// <param name="adminUserId">The admin user performing the assignment</param>
    /// <returns>Updated registration DTO with team assignment, or null if registration not found</returns>
    /// <exception cref="InvalidOperationException">Thrown when tournament is InProgress or beyond, or team not found</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when user is not a tournament admin</exception>
    Task<TournamentRegistrationDto?> AssignPlayerToTeamAsync(Guid tournamentId, Guid registrationId, Guid teamId, Guid adminUserId);

    /// <summary>
    /// Auto-assigns all unassigned registrations to teams using either balanced or random distribution.
    /// Only allowed when tournament is in Draft, Open, or RegistrationClosed status (before InProgress).
    /// Requires the user to be a tournament admin.
    /// Algorithm:
    /// - Separates goalies and skaters
    /// - Round-robin assigns goalies to ensure at least 1 per team if available
    /// - If balanceBySkillLevel: snake-draft skaters by skill level (Gold > Silver > Bronze > D-League)
    /// - Else: random distribution of skaters across teams
    /// </summary>
    /// <param name="tournamentId">The tournament ID</param>
    /// <param name="balanceBySkillLevel">Whether to balance teams by skill level (defaults to false)</param>
    /// <param name="adminUserId">The admin user performing the auto-assignment</param>
    /// <returns>Result DTO containing success status, message, and assignment counts</returns>
    /// <exception cref="InvalidOperationException">Thrown when tournament is InProgress or beyond, or no teams exist</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when user is not a tournament admin</exception>
    Task<TeamAssignmentResultDto> AutoAssignTeamsAsync(Guid tournamentId, bool balanceBySkillLevel, Guid adminUserId);

    /// <summary>
    /// Creates multiple empty teams in bulk.
    /// Only allowed when tournament is in Draft, Open, or RegistrationClosed status (before InProgress).
    /// Requires the user to be a tournament admin.
    /// Team names follow the pattern "{namePrefix} 1", "{namePrefix} 2", etc.
    /// All teams start with Status="Registered", all standings fields set to 0, and HasBye=false.
    /// </summary>
    /// <param name="tournamentId">The tournament ID</param>
    /// <param name="count">Number of teams to create</param>
    /// <param name="namePrefix">Prefix for team names (e.g., "Team" creates "Team 1", "Team 2", etc.)</param>
    /// <param name="adminUserId">The admin user performing the bulk creation</param>
    /// <returns>Response containing the created teams and success message</returns>
    /// <exception cref="InvalidOperationException">Thrown when tournament is InProgress or beyond, or count is invalid</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when user is not a tournament admin</exception>
    Task<BulkCreateTeamsResponse> BulkCreateTeamsAsync(Guid tournamentId, int count, string namePrefix, Guid adminUserId);

    /// <summary>
    /// Removes a player from their assigned team, setting their team assignment to null.
    /// Only allowed when tournament is in Draft, Open, or RegistrationClosed status (before InProgress).
    /// Requires the user to be a tournament admin.
    /// Used when reassigning players (remove from old team, then assign to new team).
    /// </summary>
    /// <param name="tournamentId">The tournament ID</param>
    /// <param name="registrationId">The registration ID to remove from team</param>
    /// <param name="adminUserId">The admin user performing the removal</param>
    /// <returns>True if player was removed from team, false if registration not found or player was not assigned</returns>
    /// <exception cref="InvalidOperationException">Thrown when tournament is InProgress or beyond</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when user is not a tournament admin</exception>
    Task<bool> RemovePlayerFromTeamAsync(Guid tournamentId, Guid registrationId, Guid adminUserId);
}
