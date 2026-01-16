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
    private readonly IBadgeService _badgeService;

    public UsersController(
        IUserService userService,
        IOrganizationService organizationService,
        IEventService eventService,
        IBadgeService badgeService)
    {
        _userService = userService;
        _organizationService = organizationService;
        _eventService = eventService;
        _badgeService = badgeService;
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
            var organizations = await _organizationService.GetUserAdminOrganizationsAsync(userId);
            return Ok(organizations);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpDelete("me")]
    public async Task<ActionResult> DeleteAccount()
    {
        try
        {
            var userId = GetCurrentUserId();
            await _userService.DeleteAccountAsync(userId);
            return Ok(new { message = "Account deleted successfully" });
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

    /// <summary>
    /// Gets all badges for a user, sorted by display order
    /// </summary>
    /// <param name="id">The user ID</param>
    /// <returns>List of user badges</returns>
    [HttpGet("{id}/badges")]
    public async Task<ActionResult<List<UserBadgeDto>>> GetUserBadges(Guid id)
    {
        try
        {
            // Any authenticated user can view any user's badges (public achievements)
            var badges = await _badgeService.GetUserBadgesAsync(id);
            return Ok(badges);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Updates the display order of the current user's badges
    /// </summary>
    /// <param name="request">Ordered list of all badge IDs</param>
    /// <returns>204 No Content on success</returns>
    [HttpPatch("me/badges/order")]
    public async Task<ActionResult> UpdateBadgeOrder([FromBody] UpdateBadgeOrderRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _badgeService.UpdateBadgeOrderAsync(userId, request.BadgeIds);
            return NoContent();
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

    /// <summary>
    /// Gets all uncelebrated badges for the current user
    /// </summary>
    /// <returns>List of uncelebrated badges with rarity counts, sorted by earned date</returns>
    [HttpGet("me/badges/uncelebrated")]
    public async Task<ActionResult<List<UncelebratedBadgeDto>>> GetUncelebratedBadges()
    {
        try
        {
            var userId = GetCurrentUserId();
            Console.WriteLine($"🎖️ [GetUncelebratedBadges] Fetching for userId: {userId}");
            var badges = await _badgeService.GetUncelebratedBadgesAsync(userId);
            Console.WriteLine($"🎖️ [GetUncelebratedBadges] Found {badges.Count} uncelebrated badges");
            return Ok(badges);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Marks a badge as celebrated for the current user
    /// </summary>
    /// <param name="id">The badge ID to celebrate</param>
    /// <returns>204 No Content on success</returns>
    [HttpPatch("me/badges/{id}/celebrate")]
    public async Task<ActionResult> CelebrateBadge(Guid id)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _badgeService.CelebrateBadgeAsync(userId, id);
            return NoContent();
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
}
