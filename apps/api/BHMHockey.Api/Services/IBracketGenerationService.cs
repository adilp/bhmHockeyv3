using BHMHockey.Api.Models.DTOs;

namespace BHMHockey.Api.Services;

public interface IBracketGenerationService
{
    /// <summary>
    /// Generates a single elimination bracket for the tournament.
    /// Creates all matches with proper seeding, bye handling, and NextMatchId linking.
    /// </summary>
    Task<List<TournamentMatchDto>> GenerateSingleEliminationBracketAsync(Guid tournamentId, Guid userId);

    /// <summary>
    /// Clears all matches from the tournament bracket.
    /// </summary>
    Task ClearBracketAsync(Guid tournamentId, Guid userId);

    /// <summary>
    /// Generates a round robin schedule for the tournament.
    /// Creates matches using the circle method where every team plays every other team exactly once.
    /// For N teams: generates N*(N-1)/2 matches organized into N-1 rounds (or N rounds if odd team count).
    /// </summary>
    Task<List<TournamentMatchDto>> GenerateRoundRobinScheduleAsync(Guid tournamentId, Guid userId);

    /// <summary>
    /// Generates a bracket/schedule for the tournament based on its Format setting.
    /// Dispatches to the appropriate generation method (SingleElimination, DoubleElimination, RoundRobin).
    /// </summary>
    Task<List<TournamentMatchDto>> GenerateBracketAsync(Guid tournamentId, Guid userId);
}
