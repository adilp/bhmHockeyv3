namespace BHMHockey.Api.Models.Entities;

public class TournamentAnnouncement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TournamentId { get; set; }
    public Tournament Tournament { get; set; } = null!;

    public string Title { get; set; } = null!;
    public string Body { get; set; } = null!;

    // Target: "All", "Captains", "Admins" (or null when TargetTeamIds is set for specific teams)
    public string? Target { get; set; } = "All";

    // JSONB array of team Guids - when set, announcement targets specific teams
    public string? TargetTeamIds { get; set; }

    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }  // Soft delete
}
