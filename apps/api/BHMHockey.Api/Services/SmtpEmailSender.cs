using System.Diagnostics;
using BHMHockey.Api.Models.DTOs;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace BHMHockey.Api.Services;

/// <summary>
/// Sends mail through an SMTP relay - in production, AWS SES.
///
/// Configuration (Smtp:*): Host, Port, Username, Password, FromAddress, FromName.
/// Username and Password are secrets and are set in the DigitalOcean console,
/// never committed.
///
/// With nothing configured in Development the email is written to the log
/// instead of sent, so the reset flow can be exercised locally. Anywhere else
/// an unconfigured sender throws: silently dropping a reset email is worse than
/// a logged failure.
/// </summary>
public class SmtpEmailSender : IEmailSender, ISmtpDiagnostics
{
    // Left in .do/app.yaml for the credentials; still present means the real
    // value was never saved in the DigitalOcean console
    private const string PlaceholderPrefix = "SET_IN_DO_CONSOLE";
    private const string PlaceholderHint =
        "Still the placeholder from .do/app.yaml - the real value was never saved in the DigitalOcean console";

    // An SES SMTP password is base64 of a version byte plus an HMAC-SHA256: 44
    // characters. An IAM secret access key is 40, and is the usual mix-up.
    private const int SesSmtpPasswordLength = 44;
    private const int IamSecretKeyLength = 40;

    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration configuration, IHostEnvironment environment, ILogger<SmtpEmailSender> logger)
    {
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    public async Task SendAsync(string toAddress, string subject, string textBody, string htmlBody, CancellationToken cancellationToken = default)
    {
        var settings = ReadSettings();

        if (!settings.IsComplete)
        {
            if (_environment.IsDevelopment())
            {
                _logger.LogWarning(
                    "SMTP not configured - email NOT sent (development only). To: {To} | Subject: {Subject}\n{Body}",
                    toAddress, subject, textBody);
                return;
            }

            throw new InvalidOperationException("SMTP is not configured");
        }

        var message = BuildMessage(settings, toAddress, subject, textBody, htmlBody);
        await DeliverAsync(settings, message, new DeliveryProgress(), cancellationToken);
    }

    public async Task<SmtpTestResponse> SendTestAsync(string toAddress, CancellationToken cancellationToken = default)
    {
        var settings = ReadSettings();
        var report = DescribeSettings(settings);

        // No development fallback here: a test that "passes" by logging instead
        // of sending would answer the one question it exists to answer wrongly
        if (!settings.IsComplete)
        {
            _logger.LogWarning("SMTP test email not attempted: SMTP is not configured");
            return new SmtpTestResponse(false, toAddress, "configuration", null,
                "SMTP is not configured - Host, Username, Password and FromAddress are all required",
                null, 0, report);
        }

        var sentAt = DateTime.UtcNow;
        var message = BuildMessage(settings, toAddress,
            "BHM Hockey SMTP test",
            $"This is a test email from the BHM Hockey server, sent {sentAt:u}. If you can read it, password reset emails can be delivered.",
            $"<p>This is a test email from the BHM Hockey server, sent {sentAt:u}.</p><p>If you can read it, password reset emails can be delivered.</p>");

        var progress = new DeliveryProgress();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await DeliverAsync(settings, message, progress, cancellationToken);
            _logger.LogInformation("SMTP test email sent via {Host}:{Port} in {ElapsedMs} ms",
                settings.Host, settings.Port, stopwatch.ElapsedMilliseconds);
            return new SmtpTestResponse(true, toAddress, null, null, null, null, stopwatch.ElapsedMilliseconds, report);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "SMTP test email failed at {Stage} via {Host}:{Port}",
                progress.Stage, settings.Host, settings.Port);
            return new SmtpTestResponse(
                progress.Accepted,
                toAddress,
                progress.Stage,
                ex.GetType().Name,
                ex.InnerException == null ? ex.Message : $"{ex.Message} -> {ex.InnerException.Message}",
                ex is SmtpCommandException command ? (int)command.StatusCode : null,
                stopwatch.ElapsedMilliseconds,
                report);
        }
    }

    private SmtpSettings ReadSettings() => new(
        _configuration["Smtp:Host"],
        int.TryParse(_configuration["Smtp:Port"], out var port) ? port : 587,
        _configuration["Smtp:Username"],
        _configuration["Smtp:Password"],
        _configuration["Smtp:FromAddress"],
        _configuration["Smtp:FromName"] ?? "BHM Hockey");

    private static MimeMessage BuildMessage(SmtpSettings settings, string toAddress, string subject, string textBody, string htmlBody)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        message.To.Add(MailboxAddress.Parse(toAddress));
        message.Subject = subject;
        message.Body = new BodyBuilder { TextBody = textBody, HtmlBody = htmlBody }.ToMessageBody();
        return message;
    }

    /// <summary>
    /// The one delivery path, shared by real mail and the admin test so a
    /// passing test means real mail works. Records how far it got.
    /// </summary>
    private static async Task DeliverAsync(SmtpSettings settings, MimeMessage message, DeliveryProgress progress, CancellationToken cancellationToken)
    {
        using var client = new SmtpClient { Timeout = 15000 };

        // Port 587 connects in plain text and upgrades with STARTTLS. Strict
        // StartTls rather than StartTlsWhenAvailable: if the server won't
        // negotiate TLS the send fails, instead of carrying on and sending the
        // SMTP credentials unencrypted.
        var socketOptions = settings.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

        progress.Stage = "connect";
        await client.ConnectAsync(settings.Host, settings.Port, socketOptions, cancellationToken);
        progress.Stage = "authenticate";
        await client.AuthenticateAsync(settings.Username, settings.Password, cancellationToken);
        progress.Stage = "send";
        await client.SendAsync(message, cancellationToken);
        progress.Accepted = true;
        progress.Stage = "disconnect";
        await client.DisconnectAsync(true, cancellationToken);
    }

    private static SmtpSettingsReport DescribeSettings(SmtpSettings settings) => new(
        settings.Host,
        settings.Port,
        settings.FromAddress,
        DescribeUsername(settings.Username),
        DescribePassword(settings.Password));

    private static SmtpCredentialReport DescribeUsername(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return new SmtpCredentialReport(false, 0, false, null, null);
        }

        var trimmed = value.Trim();
        string? hint = null;
        if (trimmed.StartsWith(PlaceholderPrefix))
        {
            hint = PlaceholderHint;
        }
        else if (!trimmed.StartsWith("AKIA"))
        {
            hint = "SES SMTP usernames start with AKIA";
        }

        // An access key id is an identifier, not a secret; the ends are enough
        // to match it against the keys listed in IAM
        var preview = value.Length > 8 ? $"{value[..4]}…{value[^4..]}" : null;
        return new SmtpCredentialReport(true, value.Length, value != trimmed, preview, hint);
    }

    private static SmtpCredentialReport DescribePassword(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return new SmtpCredentialReport(false, 0, false, null, null);
        }

        var trimmed = value.Trim();
        string? hint = null;
        if (trimmed.StartsWith(PlaceholderPrefix))
        {
            hint = PlaceholderHint;
        }
        else if (trimmed.Length == IamSecretKeyLength)
        {
            hint = $"{IamSecretKeyLength} characters is an IAM secret access key, not an SES SMTP password " +
                   $"({SesSmtpPasswordLength} characters) - create SMTP credentials under SES > SMTP settings";
        }
        else if (trimmed.Length != SesSmtpPasswordLength)
        {
            hint = $"SES SMTP passwords are {SesSmtpPasswordLength} characters";
        }

        // Never a preview: no part of the password leaves the server
        return new SmtpCredentialReport(true, value.Length, value != trimmed, null, hint);
    }

    private sealed record SmtpSettings(string? Host, int Port, string? Username, string? Password, string? FromAddress, string FromName)
    {
        public bool IsComplete =>
            !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(Username)
            && !string.IsNullOrWhiteSpace(Password) && !string.IsNullOrWhiteSpace(FromAddress);
    }

    private sealed class DeliveryProgress
    {
        public string Stage { get; set; } = "connect";
        public bool Accepted { get; set; }
    }
}
