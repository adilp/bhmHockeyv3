namespace BHMHockey.Api.Models.Entities;

public class OrganizationAdmin
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public Guid? AddedByUserId { get; set; }  // Audit: who added this admin (null for original creator)

    // Navigation properties
    public Organization Organization { get; set; } = null!;
    public User User { get; set; } = null!;
    public User? AddedByUser { get; set; }
}
