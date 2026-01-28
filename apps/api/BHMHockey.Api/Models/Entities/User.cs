namespace BHMHockey.Api.Models.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? PushToken { get; set; }

    // Hockey profile fields - Multi-position support
    // Keys: "goalie", "skater" (lowercase)
    // Values: "Gold", "Silver", "Bronze", "D-League"
    public Dictionary<string, string>? Positions { get; set; }
    public string? VenmoHandle { get; set; }

    public string Role { get; set; } = "Player"; // Player, Organizer, Admin
    public bool IsActive { get; set; } = true;
    public bool IsGhostPlayer { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
