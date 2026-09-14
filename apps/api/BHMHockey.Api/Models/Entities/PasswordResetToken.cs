namespace BHMHockey.Api.Models.Entities;

/// <summary>
/// One password reset request. The email carries two ways to prove it came to
/// you - a link token and a 6-digit code - and only their hashes live here, so
/// a copy of this table can't be replayed to take over an account.
///
/// A row is live until it is used, expires, is superseded by a newer request
/// (InvalidatedAt), or burns through its code attempts.
/// </summary>
public class PasswordResetToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// SHA-256 of the link token. The token itself only ever exists in the email.
    public string TokenHash { get; set; } = string.Empty;

    /// SHA-256 of "{Id}:{code}". Salted with the row id, because a 6-digit code has
    /// only a million values and an unsalted hash of one is trivially reversed.
    public string CodeHash { get; set; } = string.Empty;

    /// Wrong code guesses. The code is small enough to brute-force, so it dies
    /// after a handful; the link token is not guessable and has no such limit.
    public int FailedAttempts { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime? InvalidatedAt { get; set; }
}
