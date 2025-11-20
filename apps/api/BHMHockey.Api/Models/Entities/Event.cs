namespace BHMHockey.Api.Models.Entities;

public class Event
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid CreatorId { get; set; }
    public User Creator { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public int Duration { get; set; } = 60; // minutes
    public string? Venue { get; set; }
    public int MaxPlayers { get; set; } = 20;
    public decimal Cost { get; set; } = 0;
    public DateTime? RegistrationDeadline { get; set; }
    public string Status { get; set; } = "Published"; // Draft, Published, Full, Completed, Cancelled
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<EventRegistration> Registrations { get; set; } = new List<EventRegistration>();
}
