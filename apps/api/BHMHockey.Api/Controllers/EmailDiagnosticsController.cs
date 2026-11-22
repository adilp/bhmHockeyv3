using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BHMHockey.Api.Controllers;

/// <summary>
/// Check that the server can actually send email, for app admins and anyone who
/// administers an organization. Exists because a password reset answers the
/// same way whether or not its email went out, so a broken SMTP setup is
/// invisible from the outside.
/// </summary>
[ApiController]
[Route("api/auth/admin")]
[Authorize]
public class EmailDiagnosticsController : ControllerBase
{
    private readonly ISmtpDiagnostics _smtpDiagnostics;
    private readonly IUserService _userService;
    private readonly IOrganizationAdminService _organizationAdminService;
    private readonly ILogger<EmailDiagnosticsController> _logger;

    public EmailDiagnosticsController(
        ISmtpDiagnostics smtpDiagnostics,
        IUserService userService,
        IOrganizationAdminService organizationAdminService,
        ILogger<EmailDiagnosticsController> logger)
    {
        _smtpDiagnostics = smtpDiagnostics;
        _userService = userService;
        _organizationAdminService = organizationAdminService;
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
    /// stopped. Open to app admins and organization admins: the recipient is
    /// never taken from the request, so the most it can do is mail the caller,
    /// and no part of the SMTP password is ever returned. Always 200 once the
    /// attempt is made - a failed send is the result, described in the body.
    /// </summary>
    [HttpPost("test-email")]
    public async Task<ActionResult<SmtpTestResponse>> SendTestEmail(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        var isAppAdmin = User.FindFirst(ClaimTypes.Role)?.Value == "Admin";
        if (!isAppAdmin && !await _organizationAdminService.IsAdminOfAnyOrganizationAsync(userId))
        {
            _logger.LogWarning("Test email denied for user {UserId}: not an app or organization admin", userId);
            return Forbid();
        }

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
