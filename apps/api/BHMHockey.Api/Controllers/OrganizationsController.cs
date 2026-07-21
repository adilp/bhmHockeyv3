using System.Security.Claims;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BHMHockey.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrganizationsController : ControllerBase
{
    private readonly IOrganizationService _organizationService;
    private readonly IOrganizationAdminService _adminService;
    private readonly IOrganizationAutoRosterService _autoRosterService;
    private readonly IOrganizationWaiverService _waiverService;
    private readonly ILogger<OrganizationsController> _logger;

    public OrganizationsController(
        IOrganizationService organizationService,
        IOrganizationAdminService adminService,
        IOrganizationAutoRosterService autoRosterService,
        IOrganizationWaiverService waiverService,
        ILogger<OrganizationsController> logger)
    {
        _organizationService = organizationService;
        _adminService = adminService;
        _autoRosterService = autoRosterService;
        _waiverService = waiverService;
        _logger = logger;
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

    private string GetCurrentUserRole()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value ?? "Player";
    }

    /// <summary>
    /// Get all organizations. If authenticated, includes subscription status.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<OrganizationDto>>> GetAll()
    {
        var userId = GetCurrentUserIdOrNull();
        var organizations = await _organizationService.GetAllAsync(userId);
        return Ok(organizations);
    }

    /// <summary>
    /// Get organization by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrganizationDto>> GetById(Guid id)
    {
        var userId = GetCurrentUserIdOrNull();
        var organization = await _organizationService.GetByIdAsync(id, userId);

        if (organization == null)
        {
            return NotFound();
        }

        return Ok(organization);
    }

    /// <summary>
    /// Create a new organization. Requires authentication and Organizer/Admin role.
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<OrganizationDto>> Create([FromBody] CreateOrganizationRequest request)
    {
        if (GetCurrentUserRole() == "Player")
        {
            return Forbid();
        }

        var userId = GetCurrentUserId();
        try
        {
            var organization = await _organizationService.CreateAsync(request, userId);
            return CreatedAtAction(nameof(GetById), new { id = organization.Id }, organization);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Organization creation rejected for user {UserId}: {Message}", userId, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Update an organization. Only admins can update.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<OrganizationDto>> Update(Guid id, [FromBody] UpdateOrganizationRequest request)
    {
        var userId = GetCurrentUserId();
        try
        {
            var organization = await _organizationService.UpdateAsync(id, request, userId);

            if (organization == null)
            {
                return NotFound();
            }

            return Ok(organization);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Organization update rejected for organization {OrganizationId}: {Message}", id, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Delete an organization. Only admins can delete.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetCurrentUserId();
        var deleted = await _organizationService.DeleteAsync(id, userId);

        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }

    /// <summary>
    /// Subscribe to an organization. Requires authentication.
    /// </summary>
    [HttpPost("{id:guid}/subscribe")]
    [Authorize]
    public async Task<IActionResult> Subscribe(Guid id)
    {
        var userId = GetCurrentUserId();
        var success = await _organizationService.SubscribeAsync(id, userId);

        if (!success)
        {
            return BadRequest(new { message = "Already subscribed to this organization" });
        }

        return Ok(new { message = "Successfully subscribed" });
    }

    /// <summary>
    /// Unsubscribe from an organization. Requires authentication.
    /// </summary>
    [HttpDelete("{id:guid}/subscribe")]
    [Authorize]
    public async Task<IActionResult> Unsubscribe(Guid id)
    {
        var userId = GetCurrentUserId();
        var success = await _organizationService.UnsubscribeAsync(id, userId);

        if (!success)
        {
            return NotFound(new { message = "Not subscribed to this organization" });
        }

        return NoContent();
    }

    /// <summary>
    /// Get all members (subscribers) of an organization. Only admins can access.
    /// </summary>
    [HttpGet("{id:guid}/members")]
    [Authorize]
    public async Task<ActionResult<List<OrganizationMemberDto>>> GetMembers(Guid id)
    {
        var userId = GetCurrentUserId();
        var members = await _organizationService.GetMembersAsync(id, userId);
        return Ok(members);
    }

    /// <summary>
    /// Remove a member from an organization. Only admins can remove members.
    /// </summary>
    [HttpDelete("{id:guid}/members/{memberUserId:guid}")]
    [Authorize]
    public async Task<IActionResult> RemoveMember(Guid id, Guid memberUserId)
    {
        var userId = GetCurrentUserId();
        var success = await _organizationService.RemoveMemberAsync(id, memberUserId, userId);

        if (!success)
        {
            return BadRequest(new { message = "Failed to remove member. Member may not exist or you may not have permission." });
        }

        return NoContent();
    }

    /// <summary>
    /// Leave an organization: unsubscribe AND cancel upcoming registrations in the
    /// org's events (waitlist promotions fire as usual). Requires authentication.
    /// </summary>
    [HttpPost("{id:guid}/leave")]
    [Authorize]
    public async Task<IActionResult> Leave(Guid id)
    {
        var userId = GetCurrentUserId();
        await _organizationService.LeaveAsync(id, userId);
        return Ok(new { message = "You have left the organization" });
    }

    // Waiver endpoints

    /// <summary>
    /// Get the organization's current active waiver. 404 when no active waiver.
    /// </summary>
    [HttpGet("{id:guid}/waiver")]
    [Authorize]
    public async Task<ActionResult<OrganizationWaiverDto>> GetWaiver(Guid id)
    {
        var waiver = await _waiverService.GetCurrentWaiverAsync(id);

        if (waiver == null)
        {
            return NotFound(new { message = "This organization has no active waiver" });
        }

        return Ok(waiver);
    }

    /// <summary>
    /// Set the organization's waiver (admin only). Creates the next immutable
    /// version from the submitted text; empty text deactivates the waiver.
    /// </summary>
    [HttpPut("{id:guid}/waiver")]
    [Authorize]
    public async Task<ActionResult<SetOrganizationWaiverResponse>> SetWaiver(Guid id, [FromBody] SetOrganizationWaiverRequest request)
    {
        var userId = GetCurrentUserId();

        try
        {
            var waiver = await _waiverService.SetWaiverAsync(id, request.Text, userId);
            return Ok(new SetOrganizationWaiverResponse(waiver));
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Set waiver denied for organization {OrganizationId}: requester is not an admin", id);
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Set waiver rejected for organization {OrganizationId}: {Message}", id, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Render the organization's current waiver as a PDF. Public by design -
    /// waivers aren't secrets and users save/share them outside the app.
    /// 404 when no active waiver.
    /// </summary>
    [HttpGet("{id:guid}/waiver/pdf")]
    [AllowAnonymous]
    public async Task<IActionResult> GetWaiverPdf(Guid id)
    {
        var pdf = await _waiverService.GetCurrentWaiverPdfAsync(id);

        if (pdf == null)
        {
            _logger.LogWarning("Waiver PDF requested for organization {OrganizationId} with no active waiver", id);
            return NotFound(new { message = "This organization has no active waiver" });
        }

        return File(pdf.Value.Content, "application/pdf", pdf.Value.FileName);
    }

    /// <summary>
    /// Accept a SPECIFIC waiver version, recording the signature fields on the
    /// acceptance row. 400 when the id is not the org's current active version
    /// (stale-version protection), when the participant name/date is missing,
    /// or when the Parent/Guardian section is partially filled. Idempotent when
    /// already accepted (the original signature fields are preserved).
    /// </summary>
    [HttpPost("{id:guid}/waiver/accept")]
    [Authorize]
    public async Task<IActionResult> AcceptWaiver(Guid id, [FromBody] AcceptWaiverRequest request)
    {
        var userId = GetCurrentUserId();

        try
        {
            await _waiverService.AcceptWaiverAsync(id, request, userId);
            return Ok(new { message = "Waiver accepted" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Waiver acceptance rejected for organization {OrganizationId}: {Message}", id, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    // Admin management endpoints

    /// <summary>
    /// Get all admins of an organization. Only admins can access.
    /// </summary>
    [HttpGet("{id:guid}/admins")]
    [Authorize]
    public async Task<ActionResult<List<OrganizationAdminDto>>> GetAdmins(Guid id)
    {
        var userId = GetCurrentUserId();
        var admins = await _adminService.GetAdminsAsync(id, userId);
        return Ok(admins);
    }

    /// <summary>
    /// Add an admin to an organization. Only existing admins can add new admins.
    /// </summary>
    [HttpPost("{id:guid}/admins")]
    [Authorize]
    public async Task<IActionResult> AddAdmin(Guid id, [FromBody] AddAdminRequest request)
    {
        var userId = GetCurrentUserId();
        var success = await _adminService.AddAdminAsync(id, request.UserId, userId);

        if (!success)
        {
            return BadRequest(new { message = "Failed to add admin. User may already be an admin or you may not have permission." });
        }

        return Ok(new { message = "Successfully added admin" });
    }

    /// <summary>
    /// Remove an admin from an organization. Only admins can remove other admins.
    /// Cannot remove the last admin.
    /// </summary>
    [HttpDelete("{id:guid}/admins/{adminUserId:guid}")]
    [Authorize]
    public async Task<IActionResult> RemoveAdmin(Guid id, Guid adminUserId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var success = await _adminService.RemoveAdminAsync(id, adminUserId, userId);

            if (!success)
            {
                return BadRequest(new { message = "Failed to remove admin. User may not be an admin or you may not have permission." });
            }

            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // Auto-roster endpoints - org "regulars" auto-added to new org events

    /// <summary>
    /// Get the organization's auto-roster list ordered by sort order. Only admins can access.
    /// </summary>
    [HttpGet("{id:guid}/auto-roster")]
    [Authorize]
    public async Task<ActionResult<List<AutoRosterMemberDto>>> GetAutoRoster(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            var members = await _autoRosterService.GetAutoRosterAsync(id, userId);
            return Ok(members);
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Get auto-roster denied for organization {OrganizationId}: requester is not an admin", id);
            return Forbid();
        }
    }

    /// <summary>
    /// Add a subscriber to the organization's auto-roster. Only admins can add.
    /// </summary>
    [HttpPost("{id:guid}/auto-roster")]
    [Authorize]
    public async Task<ActionResult<AutoRosterMemberDto>> AddAutoRosterMember(Guid id, [FromBody] AddAutoRosterMemberRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var member = await _autoRosterService.AddMemberAsync(id, request.UserId, request.Position, userId);
            return Ok(member);
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Add auto-roster member denied for organization {OrganizationId}: requester is not an admin", id);
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Add auto-roster member rejected for organization {OrganizationId}: {Message}", id, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Remove a user from the organization's auto-roster. Only admins can remove.
    /// </summary>
    [HttpDelete("{id:guid}/auto-roster/{memberUserId:guid}")]
    [Authorize]
    public async Task<IActionResult> RemoveAutoRosterMember(Guid id, Guid memberUserId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var removed = await _autoRosterService.RemoveMemberAsync(id, memberUserId, userId);

            if (!removed)
            {
                _logger.LogWarning("Remove auto-roster member failed for organization {OrganizationId}: user {MemberUserId} not in list", id, memberUserId);
                return NotFound(new { message = "User is not in the auto-roster" });
            }

            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Remove auto-roster member denied for organization {OrganizationId}: requester is not an admin", id);
            return Forbid();
        }
    }

    /// <summary>
    /// Reorder the organization's auto-roster. All current members must be included. Only admins can reorder.
    /// </summary>
    [HttpPut("{id:guid}/auto-roster/order")]
    [Authorize]
    public async Task<ActionResult<List<AutoRosterMemberDto>>> ReorderAutoRoster(Guid id, [FromBody] ReorderAutoRosterRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var members = await _autoRosterService.ReorderAsync(id, request.OrderedUserIds, userId);
            return Ok(members);
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Reorder auto-roster denied for organization {OrganizationId}: requester is not an admin", id);
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Reorder auto-roster rejected for organization {OrganizationId}: {Message}", id, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
    }
}
