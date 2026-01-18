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
}
