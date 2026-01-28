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
    private readonly IAuthService _authService;

    public OrganizationsController(IOrganizationService organizationService, IOrganizationAdminService adminService, IAuthService authService)
    {
        _organizationService = organizationService;
        _adminService = adminService;
        _authService = authService;
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

    private async Task<string> GetCurrentUserRoleAsync()
    {
        var userId = GetCurrentUserId();
        return await _authService.GetUserRoleAsync(userId);
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
        var role = await GetCurrentUserRoleAsync();
        if (role == "Player")
        {
            return Forbid();
        }

        var userId = GetCurrentUserId();
        var organization = await _organizationService.CreateAsync(request, userId);
        return CreatedAtAction(nameof(GetById), new { id = organization.Id }, organization);
    }

    /// <summary>
    /// Update an organization. Only admins can update.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<ActionResult<OrganizationDto>> Update(Guid id, [FromBody] UpdateOrganizationRequest request)
    {
        var userId = GetCurrentUserId();
        var organization = await _organizationService.UpdateAsync(id, request, userId);

        if (organization == null)
        {
            return NotFound();
        }

        return Ok(organization);
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
}
