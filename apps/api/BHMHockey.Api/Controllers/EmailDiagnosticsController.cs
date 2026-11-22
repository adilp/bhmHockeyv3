using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BHMHockey.Api.Controllers;

/// <summary>
/// Admin-only check that the server can actually send email. Exists because a
/// password reset answers the same way whether or not its email went out, so a
/// broken SMTP setup is invisible from the outside.
/// </summary>
[ApiController]
[Route("api/auth/admin")]
[Authorize]
public class EmailDiagnosticsController : ControllerBase
{
    private readonly ISmtpDiagnostics _smtpDiagnostics;
    private readonly IUserService _userService;
    private readonly ILogger<EmailDiagnosticsController> _logger;

    public EmailDiagnosticsController(
        ISmtpDiagnostics smtpDiagnostics,
        IUserService userService,
        ILogger<EmailDiagnosticsController> logger)
    {
        _smtpDiagnostics = smtpDiagnostics;
        _userService = userService;
        _logger = logger;
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

    /// <summary>
    /// Send a test email to the caller's own address and report where delivery
    /// stopped. Always 200 once the attempt is made - a failed send is the
    /// result, described in the body. The recipient is never taken from the
    /// request, so this can't be used to mail anyone else.
    /// </summary>
    [HttpPost("test-email")]
    public async Task<ActionResult<SmtpTestResponse>> SendTestEmail(CancellationToken cancellationToken)
    {
        if (User.FindFirst(ClaimTypes.Role)?.Value != "Admin")
        {
            _logger.LogWarning("Test email denied: requester is not an admin");
            return Forbid();
        }

        var userId = GetCurrentUserId();
        var user = await _userService.GetUserByIdAsync(userId);
        if (user == null || string.IsNullOrWhiteSpace(user.Email))
        {
            _logger.LogWarning("Test email not sent: no email address for user {UserId}", userId);
            return NotFound(new { message = "No email address on your account" });
        }

        var result = await _smtpDiagnostics.SendTestAsync(user.Email, cancellationToken);
        return Ok(result);
    }
}
