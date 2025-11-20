namespace BHMHockey.Api.Models.Entities;

public class EventRegistration
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EventId { get; set; }
    public Event Event { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Status { get; set; } = "Registered"; // Registered, Cancelled (Phase 1 only)
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
}
