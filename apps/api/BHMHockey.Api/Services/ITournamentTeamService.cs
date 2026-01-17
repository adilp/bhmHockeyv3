using BHMHockey.Api.Models.DTOs;

namespace BHMHockey.Api.Services;

public interface ITournamentTeamService
{
    /// <summary>
    /// Creates a new team for a tournament.
    /// Only allowed when tournament is in Draft, Open, or RegistrationClosed status.
    /// Requires the user to be a tournament admin.
    /// Automatically sets Status="Registered", all standings fields to 0, HasBye=false.
    /// </summary>
    Task<TournamentTeamDto> CreateAsync(Guid tournamentId, CreateTournamentTeamRequest request, Guid userId);

    /// <summary>
    /// Gets all teams for a tournament.
    /// Teams are ordered by Seed (nulls last), then by Name.
    /// Includes captain user information if available.
    /// </summary>
    Task<List<TournamentTeamDto>> GetAllAsync(Guid tournamentId);

    /// <summary>
    /// Gets a specific team by ID within a tournament.
    /// Returns null if not found.
    /// Includes captain user information if available.
    /// </summary>
    Task<TournamentTeamDto?> GetByIdAsync(Guid tournamentId, Guid teamId);

    /// <summary>
    /// Updates a tournament team using patch semantics (only provided fields are updated).
    /// Requires the user to be a tournament admin.
    /// Returns null if team not found.
    /// </summary>
    Task<TournamentTeamDto?> UpdateAsync(Guid tournamentId, Guid teamId, UpdateTournamentTeamRequest request, Guid userId);

    /// <summary>
    /// Deletes a tournament team.
    /// Only allowed when tournament is in Draft, Open, or RegistrationClosed status (before InProgress).
    /// Requires the user to be a tournament admin.
    /// Returns true if deleted, false if not found.
    /// </summary>
    Task<bool> DeleteAsync(Guid tournamentId, Guid teamId, Guid userId);

    /// <summary>
    /// Checks if a user can manage a specific team (is captain OR tournament admin).
    /// </summary>
    Task<bool> CanUserManageTeamAsync(Guid tournamentId, Guid teamId, Guid userId);
}
