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
    // D-League team (optional, and only meaningful for D-League players).
    // Cleared automatically when no position is D-League.
    public string? DLeagueTeam { get; set; }

    public bool IsGhostPlayer { get; set; } = false;

    // The player's own switch: hide their game stats from other players (their
    // profile stats and their leaderboard row). Read and written only through
    // the game-results endpoints; deliberately NOT on UserDto, so no existing
    // response changes shape. The column has a database default of false.
    public bool HideGameStats { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
