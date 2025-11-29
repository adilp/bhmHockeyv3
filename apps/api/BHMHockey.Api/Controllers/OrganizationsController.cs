using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BHMHockey.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrganizationsController : ControllerBase
{
    private readonly IOrganizationService _organizationService;

    public OrganizationsController(IOrganizationService organizationService)
    {
        _organizationService = organizationService;
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
    /// Create a new organization. Requires authentication.
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<OrganizationDto>> Create([FromBody] CreateOrganizationRequest request)
    {
        var userId = GetCurrentUserId();
        var organization = await _organizationService.CreateAsync(request, userId);
        return CreatedAtAction(nameof(GetById), new { id = organization.Id }, organization);
    }

    /// <summary>
    /// Update an organization. Only the creator can update.
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
    /// Delete an organization. Only the creator can delete.
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
    /// Get all members (subscribers) of an organization. Only creator can access.
    /// </summary>
    [HttpGet("{id:guid}/members")]
    [Authorize]
    public async Task<ActionResult<List<OrganizationMemberDto>>> GetMembers(Guid id)
    {
        var userId = GetCurrentUserId();
        var members = await _organizationService.GetMembersAsync(id, userId);
        return Ok(members);
    }
}
