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

    // Payment tracking fields (Phase 4)
    public string? PaymentStatus { get; set; } // null (free), Pending, MarkedPaid, Verified
    public DateTime? PaymentMarkedAt { get; set; } // When user marked as paid
    public DateTime? PaymentVerifiedAt { get; set; } // When organizer verified
}
