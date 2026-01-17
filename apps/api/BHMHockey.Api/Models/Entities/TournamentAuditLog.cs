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
    /// The action performed (e.g., "Publish", "Start", "Cancel", "Postpone", "Resume", "Complete", "CloseRegistration", "SetCaptain", "RemoveCaptain")
    /// </summary>
    public string Action { get; set; } = null!;

    /// <summary>
    /// The status before the transition (nullable for non-status actions like captain changes)
    /// </summary>
    public string? FromStatus { get; set; }

    /// <summary>
    /// The status after the transition (nullable for non-status actions like captain changes)
    /// </summary>
    public string? ToStatus { get; set; }

    /// <summary>
    /// The type of entity affected (e.g., "Team", "TeamMember") - for general-purpose action logging
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// The ID of the affected entity - for general-purpose action logging
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// JSON of the previous state before the action
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>
    /// JSON of the new state after the action
    /// </summary>
    public string? NewValue { get; set; }

    /// <summary>
    /// Optional JSON details about the transition (e.g., new dates for postpone)
    /// </summary>
    public string? Details { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
