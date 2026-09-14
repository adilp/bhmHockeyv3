using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BHMHockey.Api.Controllers;

/// <summary>
/// Self-service password reset by email. Lives beside the older
/// /api/auth/forgot-password (which notifies an admin) and
/// /api/auth/admin/reset-password; those keep working until this flow is
/// configured and proven in production.
/// </summary>
[ApiController]
[Route("api/auth/password-reset")]
public class PasswordResetController : ControllerBase
{
    public const string RequestAcknowledgement =
        "If an account exists for that email, we've sent a link and a code to reset your password.";

    private readonly IPasswordResetService _passwordResetService;

    public PasswordResetController(IPasswordResetService passwordResetService)
    {
        _passwordResetService = passwordResetService;
    }

    /// <summary>
    /// Email a reset link and code. Always answers the same way, whether or not
    /// the address has an account.
    /// </summary>
    [HttpPost("request")]
    public async Task<ActionResult<PasswordResetMessageResponse>> RequestReset([FromBody] PasswordResetRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Email))
        {
            return BadRequest(new { message = "Email is required" });
        }

        await _passwordResetService.RequestResetAsync(request.Email);
        return Ok(new PasswordResetMessageResponse(RequestAcknowledgement));
    }

    /// <summary>
    /// Set a new password with the link token, or the email plus the 6-digit code.
    /// </summary>
    [HttpPost("confirm")]
    public async Task<ActionResult<PasswordResetMessageResponse>> ConfirmReset([FromBody] ConfirmPasswordResetRequest request)
    {
        try
        {
            await _passwordResetService.ConfirmResetAsync(request);
            return Ok(new PasswordResetMessageResponse("Your password has been reset. You can now log in."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
