using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BHMHockey.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
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

    private bool IsAdmin()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value == "Admin";
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            var response = await _authService.RegisterAsync(request);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var response = await _authService.LoginAsync(request);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var response = await _authService.RefreshTokenAsync(request.RefreshToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Admin-only endpoint to reset a user's password.
    /// Generates a temporary password that must be shared with the user securely.
    /// </summary>
    /// <param name="userId">The user ID to reset password for</param>
    /// <returns>The temporary password to share with the user</returns>
    [HttpPost("admin/reset-password/{userId}")]
    [Authorize]
    public async Task<ActionResult<AdminPasswordResetResponse>> AdminResetPassword(Guid userId)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        try
        {
            var response = await _authService.AdminResetPasswordAsync(userId);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Admin-only endpoint to get system stats.
    /// </summary>
    /// <returns>Stats including user counts</returns>
    [HttpGet("admin/stats")]
    [Authorize]
    public async Task<ActionResult<AdminStatsResponse>> GetAdminStats()
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var stats = await _authService.GetAdminStatsAsync();
        return Ok(stats);
    }

    /// <summary>
    /// Admin-only endpoint to search users by email or name.
    /// </summary>
    /// <param name="query">Search query (matches email, first name, or last name)</param>
    /// <returns>List of matching users</returns>
    [HttpGet("admin/users/search")]
    [Authorize]
    public async Task<ActionResult<List<AdminUserSearchResult>>> SearchUsers([FromQuery] string query)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
        {
            return BadRequest(new { message = "Search query must be at least 2 characters" });
        }

        var results = await _authService.SearchUsersAsync(query);
        return Ok(results);
    }

    /// <summary>
    /// Change password for the currently authenticated user.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _authService.ChangePasswordAsync(userId, request);
            return Ok(new { message = "Password changed successfully" });
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
    /// Request password reset. Sends notification to admin.
    /// Does not reveal if email exists (security).
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<ActionResult<ForgotPasswordResponse>> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new { message = "Email is required" });
        }

        var response = await _authService.ForgotPasswordAsync(request);
        return Ok(response);
    }

    /// <summary>
    /// Admin-only endpoint to update a user's role.
    /// </summary>
    /// <param name="userId">The user ID to update</param>
    /// <param name="request">The new role</param>
    /// <returns>Updated user info</returns>
    [HttpPut("admin/users/{userId}/role")]
    [Authorize]
    public async Task<ActionResult<AdminUpdateRoleResponse>> UpdateUserRole(Guid userId, [FromBody] AdminUpdateRoleRequest request)
    {
        if (!IsAdmin())
        {
            return Forbid();
        }

        var validRoles = new[] { "Player", "Organizer", "Admin" };
        if (!validRoles.Contains(request.Role))
        {
            return BadRequest(new { message = "Invalid role. Must be Player, Organizer, or Admin." });
        }

        try
        {
            var response = await _authService.UpdateUserRoleAsync(userId, request.Role);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
