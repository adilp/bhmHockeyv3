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

    // Hockey profile fields
    public string? SkillLevel { get; set; } // Gold, Silver, Bronze, D-League
    public string? Position { get; set; } // Forward, Defense, Goalie
    public string? VenmoHandle { get; set; }

    public string Role { get; set; } = "Player"; // Player, Organizer, Admin
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
