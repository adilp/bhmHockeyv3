namespace BHMHockey.Api.Models.Entities;

public class Tournament
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Organization is optional - null means standalone tournament
    public Guid? OrganizationId { get; set; }
    public Organization? Organization { get; set; }

    public Guid CreatorId { get; set; }
    public User Creator { get; set; } = null!;

    // Basic info
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    // Format & Configuration
    // Format: SingleElimination, DoubleElimination, RoundRobin
    public string Format { get; set; } = "SingleElimination";
    // TeamFormation: OrganizerAssigned, PreFormed
    public string TeamFormation { get; set; } = "OrganizerAssigned";

    // State machine status
    // Status: Draft, Open, RegistrationClosed, InProgress, Completed, Postponed, Cancelled
    public string Status { get; set; } = "Draft";

    // Dates
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public DateTime RegistrationDeadline { get; set; }
    public DateTime? PostponedToDate { get; set; }

    // Team configuration
    public int MaxTeams { get; set; }
    public int? MinPlayersPerTeam { get; set; }
    public int? MaxPlayersPerTeam { get; set; }
    public bool AllowMultiTeam { get; set; } = false;
    public bool AllowSubstitutions { get; set; } = true;

    // Payment
    public decimal EntryFee { get; set; } = 0;
    // FeeType: null (free), PerPlayer, PerTeam
    public string? FeeType { get; set; }

    // Round Robin config
    public int PointsWin { get; set; } = 3;
    public int PointsTie { get; set; } = 1;
    public int PointsLoss { get; set; } = 0;
    // PlayoffFormat: null, SingleElimination, DoubleElimination
    public string? PlayoffFormat { get; set; }
    public int? PlayoffTeamsCount { get; set; }

    // Content (no file uploads - URLs only)
    public string? RulesContent { get; set; }
    public string? WaiverUrl { get; set; }
    public string? Venue { get; set; }

    // Configuration (JSONB columns)
    public string? NotificationSettings { get; set; }  // JSON: which notifications enabled
    public string? CustomQuestions { get; set; }        // JSON: registration form questions
    public string? EligibilityRequirements { get; set; } // JSON: custom restrictions text
    public string? TiebreakerOrder { get; set; }        // JSON: ["HeadToHead", "GoalDifferential", "GoalsScored"]

    // Timestamps
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PublishedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    // Navigation properties
    public ICollection<TournamentAdmin> Admins { get; set; } = new List<TournamentAdmin>();
    public ICollection<TournamentAnnouncement> Announcements { get; set; } = new List<TournamentAnnouncement>();
}
