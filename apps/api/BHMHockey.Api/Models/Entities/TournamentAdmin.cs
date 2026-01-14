namespace BHMHockey.Api.Models.Entities;

public class TournamentAdmin
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TournamentId { get; set; }
    public Tournament Tournament { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    // Role: Owner, Admin, Scorekeeper
    public string Role { get; set; } = "Admin";

    public Guid? AddedByUserId { get; set; }
    public User? AddedByUser { get; set; }

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RemovedAt { get; set; }
}
