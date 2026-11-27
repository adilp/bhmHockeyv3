using BHMHockey.Api.Models.DTOs;

namespace BHMHockey.Api.Services;

/// <summary>
/// The one writer and reader of game results.
///
/// WRITE (PutResultAsync), in order - each refusal is logged at Warning:
///  1. The event exists (else null -> 404), has an organization, and the caller is
///     an admin of it (OrganizationAdminService) - else UnauthorizedAccessException -> 403.
///  2. The event is not Draft/Cancelled, has been played, and is on or after
///     GameResultSettings.EarliestGameDate. InvalidOperationException -> 400.
///  3. The body is valid (Validate): schema version, route id, sides, scores equal
///     goals per side, every scorer/assist names a line on the SAME side, stars
///     1..3 unique, estimates text with a known qualifier. -> 400 naming the problem.
///  4. Revision: none stored -> insert "created"; lower -> 409; equal with the
///     same body hash -> "unchanged" (a retry is safe); equal with another body
///     -> 409; higher -> lines and goals replaced whole in one transaction, "updated".
///  5. Each line's UserId is kept only for an existing, active, non-ghost user;
///     otherwise cleared to a name-only line and reported - never a failure.
///
/// READ: every read returns null (-> 404) when GameResultOptions.ReadsEnabled is
/// false, which is the feature's remote off switch.
/// </summary>
public interface IGameResultService
{
    Task<GameResultUpsertResponse?> PutResultAsync(Guid eventId, GameResultUpsertRequest request, Guid callerId);
    Task<WithdrawGameResultResponse?> WithdrawResultAsync(Guid eventId, WithdrawGameResultRequest request, Guid callerId);
    Task<GameResultDto?> GetResultAsync(Guid eventId, Guid? callerId);
    Task<PlayerStatsDto?> GetPlayerStatsAsync(Guid userId, string? season, Guid callerId);
    Task<LeaderboardDto?> GetLeaderboardAsync(Guid organizationId, string? season, string? stat, Guid callerId);
    Task<List<GameResultSummaryDto>?> GetRecentResultsAsync(Guid organizationId, int limit, Guid callerId);
    Task<StatsVisibilityDto?> GetStatsVisibilityAsync(Guid callerId);
    Task<StatsVisibilityDto?> SetStatsVisibilityAsync(Guid callerId, bool hidden);
}
