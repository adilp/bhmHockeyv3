namespace BHMHockey.Api.Services;

public interface IEmailSender
{
    /// <summary>
    /// Send one email with plain-text and HTML bodies.
    /// </summary>
    /// <exception cref="InvalidOperationException">SMTP is not configured outside development</exception>
    Task SendAsync(string toAddress, string subject, string textBody, string htmlBody, CancellationToken cancellationToken = default);
}
