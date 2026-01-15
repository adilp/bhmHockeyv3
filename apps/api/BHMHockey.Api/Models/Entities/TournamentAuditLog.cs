namespace BHMHockey.Api.Models.Entities;

/// <summary>
/// Audit log entry for tournament state transitions.
/// Records who changed what, when, and from/to states.
/// </summary>
public class TournamentAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TournamentId { get; set; }
    public Tournament Tournament { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>
    /// The action performed (e.g., "Publish", "Start", "Cancel", "Postpone", "Resume", "Complete", "CloseRegistration")
    /// </summary>
    public string Action { get; set; } = null!;

    /// <summary>
    /// The status before the transition
    /// </summary>
    public string FromStatus { get; set; } = null!;

    /// <summary>
    /// The status after the transition
    /// </summary>
    public string ToStatus { get; set; } = null!;

    /// <summary>
    /// Optional JSON details about the transition (e.g., new dates for postpone)
    /// </summary>
    public string? Details { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
