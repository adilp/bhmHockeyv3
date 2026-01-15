namespace BHMHockey.Api.Models.DTOs;

/// <summary>
/// DTO for tournament team responses - includes all team fields and computed properties
/// </summary>
public record TournamentTeamDto
{
    public required Guid Id { get; init; }
    public required Guid TournamentId { get; init; }
    public required string Name { get; init; }

    // Captain info
    public Guid? CaptainUserId { get; init; }
    public string? CaptainName { get; init; }

    // Status & Position
    public required string Status { get; init; }
    public int? WaitlistPosition { get; init; }
    public int? Seed { get; init; }
    public int? FinalPlacement { get; init; }
    public required bool HasBye { get; init; }

    // Statistics
    public required int Wins { get; init; }
    public required int Losses { get; init; }
    public required int Ties { get; init; }
    public required int Points { get; init; }
    public required int GoalsFor { get; init; }
    public required int GoalsAgainst { get; init; }

    // Computed
    public int GoalDifferential { get; init; }

    // Payment
    public string? PaymentStatus { get; init; }

    // Timestamps
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Request DTO for creating a new tournament team
/// </summary>
public record CreateTournamentTeamRequest
{
    public required string Name { get; set; }
    public int? Seed { get; set; }
}

/// <summary>
/// Request DTO for updating a tournament team (all fields optional for patch semantics)
/// </summary>
public record UpdateTournamentTeamRequest
{
    public string? Name { get; set; }
    public int? Seed { get; set; }
}

/// <summary>
/// DTO for tournament match responses - includes all match fields and team information
/// </summary>
public record TournamentMatchDto
{
    public required Guid Id { get; init; }
    public required Guid TournamentId { get; init; }

    // Teams
    public Guid? HomeTeamId { get; init; }
    public string? HomeTeamName { get; init; }
    public Guid? AwayTeamId { get; init; }
    public string? AwayTeamName { get; init; }

    // Match Info
    public required int Round { get; init; }
    public required int MatchNumber { get; init; }
    public string? BracketPosition { get; init; }

    // Schedule & Venue
    public required bool IsBye { get; init; }
    public DateTime? ScheduledTime { get; init; }
    public string? Venue { get; init; }

    // Status & Score
    public required string Status { get; init; }
    public int? HomeScore { get; init; }
    public int? AwayScore { get; init; }

    // Winner
    public Guid? WinnerTeamId { get; init; }
    public string? WinnerTeamName { get; init; }

    // Forfeit
    public string? ForfeitReason { get; init; }

    // Bracket Navigation
    public Guid? NextMatchId { get; init; }
    public Guid? LoserNextMatchId { get; init; }

    // Timestamps
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
}
