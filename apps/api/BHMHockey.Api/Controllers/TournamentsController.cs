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
    private readonly ITournamentLifecycleService _lifecycleService;
    private readonly ITournamentTeamService _teamService;
    private readonly ITournamentMatchService _matchService;
    private readonly IBracketGenerationService _bracketGenerationService;
    private readonly ITournamentRegistrationService _registrationService;
    private readonly ITournamentTeamAssignmentService _teamAssignmentService;
    private readonly ITournamentTeamMemberService _teamMemberService;

    public TournamentsController(
        ITournamentService tournamentService,
        ITournamentLifecycleService lifecycleService,
        ITournamentTeamService teamService,
        ITournamentMatchService matchService,
        IBracketGenerationService bracketGenerationService,
        ITournamentRegistrationService registrationService,
        ITournamentTeamAssignmentService teamAssignmentService,
        ITournamentTeamMemberService teamMemberService)
    {
        _tournamentService = tournamentService;
        _lifecycleService = lifecycleService;
        _teamService = teamService;
        _matchService = matchService;
        _bracketGenerationService = bracketGenerationService;
        _registrationService = registrationService;
        _teamAssignmentService = teamAssignmentService;
        _teamMemberService = teamMemberService;
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

    #region Lifecycle Endpoints

    /// <summary>
    /// Publish a tournament (Draft → Open). Makes it visible and opens registration.
    /// </summary>
    /// <param name="id">Tournament ID</param>
    /// <response code="200">Tournament published successfully</response>
    /// <response code="400">Invalid state transition</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized to manage this tournament</response>
    [HttpPost("{id:guid}/publish")]
    [Authorize]
    [ProducesResponseType(typeof(TournamentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TournamentDto>> Publish(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _lifecycleService.PublishAsync(id, userId);
            return Ok(result);
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
    /// Close registration for a tournament (Open → RegistrationClosed).
    /// </summary>
    /// <param name="id">Tournament ID</param>
    /// <response code="200">Registration closed successfully</response>
    /// <response code="400">Invalid state transition</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized to manage this tournament</response>
    [HttpPost("{id:guid}/close-registration")]
    [Authorize]
    [ProducesResponseType(typeof(TournamentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TournamentDto>> CloseRegistration(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _lifecycleService.CloseRegistrationAsync(id, userId);
            return Ok(result);
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
    /// Start a tournament (RegistrationClosed → InProgress). Locks the bracket.
    /// </summary>
    /// <param name="id">Tournament ID</param>
    /// <response code="200">Tournament started successfully</response>
    /// <response code="400">Invalid state transition</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized to manage this tournament</response>
    [HttpPost("{id:guid}/start")]
    [Authorize]
    [ProducesResponseType(typeof(TournamentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TournamentDto>> Start(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _lifecycleService.StartAsync(id, userId);
            return Ok(result);
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
    /// Complete a tournament (InProgress → Completed). Terminal state.
    /// </summary>
    /// <param name="id">Tournament ID</param>
    /// <response code="200">Tournament completed successfully</response>
    /// <response code="400">Invalid state transition</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized to manage this tournament</response>
    [HttpPost("{id:guid}/complete")]
    [Authorize]
    [ProducesResponseType(typeof(TournamentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TournamentDto>> Complete(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _lifecycleService.CompleteAsync(id, userId);
            return Ok(result);
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
    /// Postpone a tournament (InProgress → Postponed). Optionally set new dates.
    /// </summary>
    /// <param name="id">Tournament ID</param>
    /// <param name="request">Optional new start and end dates</param>
    /// <response code="200">Tournament postponed successfully</response>
    /// <response code="400">Invalid state transition</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized to manage this tournament</response>
    [HttpPost("{id:guid}/postpone")]
    [Authorize]
    [ProducesResponseType(typeof(TournamentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TournamentDto>> Postpone(Guid id, [FromBody] PostponeTournamentRequest? request = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _lifecycleService.PostponeAsync(id, userId, request?.NewStartDate, request?.NewEndDate);
            return Ok(result);
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
    /// Resume a postponed tournament (Postponed → InProgress).
    /// </summary>
    /// <param name="id">Tournament ID</param>
    /// <response code="200">Tournament resumed successfully</response>
    /// <response code="400">Invalid state transition</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized to manage this tournament</response>
    [HttpPost("{id:guid}/resume")]
    [Authorize]
    [ProducesResponseType(typeof(TournamentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TournamentDto>> Resume(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _lifecycleService.ResumeAsync(id, userId);
            return Ok(result);
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
    /// Cancel a tournament (Any non-terminal state → Cancelled). Terminal state.
    /// </summary>
    /// <param name="id">Tournament ID</param>
    /// <response code="200">Tournament cancelled successfully</response>
    /// <response code="400">Invalid state transition (e.g., already completed)</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized to manage this tournament</response>
    [HttpPost("{id:guid}/cancel")]
    [Authorize]
    [ProducesResponseType(typeof(TournamentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TournamentDto>> Cancel(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _lifecycleService.CancelAsync(id, userId);
            return Ok(result);
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

    #endregion

    #region Team Endpoints

    /// <summary>
    /// Get all teams for a tournament.
    /// </summary>
    /// <param name="id">Tournament ID</param>
    /// <response code="200">Returns list of teams</response>
    [HttpGet("{id:guid}/teams")]
    [ProducesResponseType(typeof(List<TournamentTeamDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TournamentTeamDto>>> GetTeams(Guid id)
    {
        var teams = await _teamService.GetAllAsync(id);
        return Ok(teams);
    }

    /// <summary>
    /// Get a team by ID.
    /// </summary>
    /// <param name="id">Tournament ID</param>
    /// <param name="teamId">Team ID</param>
    /// <response code="200">Returns the team</response>
    /// <response code="404">Team not found</response>
    [HttpGet("{id:guid}/teams/{teamId:guid}")]
    [ProducesResponseType(typeof(TournamentTeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TournamentTeamDto>> GetTeamById(Guid id, Guid teamId)
    {
        var team = await _teamService.GetByIdAsync(id, teamId);

        if (team == null)
        {
            return NotFound();
        }

        return Ok(team);
    }

    /// <summary>
    /// Create a new team. Requires authentication and tournament admin role.
    /// </summary>
    /// <param name="id">Tournament ID</param>
    /// <param name="request">Team creation request</param>
    /// <response code="201">Team created successfully</response>
    /// <response code="400">Invalid request data</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized to manage this tournament</response>
    [HttpPost("{id:guid}/teams")]
    [Authorize]
    [ProducesResponseType(typeof(TournamentTeamDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TournamentTeamDto>> CreateTeam(Guid id, [FromBody] CreateTournamentTeamRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var team = await _teamService.CreateAsync(id, request, userId);
            return CreatedAtAction(nameof(GetTeamById), new { id, teamId = team.Id }, team);
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
    /// Update a team. Requires authentication and tournament admin role.
    /// </summary>
    /// <param name="id">Tournament ID</param>
    /// <param name="teamId">Team ID</param>
    /// <param name="request">Update request</param>
    /// <response code="200">Team updated successfully</response>
    /// <response code="400">Invalid request data</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized to manage this tournament</response>
    /// <response code="404">Team not found</response>
    [HttpPut("{id:guid}/teams/{teamId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(TournamentTeamDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TournamentTeamDto>> UpdateTeam(Guid id, Guid teamId, [FromBody] UpdateTournamentTeamRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var team = await _teamService.UpdateAsync(id, teamId, request, userId);

            if (team == null)
            {
                return NotFound();
            }

            return Ok(team);
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
    /// Delete a team. Requires authentication and tournament admin role.
    /// </summary>
    /// <param name="id">Tournament ID</param>
    /// <param name="teamId">Team ID</param>
    /// <response code="204">Team deleted successfully</response>
    /// <response code="400">Invalid operation</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized to manage this tournament</response>
    /// <response code="404">Team not found</response>
    [HttpDelete("{id:guid}/teams/{teamId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteTeam(Guid id, Guid teamId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var deleted = await _teamService.DeleteAsync(id, teamId, userId);

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

    /// <summary>
    /// Add a player to a team. Requires authentication and tournament admin role.
    /// </summary>
    /// <remarks>
    /// Adds a player to the team with Pending status. Player receives an invitation
    /// and can accept or decline via the respond endpoint.
    /// </remarks>
    /// <param name="id">Tournament ID</param>
    /// <param name="teamId">Team ID</param>
    /// <param name="request">Request containing the UserId to add</param>
    /// <response code="201">Player added successfully</response>
    /// <response code="400">Invalid request (e.g., team full, player already on another team)</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized to manage this tournament</response>
    [HttpPost("{id:guid}/teams/{teamId:guid}/members")]
    [Authorize]
    [ProducesResponseType(typeof(TournamentTeamMemberDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TournamentTeamMemberDto>> AddTeamMember(
        Guid id,
        Guid teamId,
        [FromBody] AddTeamMemberRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _teamMemberService.AddPlayerAsync(id, teamId, request.UserId, userId);
            return CreatedAtAction(nameof(GetTeamMembers), new { id, teamId }, result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Get all members of a team.
    /// </summary>
    /// <param name="id">Tournament ID</param>
    /// <param name="teamId">Team ID</param>
    /// <response code="200">Returns list of team members</response>
    [HttpGet("{id:guid}/teams/{teamId:guid}/members")]
    [Authorize]
    [ProducesResponseType(typeof(List<TournamentTeamMemberDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<TournamentTeamMemberDto>>> GetTeamMembers(Guid id, Guid teamId)
    {
        var members = await _teamMemberService.GetTeamMembersAsync(id, teamId);
        return Ok(members);
    }

    /// <summary>
    /// Remove a player from a team. Requires authentication and tournament admin role.
    /// </summary>
    /// <param name="id">Tournament ID</param>
    /// <param name="teamId">Team ID</param>
    /// <param name="userId">User ID to remove</param>
    /// <response code="204">Player removed successfully</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized to manage this tournament</response>
    /// <response code="404">Player not found on team</response>
    [HttpDelete("{id:guid}/teams/{teamId:guid}/members/{userId:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveTeamMember(Guid id, Guid teamId, Guid userId)
    {
        try
        {
            var adminUserId = GetCurrentUserId();
            var removed = await _teamMemberService.RemovePlayerAsync(id, teamId, userId, adminUserId);

            if (!removed)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    /// <summary>
    /// Player responds to team invitation. Requires authentication.
    /// </summary>
    /// <remarks>
    /// Allows a player to accept or decline their team invitation.
    /// If accepted, position is required ("Goalie" or "Skater") and a TournamentRegistration is created.
    /// </remarks>
    /// <param name="id">Tournament ID</param>
    /// <param name="teamId">Team ID</param>
    /// <param name="request">Response with Accept flag, optional Position and CustomResponses</param>
    /// <response code="200">Response recorded successfully</response>
    /// <response code="400">Invalid request (e.g., missing position, already responded)</response>
    /// <response code="401">Not authenticated</response>
    [HttpPost("{id:guid}/teams/{teamId:guid}/members/respond")]
    [Authorize]
    [ProducesResponseType(typeof(TournamentTeamMemberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TournamentTeamMemberDto>> RespondToTeamInvite(
        Guid id,
        Guid teamId,
        [FromBody] RespondToTeamInviteRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _teamMemberService.RespondAsync(
                id,
                teamId,
                userId,
                request.Accept,
                request.Position,
                request.CustomResponses);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Transfer team captaincy to another team member. Requires authentication.
    /// Only the current captain can transfer captaincy.
    /// </summary>
    /// <remarks>
    /// The new captain must be an existing team member with Status = Accepted.
    /// The old captain becomes a regular Player on the team.
    /// </remarks>
    /// <param name="id">Tournament ID</param>
    /// <param name="teamId">Team ID</param>
    /// <param name="request">Request containing the new captain's user ID</param>
    /// <response code="200">Captain transferred successfully</response>
    /// <response code="400">Invalid request (e.g., new captain not accepted member)</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not the current captain</response>
    [HttpPost("{id:guid}/teams/{teamId:guid}/transfer-captain")]
    [Authorize]
    [ProducesResponseType(typeof(TransferCaptainResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TransferCaptainResponse>> TransferCaptain(
        Guid id,
        Guid teamId,
        [FromBody] TransferCaptainRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _teamMemberService.TransferCaptainAsync(id, teamId, request.NewCaptainUserId, userId);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Search for users to add to a team. Requires authentication and captain or admin role.
    /// </summary>
    /// <remarks>
    /// Allows team captains and tournament admins to search for users by email or name.
    /// Results exclude users already on the team and are limited to 20 users.
    /// </remarks>
    /// <param name="id">Tournament ID</param>
    /// <param name="teamId">Team ID</param>
    /// <param name="query">Search query (email or name, partial match)</param>
    /// <response code="200">Returns list of matching users</response>
    /// <response code="400">Invalid query (too short)</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized to search users for this team</response>
    [HttpPost("{id:guid}/teams/{teamId:guid}/search-users")]
    [Authorize]
    [ProducesResponseType(typeof(List<UserSearchResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<UserSearchResultDto>>> SearchUsers(
        Guid id,
        Guid teamId,
        [FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            return BadRequest(new { message = "Search query must be at least 2 characters" });
        }

        try
        {
            var userId = GetCurrentUserId();
            var results = await _teamMemberService.SearchUsersAsync(id, teamId, query, userId);
            return Ok(results);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    #endregion

    #region Match Endpoints

    /// <summary>
    /// Get all matches for a tournament.
    /// </summary>
    /// <param name="id">Tournament ID</param>
    /// <response code="200">Returns list of matches</response>
    [HttpGet("{id:guid}/matches")]
    [ProducesResponseType(typeof(List<TournamentMatchDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<TournamentMatchDto>>> GetMatches(Guid id)
    {
        var matches = await _matchService.GetAllAsync(id);
        return Ok(matches);
    }

    /// <summary>
    /// Get a match by ID.
    /// </summary>
    /// <param name="id">Tournament ID</param>
    /// <param name="matchId">Match ID</param>
    /// <response code="200">Returns the match</response>
    /// <response code="404">Match not found</response>
    [HttpGet("{id:guid}/matches/{matchId:guid}")]
    [ProducesResponseType(typeof(TournamentMatchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TournamentMatchDto>> GetMatchById(Guid id, Guid matchId)
    {
        var match = await _matchService.GetByIdAsync(id, matchId);

        if (match == null)
        {
            return NotFound();
        }

        return Ok(match);
    }

    /// <summary>
    /// Enter or update the score for a match. Requires authentication and tournament admin role.
    /// </summary>
    /// <remarks>
    /// Updates match scores, determines winner, advances winner to next match in bracket,
    /// and updates team statistics. For tied scores in elimination formats, OvertimeWinnerId is required.
    /// </remarks>
    /// <param name="id">Tournament ID</param>
    /// <param name="matchId">Match ID</param>
    /// <param name="request">Score entry request</param>
    /// <response code="200">Score entered successfully</response>
    /// <response code="400">Invalid request (e.g., tied score without overtime winner in elimination)</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized to manage this tournament</response>
    /// <response code="404">Match not found</response>
    [HttpPut("{id:guid}/matches/{matchId:guid}/score")]
    [Authorize]
    [ProducesResponseType(typeof(TournamentMatchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TournamentMatchDto>> EnterScore(Guid id, Guid matchId, [FromBody] EnterScoreRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var match = await _matchService.EnterScoreAsync(id, matchId, request, userId);
            return Ok(match);
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
    /// Record a forfeit for a match. Requires authentication and tournament admin role.
    /// </summary>
    /// <remarks>
    /// Sets the non-forfeiting team as winner, updates team statistics (win/loss),
    /// advances winner to next match in bracket, and sets forfeiting team status to Eliminated
    /// in elimination formats.
    /// </remarks>
    /// <param name="id">Tournament ID</param>
    /// <param name="matchId">Match ID</param>
    /// <param name="request">Forfeit request with forfeiting team ID and reason</param>
    /// <response code="200">Forfeit recorded successfully</response>
    /// <response code="400">Invalid request (e.g., forfeiting team not in match)</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized to manage this tournament</response>
    /// <response code="404">Match not found</response>
    [HttpPost("{id:guid}/matches/{matchId:guid}/forfeit")]
    [Authorize]
    [ProducesResponseType(typeof(TournamentMatchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TournamentMatchDto>> ForfeitMatch(Guid id, Guid matchId, [FromBody] ForfeitMatchRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var match = await _matchService.ForfeitMatchAsync(id, matchId, request, userId);
            return Ok(match);
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

    #endregion

    #region Bracket Generation

    /// <summary>
    /// Generates a single elimination bracket for the tournament.
    /// Creates all matches with proper seeding and bye handling.
    /// </summary>
    [HttpPost("{id}/generate-bracket")]
    [Authorize]
    public async Task<ActionResult<List<TournamentMatchDto>>> GenerateBracket(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var matches = await _bracketGenerationService.GenerateSingleEliminationBracketAsync(id, userId);
            return Ok(matches);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Clears all matches from the tournament bracket.
    /// Use this before regenerating a bracket.
    /// </summary>
    [HttpDelete("{id}/bracket")]
    [Authorize]
    public async Task<ActionResult> ClearBracket(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _bracketGenerationService.ClearBracketAsync(id, userId);
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    #endregion

    #region Registration Endpoints

    /// <summary>
    /// Register for a tournament. Requires authentication.
    /// </summary>
    /// <param name="id">Tournament ID</param>
    /// <param name="request">Registration request with position and optional custom responses</param>
    /// <response code="201">Registration created successfully</response>
    /// <response code="400">Invalid request or registration not allowed</response>
    /// <response code="401">Not authenticated</response>
    [HttpPost("{id:guid}/register")]
    [Authorize]
    [ProducesResponseType(typeof(TournamentRegistrationResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TournamentRegistrationResultDto>> Register(Guid id, [FromBody] CreateTournamentRegistrationRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _registrationService.RegisterAsync(id, request, userId);
            return StatusCode(StatusCodes.Status201Created, result);
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
    /// Get current user's registration for a tournament. Requires authentication.
    /// </summary>
    /// <param name="id">Tournament ID</param>
    /// <response code="200">Returns the registration</response>
    /// <response code="404">Not registered for this tournament</response>
    /// <response code="401">Not authenticated</response>
    [HttpGet("{id:guid}/register")]
    [Authorize]
    [ProducesResponseType(typeof(TournamentRegistrationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TournamentRegistrationDto>> GetMyRegistration(Guid id)
    {
        var userId = GetCurrentUserId();
        var registration = await _registrationService.GetMyRegistrationAsync(id, userId);

        if (registration == null)
        {
            return NotFound();
        }

        return Ok(registration);
    }

    /// <summary>
    /// Update current user's registration. Requires authentication.
    /// </summary>
    /// <param name="id">Tournament ID</param>
    /// <param name="request">Update request with optional position and custom responses</param>
    /// <response code="200">Registration updated successfully</response>
    /// <response code="400">Invalid request or update not allowed</response>
    /// <response code="404">Not registered for this tournament</response>
    /// <response code="401">Not authenticated</response>
    [HttpPut("{id:guid}/register")]
    [Authorize]
    [ProducesResponseType(typeof(TournamentRegistrationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TournamentRegistrationDto>> UpdateMyRegistration(Guid id, [FromBody] UpdateTournamentRegistrationRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var registration = await _registrationService.UpdateAsync(id, request, userId);

            if (registration == null)
            {
                return NotFound();
            }

            return Ok(registration);
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
    /// Withdraw registration (self-service). Requires authentication.
    /// </summary>
    /// <param name="id">Tournament ID</param>
    /// <response code="204">Registration withdrawn successfully</response>
    /// <response code="404">Not registered for this tournament</response>
    /// <response code="401">Not authenticated</response>
    [HttpDelete("{id:guid}/register")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> WithdrawRegistration(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var withdrawn = await _registrationService.WithdrawAsync(id, userId);

            if (!withdrawn)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    /// <summary>
    /// List all registrations for a tournament. Admin only.
    /// </summary>
    /// <param name="id">Tournament ID</param>
    /// <response code="200">Returns list of all registrations</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized to view registrations</response>
    [HttpGet("{id:guid}/registrations")]
    [Authorize]
    [ProducesResponseType(typeof(List<TournamentRegistrationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<TournamentRegistrationDto>>> GetAllRegistrations(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var registrations = await _registrationService.GetAllAsync(id, userId);
            return Ok(registrations);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    /// <summary>
    /// Mark payment as complete for current user's registration. Requires authentication.
    /// </summary>
    /// <remarks>
    /// Players call this endpoint after completing their payment (e.g., via Venmo).
    /// This transitions PaymentStatus from Pending to MarkedPaid.
    /// The organizer must then verify the payment.
    /// </remarks>
    /// <param name="id">Tournament ID</param>
    /// <response code="200">Payment marked successfully</response>
    /// <response code="400">Cannot mark payment (not pending, free tournament, or not registered)</response>
    /// <response code="401">Not authenticated</response>
    [HttpPost("{id:guid}/register/mark-payment")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> MarkPayment(Guid id)
    {
        var userId = GetCurrentUserId();
        var result = await _registrationService.MarkPaymentAsync(id, userId);

        if (!result)
        {
            return BadRequest(new { message = "Cannot mark payment. Registration may not exist, payment already marked, or tournament is free." });
        }

        return Ok(new { message = "Payment marked successfully. Awaiting verification from organizer." });
    }

    /// <summary>
    /// Verify or reject a registration's payment. Admin only.
    /// </summary>
    /// <remarks>
    /// Tournament admins use this to verify that a player's payment has been received.
    /// Set verified=true to confirm payment, or verified=false to reset to pending status.
    /// </remarks>
    /// <param name="id">Tournament ID</param>
    /// <param name="registrationId">Registration ID to verify</param>
    /// <param name="request">Verification request with verified boolean</param>
    /// <response code="200">Payment status updated successfully</response>
    /// <response code="401">Not authenticated</response>
    /// <response code="403">Not authorized to manage this tournament</response>
    /// <response code="404">Registration not found</response>
    [HttpPut("{id:guid}/registrations/{registrationId:guid}/payment")]
    [Authorize]
    [ProducesResponseType(typeof(TournamentRegistrationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TournamentRegistrationDto>> VerifyPayment(
        Guid id,
        Guid registrationId,
        [FromBody] VerifyTournamentPaymentRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var registration = await _registrationService.VerifyPaymentAsync(id, registrationId, request.Verified, userId);

            if (registration == null)
            {
                return NotFound(new { message = "Registration not found" });
            }

            return Ok(registration);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    #endregion

    #region Team Assignment Endpoints

    /// <summary>
    /// Assign a player to a specific team. Requires tournament admin role.
    /// </summary>
    [HttpPut("{id:guid}/registrations/{registrationId:guid}/team")]
    [Authorize]
    [ProducesResponseType(typeof(TournamentRegistrationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TournamentRegistrationDto>> AssignPlayerToTeam(
        Guid id,
        Guid registrationId,
        [FromBody] AssignPlayerToTeamRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var registration = await _teamAssignmentService.AssignPlayerToTeamAsync(id, registrationId, request.TeamId, userId);

            if (registration == null)
            {
                return NotFound();
            }

            return Ok(registration);
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
    /// Auto-assign all unassigned registrations to teams. Requires tournament admin role.
    /// </summary>
    /// <remarks>
    /// Distributes goalies evenly across teams first, then assigns skaters.
    /// If balanceBySkillLevel is true, uses snake-draft by skill level (Gold > Silver > Bronze > D-League).
    /// </remarks>
    [HttpPost("{id:guid}/assign-teams")]
    [Authorize]
    [ProducesResponseType(typeof(TeamAssignmentResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<TeamAssignmentResultDto>> AutoAssignTeams(
        Guid id,
        [FromBody] AutoAssignTeamsRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _teamAssignmentService.AutoAssignTeamsAsync(id, request.BalanceBySkillLevel, userId);
            return Ok(result);
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
    /// Create multiple empty teams in bulk. Requires tournament admin role.
    /// </summary>
    /// <remarks>
    /// Creates teams with sequential names based on the provided prefix.
    /// Example: count=4, namePrefix="Team" creates "Team 1", "Team 2", "Team 3", "Team 4"
    /// </remarks>
    [HttpPost("{id:guid}/create-teams")]
    [Authorize]
    [ProducesResponseType(typeof(BulkCreateTeamsResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<BulkCreateTeamsResponse>> BulkCreateTeams(
        Guid id,
        [FromBody] BulkCreateTeamsRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var response = await _teamAssignmentService.BulkCreateTeamsAsync(id, request.Count, request.NamePrefix, userId);
            return StatusCode(StatusCodes.Status201Created, response);
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

    #endregion
}
