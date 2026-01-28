using System.Security.Claims;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Exceptions;
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

    private string GetCurrentUserRole()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value ?? "Player";
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
    /// Create a new event. Requires authentication and Organizer/Admin role.
    /// </summary>
    [Authorize]
    [HttpPost]
    public async Task<ActionResult<EventDto>> Create([FromBody] CreateEventRequest request)
    {
        if (GetCurrentUserRole() == "Player")
        {
            return Forbid();
        }

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
        catch (ConcurrentModificationException ex)
        {
            return Conflict(new { message = ex.Message });
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
    /// Returns empty list for non-organizers when roster is unpublished (draft mode).
    /// </summary>
    [HttpGet("{id:guid}/registrations")]
    public async Task<ActionResult<List<EventRegistrationDto>>> GetRegistrations(Guid id)
    {
        var currentUserId = GetCurrentUserIdOrNull();
        var evt = await _eventService.GetByIdAsync(id, currentUserId);

        if (evt == null)
            return NotFound();

        // Organizers and published events: full roster
        if (evt.CanManage || evt.IsRosterPublished)
        {
            return Ok(await _eventService.GetRegistrationsAsync(id));
        }

        // Non-organizers on unpublished events: empty list
        // (They see their own status via EventDto fields instead)
        return Ok(new List<EventRegistrationDto>());
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

        var dtos = await _eventService.GetWaitlistWithBadgesAsync(id);
        return Ok(dtos);
    }

    #endregion

    #region Waitlist Management

    /// <summary>
    /// Reorder waitlist positions (organizer only).
    /// All waitlisted users must be included with sequential positions starting from 1.
    /// </summary>
    [Authorize]
    [HttpPut("{eventId:guid}/waitlist/reorder")]
    public async Task<IActionResult> ReorderWaitlist(
        Guid eventId,
        [FromBody] ReorderWaitlistRequest request)
    {
        var userId = GetCurrentUserId();

        // Check if event exists
        var eventDto = await _eventService.GetByIdAsync(eventId);
        if (eventDto == null)
        {
            return NotFound();
        }

        // Check if user can manage this event
        if (!await _eventService.CanUserManageEventAsync(eventId, userId))
        {
            return Forbid();
        }

        try
        {
            // Map WaitlistOrderItem to WaitlistReorderItem
            var items = request.Items
                .Select(i => new WaitlistReorderItem(i.RegistrationId, i.Position))
                .ToList();

            await _waitlistService.ReorderWaitlistAsync(eventId, items);

            return Ok(new { success = true, message = "Waitlist reordered" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
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
    /// Returns detailed result including whether user was promoted to roster.
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
            var result = await _eventService.UpdatePaymentStatusAsync(eventId, registrationId, request.PaymentStatus, userId);

            if (!result.Success)
            {
                return NotFound(new { message = result.Message });
            }

            return Ok(result);
        }
        catch (ConcurrentModificationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    #endregion

    #region Registration Management (Organizer)

    /// <summary>
    /// Remove a registration (organizer only). Works for both registered and waitlisted users.
    /// </summary>
    [Authorize]
    [HttpDelete("{eventId:guid}/registrations/{registrationId:guid}")]
    public async Task<IActionResult> RemoveRegistration(Guid eventId, Guid registrationId)
    {
        var userId = GetCurrentUserId();

        try
        {
            var success = await _eventService.RemoveRegistrationAsync(eventId, registrationId, userId);

            if (!success)
            {
                return NotFound(new { message = "Event or registration not found, or you are not the organizer." });
            }

            return Ok(new { message = "Registration removed successfully" });
        }
        catch (ConcurrentModificationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Move a waitlisted player to the roster (organizer only).
    /// </summary>
    [Authorize]
    [HttpPost("{eventId:guid}/registrations/{registrationId:guid}/move-to-roster")]
    public async Task<ActionResult<MoveResultDto>> MoveToRoster(Guid eventId, Guid registrationId)
    {
        var userId = GetCurrentUserId();

        try
        {
            var result = await _eventService.MoveToRosterAsync(eventId, registrationId, userId);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Message });
            }

            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// Move a rostered player to the waitlist (organizer only).
    /// </summary>
    [Authorize]
    [HttpPost("{eventId:guid}/registrations/{registrationId:guid}/move-to-waitlist")]
    public async Task<ActionResult<MoveResultDto>> MoveToWaitlist(Guid eventId, Guid registrationId)
    {
        var userId = GetCurrentUserId();

        try
        {
            var result = await _eventService.MoveToWaitlistAsync(eventId, registrationId, userId);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Message });
            }

            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// Publish the roster for an event (organizer only).
    /// Sets IsRosterPublished=true and sends notifications to all players.
    /// </summary>
    [Authorize]
    [HttpPost("{eventId:guid}/publish-roster")]
    public async Task<ActionResult<PublishResultDto>> PublishRoster(Guid eventId)
    {
        var userId = GetCurrentUserId();

        try
        {
            var result = await _eventService.PublishRosterAsync(eventId, userId);

            if (!result.Success)
            {
                return BadRequest(new { message = result.Message });
            }

            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
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

    /// <summary>
    /// Search for users that can be added to an event's waitlist (organizer only).
    /// Returns users matching the query by first name or last name, excluding those already registered.
    /// </summary>
    [Authorize]
    [HttpGet("{eventId:guid}/search-users")]
    public async Task<ActionResult<List<UserSearchResultDto>>> SearchUsersForEvent(
        Guid eventId,
        [FromQuery] string query)
    {
        var userId = GetCurrentUserId();

        try
        {
            var results = await _eventService.SearchUsersForEventAsync(eventId, userId, query);
            return Ok(results);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    /// <summary>
    /// Add a user to an event's waitlist (organizer only).
    /// Creates a new registration with Status="Waitlisted" and sends a notification to the user.
    /// </summary>
    [Authorize]
    [HttpPost("{eventId:guid}/registrations/add-user")]
    public async Task<ActionResult<EventRegistrationDto>> AddUserToEvent(
        Guid eventId,
        [FromBody] AddUserToEventRequest request)
    {
        var userId = GetCurrentUserId();

        try
        {
            var registration = await _eventService.AddUserToWaitlistAsync(
                eventId, request.UserId, userId, request.Position);
            return Ok(registration);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Create a ghost player and add them to an event's waitlist (organizer only).
    /// Ghost players are placeholder accounts for people who don't have the app.
    /// </summary>
    [Authorize]
    [HttpPost("{eventId:guid}/registrations/create-ghost-player")]
    public async Task<ActionResult<EventRegistrationDto>> CreateGhostPlayer(
        Guid eventId,
        [FromBody] CreateGhostPlayerRequest request)
    {
        var userId = GetCurrentUserId();

        try
        {
            var registration = await _eventService.CreateGhostPlayerAsync(
                eventId, userId, request.FirstName, request.LastName, request.Position, request.SkillLevel);
            return Ok(registration);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    #endregion

    #region Roster Order

    /// <summary>
    /// Update roster order for all registrations (organizer only).
    /// Allows reordering players and changing teams in a single batch update.
    /// </summary>
    [Authorize]
    [HttpPut("{eventId:guid}/roster-order")]
    public async Task<IActionResult> UpdateRosterOrder(
        Guid eventId,
        [FromBody] UpdateRosterOrderRequest request)
    {
        var userId = GetCurrentUserId();

        try
        {
            var success = await _eventService.UpdateRosterOrderAsync(
                eventId, request.Items, userId);

            if (!success)
            {
                return NotFound(new { message = "Event not found or you are not the organizer." });
            }

            return Ok(new { message = "Roster order updated successfully" });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    #endregion
}
