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
public class SmtpEmailSender : IEmailSender
{
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
        var host = _configuration["Smtp:Host"];
        var username = _configuration["Smtp:Username"];
        var password = _configuration["Smtp:Password"];
        var fromAddress = _configuration["Smtp:FromAddress"];

        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(fromAddress))
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

        var port = int.TryParse(_configuration["Smtp:Port"], out var configuredPort) ? configuredPort : 587;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_configuration["Smtp:FromName"] ?? "BHM Hockey", fromAddress));
        message.To.Add(MailboxAddress.Parse(toAddress));
        message.Subject = subject;
        message.Body = new BodyBuilder { TextBody = textBody, HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient { Timeout = 15000 };

        // Port 587 connects in plain text and upgrades with STARTTLS. Strict
        // StartTls rather than StartTlsWhenAvailable: if the server won't
        // negotiate TLS the send fails, instead of carrying on and sending the
        // SMTP credentials unencrypted.
        var socketOptions = port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

        await client.ConnectAsync(host, port, socketOptions, cancellationToken);
        await client.AuthenticateAsync(username, password, cancellationToken);
        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
