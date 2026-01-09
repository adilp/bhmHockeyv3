namespace BHMHockey.Api.Models.Entities;

public class BadgeType
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Machine name for referencing in code (e.g., "tournament_winner", "beta_tester")
    public string Code { get; set; } = string.Empty;

    // Display name shown to users (e.g., "Tournament Champion")
    public string Name { get; set; } = string.Empty;

    // What this badge means
    public string Description { get; set; } = string.Empty;

    // Asset reference for the app (e.g., "trophy_gold", "star_teal")
    public string IconName { get; set; } = string.Empty;

    // Grouping: "achievement", "milestone", "social"
    public string Category { get; set; } = string.Empty;

    // Default ordering when user hasn't customized
    public int SortPriority { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
