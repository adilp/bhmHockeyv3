using BHMHockey.Api.Models.DTOs;

namespace BHMHockey.Api.Services;

/// <summary>
/// Service for operations on tournament matches including read, score entry, and match management.
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

    /// <summary>
    /// Enters the score for a match. Automatically determines winner, advances to next match,
    /// updates team statistics, and logs the action for audit.
    /// </summary>
    /// <param name="tournamentId">Tournament ID</param>
    /// <param name="matchId">Match ID</param>
    /// <param name="request">Score entry request with homeScore, awayScore, and optional overtimeWinnerId</param>
    /// <param name="userId">User performing the action (must be tournament admin/scorekeeper)</param>
    /// <returns>Updated match DTO</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when: match not found, match has TBD teams, tournament not InProgress,
    /// tied score in elimination without overtimeWinnerId, invalid overtimeWinnerId
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when user is not authorized to manage tournament</exception>
    Task<TournamentMatchDto> EnterScoreAsync(Guid tournamentId, Guid matchId, EnterScoreRequest request, Guid userId);

    /// <summary>
    /// Marks a match as forfeit. The non-forfeiting team wins and advances.
    /// </summary>
    /// <param name="tournamentId">Tournament ID</param>
    /// <param name="matchId">Match ID</param>
    /// <param name="request">Forfeit request with forfeiting team ID and reason</param>
    /// <param name="userId">User performing the action (must be tournament admin)</param>
    /// <returns>Updated match DTO</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when: match not found, invalid forfeiting team, match already completed
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when user is not authorized to manage tournament</exception>
    Task<TournamentMatchDto> ForfeitMatchAsync(Guid tournamentId, Guid matchId, ForfeitMatchRequest request, Guid userId);
}
