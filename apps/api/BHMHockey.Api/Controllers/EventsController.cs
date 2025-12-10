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
    private readonly IWaitlistService _waitlistService;

    public EventsController(IEventService eventService, IWaitlistService waitlistService)
    {
        _eventService = eventService;
        _waitlistService = waitlistService;
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
    /// Optionally specify position if user has multiple positions in their profile.
    /// Returns registration result including waitlist status if event is full.
    /// </summary>
    [Authorize]
    [HttpPost("{id:guid}/register")]
    public async Task<ActionResult<RegistrationResultDto>> Register(Guid id, [FromBody] RegisterForEventRequest? request)
    {
        var userId = GetCurrentUserId();

        try
        {
            var result = await _eventService.RegisterAsync(id, userId, request?.Position);
            return Ok(result);
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

    /// <summary>
    /// Get waitlist for an event. Organizers only.
    /// </summary>
    [Authorize]
    [HttpGet("{id:guid}/waitlist")]
    public async Task<ActionResult<List<EventRegistrationDto>>> GetWaitlist(Guid id)
    {
        var userId = GetCurrentUserId();

        // Verify user can manage this event
        if (!await _eventService.CanUserManageEventAsync(id, userId))
        {
            return Forbid();
        }

        var waitlist = await _waitlistService.GetWaitlistAsync(id);

        var dtos = waitlist.Select(r => new EventRegistrationDto(
            r.Id,
            r.EventId,
            new UserDto(
                r.User.Id,
                r.User.Email,
                r.User.FirstName,
                r.User.LastName,
                r.User.PhoneNumber,
                r.User.Positions,
                r.User.VenmoHandle,
                r.User.Role,
                r.User.CreatedAt
            ),
            r.Status,
            r.RegisteredAt,
            r.RegisteredPosition,
            r.PaymentStatus,
            r.PaymentMarkedAt,
            r.PaymentVerifiedAt,
            r.TeamAssignment,
            r.WaitlistPosition,
            r.PromotedAt,
            r.PaymentDeadlineAt,
            r.IsWaitlisted
        )).ToList();

        return Ok(dtos);
    }

    #endregion

    #region Payment

    /// <summary>
    /// Mark payment as complete for current user's registration.
    /// </summary>
    [Authorize]
    [HttpPost("{id:guid}/payment/mark-paid")]
    public async Task<IActionResult> MarkPayment(Guid id, [FromBody] MarkPaymentRequest? request)
    {
        var userId = GetCurrentUserId();

        var success = await _eventService.MarkPaymentAsync(id, userId, request?.PaymentReference);

        if (!success)
        {
            return BadRequest(new { message = "Unable to mark payment. Registration not found, event is free, or payment already marked." });
        }

        return Ok(new { message = "Payment marked as complete. Awaiting organizer verification." });
    }

    /// <summary>
    /// Update payment status for a registration (organizer only).
    /// </summary>
    [Authorize]
    [HttpPut("{eventId:guid}/registrations/{registrationId:guid}/payment")]
    public async Task<IActionResult> UpdatePaymentStatus(
        Guid eventId,
        Guid registrationId,
        [FromBody] UpdatePaymentStatusRequest request)
    {
        var userId = GetCurrentUserId();

        try
        {
            var success = await _eventService.UpdatePaymentStatusAsync(eventId, registrationId, request.PaymentStatus, userId);

            if (!success)
            {
                return NotFound(new { message = "Event or registration not found, or you are not the organizer." });
            }

            return Ok(new { message = $"Payment status updated to {request.PaymentStatus}" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    #endregion

    #region Team Assignment

    /// <summary>
    /// Update team assignment for a registration (organizer only).
    /// </summary>
    [Authorize]
    [HttpPut("{eventId:guid}/registrations/{registrationId:guid}/team")]
    public async Task<IActionResult> UpdateTeamAssignment(
        Guid eventId,
        Guid registrationId,
        [FromBody] UpdateTeamAssignmentRequest request)
    {
        var userId = GetCurrentUserId();

        try
        {
            var success = await _eventService.UpdateTeamAssignmentAsync(
                eventId, registrationId, request.TeamAssignment, userId);

            if (!success)
            {
                return NotFound(new { message = "Event or registration not found, or you are not the organizer." });
            }

            return Ok(new { message = $"Moved to Team {request.TeamAssignment}" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    #endregion
}
