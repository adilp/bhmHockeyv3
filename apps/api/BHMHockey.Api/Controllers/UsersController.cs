using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BHMHockey.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IOrganizationService _organizationService;
    private readonly IEventService _eventService;

    public UsersController(IUserService userService, IOrganizationService organizationService, IEventService eventService)
    {
        _userService = userService;
        _organizationService = organizationService;
        _eventService = eventService;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (userIdClaim == null || !Guid.TryParse(userIdClaim, out var userId))
        {
            throw new UnauthorizedAccessException("User ID not found in token");
        }

        return userId;
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> GetCurrentUser()
    {
        try
        {
            var userId = GetCurrentUserId();
            var user = await _userService.GetUserByIdAsync(userId);

            if (user == null)
            {
                return NotFound(new { message = "User not found" });
            }

            return Ok(user);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPut("me")]
    public async Task<ActionResult<UserDto>> UpdateProfile([FromBody] UpdateUserProfileRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var updatedUser = await _userService.UpdateProfileAsync(userId, request);
            return Ok(updatedUser);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("me/push-token")]
    public async Task<ActionResult> UpdatePushToken([FromBody] UpdatePushTokenRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _userService.UpdatePushTokenAsync(userId, request.PushToken);
            return Ok(new { message = "Push token updated successfully" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("me/subscriptions")]
    public async Task<ActionResult<List<OrganizationSubscriptionDto>>> GetMySubscriptions()
    {
        try
        {
            var userId = GetCurrentUserId();
            var subscriptions = await _organizationService.GetUserSubscriptionsAsync(userId);
            return Ok(subscriptions);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpGet("me/registrations")]
    public async Task<ActionResult<List<EventDto>>> GetMyRegistrations()
    {
        try
        {
            var userId = GetCurrentUserId();
            var events = await _eventService.GetUserRegistrationsAsync(userId);
            return Ok(events);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpGet("me/organizations")]
    public async Task<ActionResult<List<OrganizationDto>>> GetMyOrganizations()
    {
        try
        {
            var userId = GetCurrentUserId();
            var organizations = await _organizationService.GetUserCreatedOrganizationsAsync(userId);
            return Ok(organizations);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }
}
