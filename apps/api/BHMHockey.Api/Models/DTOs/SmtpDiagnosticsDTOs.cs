namespace BHMHockey.Api.Models.DTOs;

/// <summary>
/// Outcome of an admin SMTP test send.
/// </summary>
public record SmtpTestResponse(
    bool Sent,              // True once the SMTP server accepted the message
    string To,
    string? FailedStage,    // "configuration" | "connect" (includes TLS) | "authenticate" | "send" | "disconnect"; null on success
    string? ErrorType,      // Exception type, e.g. "AuthenticationException"
    string? Error,          // The server's or socket's own words, e.g. "535: Authentication Credentials Invalid"
    int? SmtpStatusCode,    // SMTP reply code when the server rejected a command
    long ElapsedMs,
    SmtpSettingsReport Settings
);

/// <summary>
/// What the server is configured with. Host, port and from address are not
/// secret (they're committed in .do/app.yaml); the credentials are described,
/// never shown.
/// </summary>
public record SmtpSettingsReport(
    string? Host,
    int Port,
    string? FromAddress,
    SmtpCredentialReport Username,
    SmtpCredentialReport Password
);

public record SmtpCredentialReport(
    bool Present,
    int Length,
    bool HasSurroundingWhitespace,  // A stray space or newline from pasting breaks the login
    string? Preview,                // Username only: first and last 4 characters, to match against IAM. Always null for the password
    string? Hint                    // A likely mistake recognised from the value's shape, if any
);
