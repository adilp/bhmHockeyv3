using System.ComponentModel.DataAnnotations.Schema;

namespace BHMHockey.Api.Models.Entities;

public class EventRegistration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Status { get; set; } = "Registered"; // Registered, Cancelled
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    // Position tracking - which position user registered as (Goalie or Skater)
    public string? RegisteredPosition { get; set; }

    // Payment tracking fields (Phase 4)
    public string? PaymentStatus { get; set; } // null (free), Pending, MarkedPaid, Verified
    public DateTime? PaymentMarkedAt { get; set; } // When user marked as paid
    public DateTime? PaymentVerifiedAt { get; set; } // When organizer verified

    // Team assignment for games
    public string? TeamAssignment { get; set; } // "Black" or "White"

    // Roster ordering (for drag-and-drop reordering)
    public int? RosterOrder { get; set; } // Order within team (lower = higher on roster)

    // Waitlist fields (Phase 5)
    public int? WaitlistPosition { get; set; } // Position in waitlist (1 = first, null = not waitlisted)
    public DateTime? PromotedAt { get; set; } // When user was promoted from waitlist
    public DateTime? PaymentDeadlineAt { get; set; } // Deadline to pay after promotion (2 hours)

    // Computed properties
    [NotMapped]
    public bool IsWaitlisted => Status == "Waitlisted";

    [NotMapped]
    public bool IsPromotedAndPending => PromotedAt != null && PaymentStatus == "Pending";
}
