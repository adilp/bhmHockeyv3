using System.Net;
using System.Security.Cryptography;
using System.Text;
using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

/// <summary>
/// Self-service password reset by email.
///
/// Each request mails a link (for tapping) and a 6-digit code (for typing, and
/// for email clients that mangle links). Either one resets the password, once.
/// Only hashes are stored; a request expires after PasswordReset:ExpiryMinutes;
/// a newer request invalidates older ones so only the latest email works.
/// </summary>
public class PasswordResetService : IPasswordResetService
{
    public const string InvalidMessage = "This reset link or code is invalid or has expired. Please request a new one.";

    // An email address gets a few tries in a window, then goes quiet - enough for
    // "I didn't get it, send again", not enough to flood someone's inbox
    private const int MaxRequestsPerWindow = 3;
    private static readonly TimeSpan RequestWindow = TimeSpan.FromMinutes(15);

    // A million possible codes; five guesses per request makes guessing hopeless
    private const int MaxCodeAttempts = 5;

    // Matches ChangePasswordAsync
    private const int MinPasswordLength = 6;

    private readonly AppDbContext _context;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(
        AppDbContext context,
        IEmailSender emailSender,
        IConfiguration configuration,
        ILogger<PasswordResetService> logger)
    {
        _context = context;
        _emailSender = emailSender;
        _configuration = configuration;
        _logger = logger;
    }

    private int ExpiryMinutes =>
        int.TryParse(_configuration["PasswordReset:ExpiryMinutes"], out var minutes) && minutes > 0 ? minutes : 30;

    private string LinkBaseUrl =>
        string.IsNullOrWhiteSpace(_configuration["PasswordReset:LinkBaseUrl"])
            ? "https://bhmhockey.com/reset"
            : _configuration["PasswordReset:LinkBaseUrl"]!;

    public async Task RequestResetAsync(string email)
    {
        var normalized = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            return;
        }

        var user = await _context.Users.FirstOrDefaultAsync(u =>
            u.Email.ToLower() == normalized && u.IsActive && !u.IsGhostPlayer);
        if (user == null)
        {
            _logger.LogInformation("Password reset requested for an address with no active account");
            return;
        }

        var now = DateTime.UtcNow;
        var windowStart = now - RequestWindow;
        var recentRequests = await _context.PasswordResetTokens
            .CountAsync(t => t.UserId == user.Id && t.CreatedAt > windowStart);
        if (recentRequests >= MaxRequestsPerWindow)
        {
            _logger.LogWarning("Password reset rate limit reached for user {UserId}", user.Id);
            return;
        }

        // Only the newest email should work - an older link sitting in an inbox
        // is one more thing that can leak
        var outstanding = await _context.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null && t.InvalidatedAt == null)
            .ToListAsync();
        foreach (var previous in outstanding)
        {
            previous.InvalidatedAt = now;
        }

        var token = GenerateToken();
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var reset = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = Sha256(token),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(ExpiryMinutes)
        };
        reset.CodeHash = HashCode(reset.Id, code);

        _context.PasswordResetTokens.Add(reset);
        await _context.SaveChangesAsync();

        var link = $"{LinkBaseUrl}?token={Uri.EscapeDataString(token)}";
        var (subject, text, html) = BuildEmail(user.FirstName, link, code, ExpiryMinutes);

        try
        {
            await _emailSender.SendAsync(user.Email, subject, text, html);
            _logger.LogInformation("Password reset email sent to user {UserId}", user.Id);
        }
        catch (Exception ex)
        {
            // Still answer the caller the same way - a failure here must not
            // become a signal that the address has an account
            _logger.LogError(ex, "Password reset email failed for user {UserId}", user.Id);
        }
    }

    public async Task ConfirmResetAsync(ConfirmPasswordResetRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < MinPasswordLength)
        {
            throw new InvalidOperationException($"New password must be at least {MinPasswordLength} characters");
        }

        var now = DateTime.UtcNow;
        PasswordResetToken? reset;

        if (!string.IsNullOrWhiteSpace(request.Token))
        {
            var tokenHash = Sha256(request.Token.Trim());
            reset = await _context.PasswordResetTokens
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TokenHash == tokenHash);

            if (reset == null || !IsLive(reset, now))
            {
                throw new InvalidOperationException(InvalidMessage);
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.Email) && !string.IsNullOrWhiteSpace(request.Code))
        {
            var normalized = request.Email.Trim().ToLowerInvariant();
            reset = await _context.PasswordResetTokens
                .Include(t => t.User)
                .Where(t => t.User.Email.ToLower() == normalized
                    && t.UsedAt == null && t.InvalidatedAt == null && t.ExpiresAt > now)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            if (reset == null)
            {
                throw new InvalidOperationException(InvalidMessage);
            }

            if (!CodeMatches(reset, request.Code.Trim()))
            {
                reset.FailedAttempts++;
                if (reset.FailedAttempts >= MaxCodeAttempts)
                {
                    reset.InvalidatedAt = now;
                }
                await _context.SaveChangesAsync();

                _logger.LogWarning(
                    "Wrong password reset code for user {UserId} (attempt {Attempt} of {Max})",
                    reset.UserId, reset.FailedAttempts, MaxCodeAttempts);
                throw new InvalidOperationException(InvalidMessage);
            }
        }
        else
        {
            throw new InvalidOperationException("Provide the token from the reset link, or your email and the 6-digit code");
        }

        var user = reset.User;
        if (!user.IsActive || user.IsGhostPlayer)
        {
            throw new InvalidOperationException(InvalidMessage);
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAt = now;
        reset.UsedAt = now;

        var others = await _context.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.Id != reset.Id && t.UsedAt == null && t.InvalidatedAt == null)
            .ToListAsync();
        foreach (var other in others)
        {
            other.InvalidatedAt = now;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Password reset completed for user {UserId}", user.Id);
    }

    private static bool IsLive(PasswordResetToken reset, DateTime now) =>
        reset.UsedAt == null && reset.InvalidatedAt == null && reset.ExpiresAt > now
        && reset.FailedAttempts < MaxCodeAttempts;

    private static bool CodeMatches(PasswordResetToken reset, string code) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(HashCode(reset.Id, code)),
            Encoding.UTF8.GetBytes(reset.CodeHash));

    private static string HashCode(Guid resetId, string code) => Sha256($"{resetId:N}:{code}");

    private static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    /// URL-safe base64 of 32 random bytes (256 bits)
    private static string GenerateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static (string Subject, string Text, string Html) BuildEmail(
        string firstName, string link, string code, int expiryMinutes)
    {
        var name = string.IsNullOrWhiteSpace(firstName) ? "there" : firstName.Trim();
        const string subject = "Reset your BHM Hockey password";

        var text =
            $"Hi {name},\n\n" +
            "We received a request to reset your BHM Hockey password.\n\n" +
            $"Tap this link on your phone to choose a new password:\n{link}\n\n" +
            $"Or enter this code in the app: {code}\n\n" +
            $"The link and code work once and expire in {expiryMinutes} minutes.\n\n" +
            "If you didn't ask for this, you can ignore this email - your password won't change.\n\n" +
            "- BHM Hockey";

        var safeName = WebUtility.HtmlEncode(name);
        var safeLink = WebUtility.HtmlEncode(link);
        var html =
            "<!doctype html><html><body style=\"font-family:-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:#1f2328;line-height:1.5;\">" +
            $"<p>Hi {safeName},</p>" +
            "<p>We received a request to reset your BHM Hockey password.</p>" +
            $"<p><a href=\"{safeLink}\" style=\"display:inline-block;background:#00D9C0;color:#0D1117;padding:12px 20px;border-radius:8px;text-decoration:none;font-weight:600;\">Reset password</a></p>" +
            "<p>Or enter this code in the app:</p>" +
            $"<p style=\"font-size:28px;font-weight:700;letter-spacing:6px;font-family:Menlo,Consolas,monospace;\">{code}</p>" +
            $"<p style=\"color:#59636e;\">The link and code work once and expire in {expiryMinutes} minutes. " +
            "If you didn't ask for this, you can ignore this email - your password won't change.</p>" +
            "<p>- BHM Hockey</p></body></html>";

        return (subject, text, html);
    }
}
