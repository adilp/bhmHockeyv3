namespace BHMHockey.Api.Models.Entities;

public class Organization
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CreatorId { get; set; }
    public User Creator { get; set; } = null!;
    public string? Location { get; set; }
    public string? SkillLevel { get; set; } // Beginner, Intermediate, Advanced, All
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<OrganizationSubscription> Subscriptions { get; set; } = new List<OrganizationSubscription>();
    public ICollection<Event> Events { get; set; } = new List<Event>();
}
