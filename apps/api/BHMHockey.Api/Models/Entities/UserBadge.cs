namespace BHMHockey.Api.Models.Entities;

public class UserBadge
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // User who earned this badge
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    // The badge type earned
    public Guid BadgeTypeId { get; set; }
    public BadgeType BadgeType { get; set; } = null!;

    // Badge-specific details (tournament name, year, description, etc.)
    // Stored as JSONB in PostgreSQL
    public Dictionary<string, object>? Context { get; set; }

    // When the badge was earned
    public DateTime EarnedAt { get; set; } = DateTime.UtcNow;

    // When the celebration modal was shown to the user (null = needs celebration)
    public DateTime? CelebratedAt { get; set; }

    // User's custom ordering (null = not customized, use BadgeType.SortPriority)
    public int? DisplayOrder { get; set; }
}
