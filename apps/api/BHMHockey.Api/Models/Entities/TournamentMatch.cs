namespace BHMHockey.Api.Models.Entities;

public class TournamentMatch
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TournamentId { get; set; }
    public Tournament Tournament { get; set; } = null!;

    // Team references (nullable for TBD/bye matches)
    public Guid? HomeTeamId { get; set; }
    public TournamentTeam? HomeTeam { get; set; }

    public Guid? AwayTeamId { get; set; }
    public TournamentTeam? AwayTeam { get; set; }

    // Match position in tournament
    public int Round { get; set; }
    public int MatchNumber { get; set; }
    /// <summary>
    /// Bracket position identifier (e.g., "QF1", "SF1", "Final", "3rdPlace", "W-R1-M1", "L-R1-M1")
    /// </summary>
    public string? BracketPosition { get; set; }

    /// <summary>
    /// Bracket type for double elimination tournaments: "Winners", "Losers", "GrandFinal", or null for round robin/single elimination
    /// </summary>
    public string? BracketType { get; set; }

    public bool IsBye { get; set; } = false;

    // Scheduling
    public DateTime? ScheduledTime { get; set; }
    public string? Venue { get; set; }

    /// <summary>
    /// Match status: Scheduled, InProgress, Completed, Cancelled, Forfeit, Bye
    /// </summary>
    public string Status { get; set; } = "Scheduled";

    // Scores
    public int? HomeScore { get; set; }
    public int? AwayScore { get; set; }

    // Winner tracking
    public Guid? WinnerTeamId { get; set; }
    public TournamentTeam? WinnerTeam { get; set; }

    public string? ForfeitReason { get; set; }

    // Bracket advancement (self-references)
    /// <summary>
    /// Next match for the winner of this match
    /// </summary>
    public Guid? NextMatchId { get; set; }
    public TournamentMatch? NextMatch { get; set; }

    /// <summary>
    /// Next match for the loser of this match (double elimination only)
    /// </summary>
    public Guid? LoserNextMatchId { get; set; }
    public TournamentMatch? LoserNextMatch { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
