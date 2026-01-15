namespace BHMHockey.Api.Models.Entities;

public class TournamentTeamMember
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TeamId { get; set; }
    public TournamentTeam Team { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>
    /// Role: Player (default for organizer-assigned mode)
    /// TRN-011 will add more roles (Captain, CoCapain, etc.)
    /// </summary>
    public string Role { get; set; } = "Player";

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
