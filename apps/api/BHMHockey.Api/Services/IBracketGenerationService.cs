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
}
