using System.Security.Claims;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BHMHockey.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly IEventService _eventService;

    public EventsController(IEventService eventService)
    {
        _eventService = eventService;
    }

    #region Authentication Helpers

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

    #endregion

    #region Event CRUD

    /// <summary>
    /// Get all upcoming events. Optionally filter by organization.
    /// If authenticated, includes registration status for current user.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<EventDto>>> GetAll([FromQuery] Guid? organizationId)
    {
        var currentUserId = GetCurrentUserIdOrNull();

        if (organizationId.HasValue)
        {
            var orgEvents = await _eventService.GetByOrganizationAsync(organizationId.Value, currentUserId);
            return Ok(orgEvents);
        }

        var events = await _eventService.GetAllAsync(currentUserId);
        return Ok(events);
    }

    /// <summary>
    /// Get a single event by ID.
    /// If authenticated, includes registration status for current user.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EventDto>> GetById(Guid id)
    {
        var currentUserId = GetCurrentUserIdOrNull();
        var eventDto = await _eventService.GetByIdAsync(id, currentUserId);

        if (eventDto == null)
        {
            return NotFound();
        }

        return Ok(eventDto);
    }

    /// <summary>
    /// Create a new event. Requires authentication.
    /// </summary>
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<EventDto>> Create([FromBody] CreateEventRequest request)
    {
        var userId = GetCurrentUserId();
        var eventDto = await _eventService.CreateAsync(request, userId);

        return CreatedAtAction(nameof(GetById), new { id = eventDto.Id }, eventDto);
    }

    /// <summary>
    /// Update an event. Only the creator can update.
    /// </summary>
    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EventDto>> Update(Guid id, [FromBody] UpdateEventRequest request)
    {
        var userId = GetCurrentUserId();
        var eventDto = await _eventService.UpdateAsync(id, request, userId);

        if (eventDto == null)
        {
            return NotFound();
        }

        return Ok(eventDto);
    }

    /// <summary>
    /// Cancel/delete an event. Only the creator can cancel.
    /// </summary>
    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var userId = GetCurrentUserId();
        var success = await _eventService.DeleteAsync(id, userId);

        if (!success)
        {
            return NotFound();
        }

        return NoContent();
    }

    #endregion

    #region Registration

    /// <summary>
    /// Register for an event. Requires authentication.
    /// </summary>
    [Authorize]
    [HttpPost("{id:guid}/register")]
    public async Task<IActionResult> Register(Guid id)
    {
        var userId = GetCurrentUserId();

        try
        {
            var success = await _eventService.RegisterAsync(id, userId);

            if (!success)
            {
                return BadRequest(new { message = "Already registered for this event" });
            }

            return Ok(new { message = "Successfully registered" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Cancel registration for an event. Requires authentication.
    /// </summary>
    [Authorize]
    [HttpDelete("{id:guid}/register")]
    public async Task<IActionResult> CancelRegistration(Guid id)
    {
        var userId = GetCurrentUserId();
        var success = await _eventService.CancelRegistrationAsync(id, userId);

        if (!success)
        {
            return NotFound(new { message = "Registration not found" });
        }

        return NoContent();
    }

    /// <summary>
    /// Get all registrations for an event.
    /// </summary>
    [HttpGet("{id:guid}/registrations")]
    public async Task<ActionResult<List<EventRegistrationDto>>> GetRegistrations(Guid id)
    {
        var registrations = await _eventService.GetRegistrationsAsync(id);
        return Ok(registrations);
    }

    #endregion
}
