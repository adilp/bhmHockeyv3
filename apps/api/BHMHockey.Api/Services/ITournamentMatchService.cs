using BHMHockey.Api.Models.DTOs;

namespace BHMHockey.Api.Services;

/// <summary>
/// Service for read-only operations on tournament matches.
/// </summary>
public interface ITournamentMatchService
{
    /// <summary>
    /// Gets all matches for a tournament, ordered by Round then MatchNumber.
    /// Includes team information (HomeTeam, AwayTeam, WinnerTeam).
    /// </summary>
    Task<List<TournamentMatchDto>> GetAllAsync(Guid tournamentId);

    /// <summary>
    /// Gets a specific match by ID if it belongs to the specified tournament.
    /// Includes team information (HomeTeam, AwayTeam, WinnerTeam).
    /// Returns null if match not found or doesn't belong to tournament.
    /// </summary>
    Task<TournamentMatchDto?> GetByIdAsync(Guid tournamentId, Guid matchId);
}
