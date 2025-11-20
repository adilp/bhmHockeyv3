namespace BHMHockey.Api.Models.Entities;

public class OrganizationSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public bool NotificationEnabled { get; set; } = true;
    public DateTime SubscribedAt { get; set; } = DateTime.UtcNow;
}
