using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BHMHockey.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TournamentsController : ControllerBase
{
    private readonly ITournamentService _tournamentService;

    public TournamentsController(ITournamentService tournamentService)
    {
        _tournamentService = tournamentService;
    }

    private Guid? GetCurrentUserIdOrNull()
    {
        if (User?.Identity?.IsAuthenticated != true)
        {
            return null;
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (userIdClaim != null && Guid.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }

        return null;
    }

    private Guid GetCurrentUserId()
    {
        var userId = GetCurrentUserIdOrNull();
        if (userId == null)
        {
            throw new UnauthorizedAccessException("User ID not found in token");
        }
        return userId.Value;
    }

    /// <summary>
    /// Get all public tournaments (Open, InProgress, Completed statuses).
    /// </summary>
    /// <remarks>
    /// Returns tournaments that are visible to the public. Draft, RegistrationClosed,
    /// Postponed, and Cancelled tournaments are not included.
    /// </remarks>
    /// <response code="200">Returns list of public tournaments</response>
    [HttpGet]
    [ProducesResponseType(typeof(List<TournamentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TournamentDto>>> GetAll()
    {
        var userId = GetCurrentUserIdOrNull();
        var tournaments = await _tournamentService.GetAllAsync(userId);
        return Ok(tournaments);
    }

    /// <summary>
    /// Get a tournament by ID.
    /// </summary>
    /// <param name="id">Tournament ID</param>
    /// <response code="200">Returns the tournament</response>
    /// <response code="404">Tournament not found</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TournamentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TournamentDto>> GetById(Guid id)
    {
        var userId = GetCurrentUserIdOrNull();
        var tournament = await _tournamentService.GetByIdAsync(id, userId);

        if (tournament == null)
        {
            return NotFound();
        }

        return Ok(tournament);
    }

    /// <summary>
    /// Create a new tournament. Requires authentication.
    /// </summary>
    /// <remarks>
    /// Creates a tournament in Draft status. The creator automatically becomes the Owner.
    /// If organizationId is provided, the creator must be an organization admin.
    /// </remarks>
    /// <param name="request">Tournament creation request</param>
    /// <response code="201">Tournament created successfully</response>
    /// <response code="400">Invalid request data</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized to create tournament for this organization</response>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(TournamentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TournamentDto>> Create([FromBody] CreateTournamentRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var tournament = await _tournamentService.CreateAsync(request, userId);
            return CreatedAtAction(nameof(GetById), new { id = tournament.Id }, tournament);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    /// <summary>
    /// Update a tournament. Requires authentication and tournament admin role.
    /// </summary>
    /// <remarks>
    /// Updates tournament settings. Only allowed when tournament is in Draft or Open status.
    /// All fields are optional - only provided fields will be updated (patch semantics).
    /// </remarks>
    /// <param name="id">Tournament ID</param>
    /// <param name="request">Update request with fields to modify</param>
    /// <response code="200">Tournament updated successfully</response>
    /// <response code="400">Invalid request or tournament not in editable status</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized to update this tournament</response>
    /// <response code="404">Tournament not found</response>
    [HttpPut("{id:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(TournamentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TournamentDto>> Update(Guid id, [FromBody] UpdateTournamentRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var tournament = await _tournamentService.UpdateAsync(id, request, userId);

            if (tournament == null)
            {
                return NotFound();
            }

            return Ok(tournament);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    /// <summary>
    /// Delete a tournament. Requires authentication and tournament admin role.
    /// </summary>
    /// <remarks>
    /// Permanently deletes a tournament. Only allowed when tournament is in Draft status.
    /// This is a hard delete - the tournament cannot be recovered.
    /// </remarks>
    /// <param name="id">Tournament ID</param>
    /// <response code="204">Tournament deleted successfully</response>
    /// <response code="400">Tournament not in Draft status</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized to delete this tournament</response>
    /// <response code="404">Tournament not found</response>
    [HttpDelete("{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var deleted = await _tournamentService.DeleteAsync(id, userId);

            if (!deleted)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }
}
