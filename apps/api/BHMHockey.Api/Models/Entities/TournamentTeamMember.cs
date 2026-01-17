namespace BHMHockey.Api.Models.Entities;

public class TournamentTeamMember
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TeamId { get; set; }
    public TournamentTeam Team { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>
    /// Role: Player, Captain, CoCaptain, etc.
    /// </summary>
    public string Role { get; set; } = "Player";

    /// <summary>
    /// Status: Pending (invited), Accepted (confirmed), Declined (rejected invitation)
    /// Default to Accepted for backward compatibility with organizer-assigned members
    /// </summary>
    public string Status { get; set; } = "Accepted";

    /// <summary>
    /// Position: Goalie or Skater (nullable)
    /// </summary>
    public string? Position { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When player accepted or declined the invitation (nullable)
    /// </summary>
    public DateTime? RespondedAt { get; set; }

    /// <summary>
    /// When player was removed or left the team (nullable)
    /// </summary>
    public DateTime? LeftAt { get; set; }
}
