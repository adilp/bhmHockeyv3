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
    private const string ADMIN_EMAIL = "a@a.com";
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

    private string? GetCurrentUserEmail()
    {
        return User.FindFirst(ClaimTypes.Email)?.Value
            ?? User.FindFirst("email")?.Value;
    }

    private bool IsAdmin()
    {
        var email = GetCurrentUserEmail();
        return email?.ToLower() == ADMIN_EMAIL.ToLower();
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
    /// Admin-only endpoint to search users by email.
    /// </summary>
    /// <param name="email">Email to search for (partial match)</param>
    /// <returns>List of matching users</returns>
    [HttpGet("admin/users/search")]
    [Authorize]
    public async Task<ActionResult<List<AdminUserSearchResult>>> SearchUsers([FromQuery] string email)
    {
        Console.WriteLine($"🔍 [SearchUsers] Called with email: {email}");
        Console.WriteLine($"🔍 [SearchUsers] User email: {GetCurrentUserEmail()}");
        Console.WriteLine($"🔍 [SearchUsers] IsAdmin: {IsAdmin()}");

        if (!IsAdmin())
        {
            Console.WriteLine($"🔍 [SearchUsers] Forbidden - not admin");
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(email) || email.Length < 2)
        {
            Console.WriteLine($"🔍 [SearchUsers] Bad request - email too short");
            return BadRequest(new { message = "Email search must be at least 2 characters" });
        }

        try
        {
            var results = await _authService.SearchUsersAsync(email);
            Console.WriteLine($"🔍 [SearchUsers] Found {results.Count} results");
            return Ok(results);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"🔍 [SearchUsers] Error: {ex.Message}");
            Console.WriteLine($"🔍 [SearchUsers] Stack: {ex.StackTrace}");
            throw;
        }
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
}
