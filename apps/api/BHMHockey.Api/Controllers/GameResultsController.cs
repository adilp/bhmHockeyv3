using System.Security.Claims;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Exceptions;
using BHMHockey.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BHMHockey.Api.Controllers;

/// <summary>
/// Game results: every endpoint here is NEW. They live in their own controller
/// so that no existing controller, its constructor or any of its actions
/// changes (Contract/InstalledClientContractTests pins the old ones).
///
/// Every read answers 404 when there is nothing to show - including when the
/// feature is switched off (GameResults__ReadsEnabled=false) - and the app
/// treats 404 as "no stats", so nothing new appears. No read answers 401 for an
/// authenticated caller (the app logs a user out on 401): a caller who may not
/// see something gets 403 or 404.
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
public class GameResultsController : ControllerBase
{
    private readonly IGameResultService _results;
    private readonly IPastGameService _pastGames;
    private readonly ILogger<GameResultsController> _logger;

    public GameResultsController(IGameResultService results, IPastGameService pastGames, ILogger<GameResultsController> logger)
    {
        _results = results;
        _pastGames = pastGames;
        _logger = logger;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User ID not found in token");
        }
        return userId;
    }

    // ------------------------------------------------------------------ writes

    /// <summary>
    /// Publish or correct a game's result (admin of the event's organization).
    /// Idempotent: the same revision and body again is "unchanged".
    /// </summary>
    [HttpPut("events/{id:guid}/result")]
    public async Task<ActionResult<GameResultUpsertResponse>> PutResult(Guid id, [FromBody] GameResultUpsertRequest request)
    {
        try
        {
            var result = await _results.PutResultAsync(id, request, GetCurrentUserId());
            return result == null ? NotFound(new { message = "Event not found" }) : Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Put result denied for event {EventId}", id);
            return Forbid();
        }
        catch (StaleRevisionException ex)
        {
            _logger.LogWarning("Put result conflict for event {EventId}: {Message}", id, ex.Message);
            return Conflict(new { message = ex.Message, currentRevision = ex.CurrentRevision });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Put result refused for event {EventId}: {Message}", id, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Withdraw a published result (soft: Status = Withdrawn).</summary>
    [HttpPost("events/{id:guid}/result/withdraw")]
    public async Task<ActionResult<WithdrawGameResultResponse>> WithdrawResult(Guid id, [FromBody] WithdrawGameResultRequest request)
    {
        try
        {
            var result = await _results.WithdrawResultAsync(id, request, GetCurrentUserId());
            if (result == null)
            {
                _logger.LogWarning("Withdraw: no result for event {EventId}", id);
                return NotFound(new { message = "No result for this event" });
            }
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Withdraw result denied for event {EventId}", id);
            return Forbid();
        }
        catch (StaleRevisionException ex)
        {
            _logger.LogWarning("Withdraw result conflict for event {EventId}: {Message}", id, ex.Message);
            return Conflict(new { message = ex.Message, currentRevision = ex.CurrentRevision });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Withdraw result refused for event {EventId}: {Message}", id, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Add a game that was played but never existed in the app, with its roster,
    /// sending nothing to anyone (admin of the organization). See PastGameService.
    /// </summary>
    [HttpPost("organizations/{id:guid}/past-games")]
    public async Task<ActionResult<CreatePastGameResponse>> CreatePastGame(Guid id, [FromBody] CreatePastGameRequest request)
    {
        try
        {
            var result = await _pastGames.CreatePastGameAsync(id, request, GetCurrentUserId());
            return result == null ? NotFound(new { message = "Organization not found" }) : Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Past game denied for organization {OrganizationId}", id);
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Past game refused for organization {OrganizationId}: {Message}", id, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    // ------------------------------------------------------------------- reads

    /// <summary>The Results tab. Same visibility as the event; 404 when nothing is published.</summary>
    [HttpGet("events/{id:guid}/result")]
    public async Task<ActionResult<GameResultDto>> GetResult(Guid id)
    {
        var result = await _results.GetResultAsync(id, GetCurrentUserId());
        if (result == null)
        {
            _logger.LogWarning("No visible result for event {EventId}", id);
            return NotFound(new { message = "No result for this event" });
        }
        return Ok(result);
    }

    /// <summary>A player's season line, career line and game log (?season=2026).</summary>
    [HttpGet("users/{id:guid}/stats")]
    public async Task<ActionResult<PlayerStatsDto>> GetPlayerStats(Guid id, [FromQuery] string? season)
    {
        try
        {
            var result = await _results.GetPlayerStatsAsync(id, season, GetCurrentUserId());
            if (result == null)
            {
                _logger.LogWarning("No visible stats for user {UserId}", id);
                return NotFound(new { message = "No stats for this player" });
            }
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Player stats refused for {UserId}: {Message}", id, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Whether my stats are hidden from other players.</summary>
    [HttpGet("users/me/stats-visibility")]
    public async Task<ActionResult<StatsVisibilityDto>> GetStatsVisibility()
    {
        var result = await _results.GetStatsVisibilityAsync(GetCurrentUserId());
        if (result == null)
        {
            _logger.LogWarning("Stats visibility: caller not found");
            return NotFound(new { message = "User not found" });
        }
        return Ok(result);
    }

    /// <summary>Hide or show my stats to other players.</summary>
    [HttpPut("users/me/stats-visibility")]
    public async Task<ActionResult<StatsVisibilityDto>> SetStatsVisibility([FromBody] StatsVisibilityDto request)
    {
        var result = await _results.SetStatsVisibilityAsync(GetCurrentUserId(), request.Hidden);
        if (result == null)
        {
            _logger.LogWarning("Set stats visibility: caller not found");
            return NotFound(new { message = "User not found" });
        }
        return Ok(result);
    }

    /// <summary>The organization's leaders for a season (members only).</summary>
    [HttpGet("organizations/{id:guid}/leaderboard")]
    public async Task<ActionResult<LeaderboardDto>> GetLeaderboard(Guid id, [FromQuery] string? season, [FromQuery] string? stat)
    {
        try
        {
            var result = await _results.GetLeaderboardAsync(id, season, stat, GetCurrentUserId());
            if (result == null)
            {
                _logger.LogWarning("No leaderboard for organization {OrganizationId}", id);
                return NotFound(new { message = "No leaderboard" });
            }
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Leaderboard refused for organization {OrganizationId}: {Message}", id, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>The organization's most recent published results (members only).</summary>
    [HttpGet("organizations/{id:guid}/results")]
    public async Task<ActionResult<List<GameResultSummaryDto>>> GetRecentResults(Guid id, [FromQuery] int limit = 5)
    {
        try
        {
            var result = await _results.GetRecentResultsAsync(id, limit, GetCurrentUserId());
            if (result == null)
            {
                _logger.LogWarning("No results for organization {OrganizationId}", id);
                return NotFound(new { message = "No results" });
            }
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }
}
