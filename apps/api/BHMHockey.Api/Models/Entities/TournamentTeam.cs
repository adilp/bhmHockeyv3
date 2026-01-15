namespace BHMHockey.Api.Models.Entities;

public class TournamentTeam
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TournamentId { get; set; }
    public Tournament Tournament { get; set; } = null!;

    public string Name { get; set; } = null!;

    // Captain is optional - organizer-assigned teams may not have captains
    public Guid? CaptainUserId { get; set; }
    public User? Captain { get; set; }

    /// <summary>
    /// Status: Registered, Waitlisted, Active, Eliminated, Winner
    /// </summary>
    public string Status { get; set; } = "Registered";

    public int? WaitlistPosition { get; set; }
    public int? Seed { get; set; }
    public int? FinalPlacement { get; set; }

    public bool HasBye { get; set; } = false;

    // Statistics
    public int Wins { get; set; } = 0;
    public int Losses { get; set; } = 0;
    public int Ties { get; set; } = 0;
    public int Points { get; set; } = 0;
    public int GoalsFor { get; set; } = 0;
    public int GoalsAgainst { get; set; } = 0;

    /// <summary>
    /// PaymentStatus: null, Pending, MarkedPaid, Verified
    /// </summary>
    public string? PaymentStatus { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
