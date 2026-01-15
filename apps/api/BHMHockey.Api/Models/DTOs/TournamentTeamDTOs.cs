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

/// <summary>
/// Request DTO for entering match scores.
/// For elimination formats with tied scores, OvertimeWinnerId is required.
/// </summary>
public record EnterScoreRequest
{
    /// <summary>
    /// Score for the home team (must be >= 0)
    /// </summary>
    public required int HomeScore { get; init; }

    /// <summary>
    /// Score for the away team (must be >= 0)
    /// </summary>
    public required int AwayScore { get; init; }

    /// <summary>
    /// Required when HomeScore == AwayScore in elimination formats.
    /// Must be either HomeTeamId or AwayTeamId.
    /// </summary>
    public Guid? OvertimeWinnerId { get; init; }
}

/// <summary>
/// Request DTO for marking a match as forfeit.
/// </summary>
public record ForfeitMatchRequest
{
    /// <summary>
    /// The team that is forfeiting the match.
    /// Must be either HomeTeamId or AwayTeamId.
    /// </summary>
    public required Guid ForfeitingTeamId { get; init; }

    /// <summary>
    /// Reason for the forfeit (e.g., "No-show", "Insufficient players")
    /// </summary>
    public required string Reason { get; init; }
}

/// <summary>
/// DTO for score entry audit log details (serialized to JSON in audit log)
/// </summary>
public record ScoreEntryAuditDto
{
    public Guid MatchId { get; init; }
    public int? OldHomeScore { get; init; }
    public int? OldAwayScore { get; init; }
    public Guid? OldWinnerId { get; init; }
    public int NewHomeScore { get; init; }
    public int NewAwayScore { get; init; }
    public Guid NewWinnerId { get; init; }
    public bool IsEdit { get; init; }
}
