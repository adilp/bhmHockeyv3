using System.ComponentModel.DataAnnotations.Schema;

namespace BHMHockey.Api.Models.Entities;

public class TournamentRegistration
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TournamentId { get; set; }
    public Tournament Tournament { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>
    /// Status: Registered, Waitlisted, Assigned, Cancelled
    /// </summary>
    public string Status { get; set; } = "Registered";

    /// <summary>
    /// Position: Goalie or Skater
    /// </summary>
    public string? Position { get; set; }

    // Waitlist fields
    public int? WaitlistPosition { get; set; }
    public DateTime? PromotedAt { get; set; }

    // Team assignment (for OrganizerAssigned mode)
    public Guid? AssignedTeamId { get; set; }
    public TournamentTeam? AssignedTeam { get; set; }

    // Custom registration responses (JSONB)
    public string? CustomResponses { get; set; }

    // Waiver tracking
    /// <summary>
    /// WaiverStatus: Signed, Pending
    /// </summary>
    public string? WaiverStatus { get; set; }

    // Payment tracking
    /// <summary>
    /// PaymentStatus: null (free), Pending, MarkedPaid, Verified
    /// </summary>
    public string? PaymentStatus { get; set; }
    public DateTime? PaymentMarkedAt { get; set; }
    public DateTime? PaymentVerifiedAt { get; set; }
    public DateTime? PaymentDeadlineAt { get; set; }

    // Timestamps
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAt { get; set; }

    // Computed properties
    [NotMapped]
    public bool IsWaitlisted => Status == "Waitlisted";
}
