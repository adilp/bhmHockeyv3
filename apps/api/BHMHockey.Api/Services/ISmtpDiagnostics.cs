using BHMHockey.Api.Models.DTOs;

namespace BHMHockey.Api.Services;

public interface ISmtpDiagnostics
{
    /// <summary>
    /// Send a test email through the same path as real mail and report where
    /// delivery stopped. Never throws for an SMTP failure - the failure is the
    /// answer. Reports on the configured credentials without revealing them.
    /// </summary>
    Task<SmtpTestResponse> SendTestAsync(string toAddress, CancellationToken cancellationToken = default);
}
