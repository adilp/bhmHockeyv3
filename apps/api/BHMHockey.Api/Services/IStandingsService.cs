using BHMHockey.Api.Models.DTOs;

namespace BHMHockey.Api.Services;

/// <summary>
/// Service for calculating and retrieving tournament standings.
/// </summary>
public interface IStandingsService
{
    /// <summary>
    /// Gets standings for a tournament. Works for all tournament formats.
    /// Teams are sorted by points, then tiebreakers (head-to-head, goal differential, goals scored).
    /// For unresolvable ties (3+ teams), returns tiedGroups array for manual resolution.
    /// </summary>
    /// <param name="tournamentId">Tournament ID</param>
    /// <returns>Standings with sorted teams, playoff cutoff line, and tied groups. Returns null if tournament not found.</returns>
    Task<StandingsDto?> GetStandingsAsync(Guid tournamentId);

    /// <summary>
    /// Manually resolves tied standings by setting final placements for teams.
    /// Requires tournament admin role. Creates audit log entry.
    /// </summary>
    /// <param name="tournamentId">Tournament ID</param>
    /// <param name="resolutions">List of team ID to final placement assignments</param>
    /// <param name="userId">User performing the resolution (for authorization and audit)</param>
    /// <returns>True if successful</returns>
    /// <exception cref="UnauthorizedAccessException">If user is not an admin</exception>
    /// <exception cref="InvalidOperationException">If validation fails</exception>
    Task<bool> ResolveTiesAsync(Guid tournamentId, List<TieResolutionItem> resolutions, Guid userId);
}
