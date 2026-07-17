namespace BHMHockey.Api.Models.Entities;

public class OrganizationAutoRosterMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Position { get; set; } = "Skater"; // "Goalie" or "Skater"
    public int SortOrder { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public Guid? AddedByUserId { get; set; }
    public User? AddedByUser { get; set; }
}
