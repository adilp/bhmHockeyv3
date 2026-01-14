namespace BHMHockey.Api.Models.DTOs;

/// <summary>
/// DTO for tournament responses - includes all tournament fields and computed properties
/// </summary>
public record TournamentDto
{
    public required Guid Id { get; init; }
    public Guid? OrganizationId { get; init; }
    public string? OrganizationName { get; init; }
    public required Guid CreatorId { get; init; }

    // Basic info
    public required string Name { get; init; }
    public string? Description { get; init; }

    // Format & Configuration
    public required string Format { get; init; }
    public required string TeamFormation { get; init; }

    // Status
    public required string Status { get; init; }

    // Dates
    public required DateTime StartDate { get; init; }
    public required DateTime EndDate { get; init; }
    public required DateTime RegistrationDeadline { get; init; }
    public DateTime? PostponedToDate { get; init; }

    // Team configuration
    public required int MaxTeams { get; init; }
    public int? MinPlayersPerTeam { get; init; }
    public int? MaxPlayersPerTeam { get; init; }
    public required bool AllowMultiTeam { get; init; }
    public required bool AllowSubstitutions { get; init; }

    // Payment
    public required decimal EntryFee { get; init; }
    public string? FeeType { get; init; }

    // Round Robin config
    public required int PointsWin { get; init; }
    public required int PointsTie { get; init; }
    public required int PointsLoss { get; init; }
    public string? PlayoffFormat { get; init; }
    public int? PlayoffTeamsCount { get; init; }

    // Content
    public string? RulesContent { get; init; }
    public string? WaiverUrl { get; init; }
    public string? Venue { get; init; }

    // Configuration (as JSON strings - will be parsed on frontend)
    public string? NotificationSettings { get; init; }
    public string? CustomQuestions { get; init; }
    public string? EligibilityRequirements { get; init; }
    public string? TiebreakerOrder { get; init; }

    // Timestamps
    public required DateTime CreatedAt { get; init; }
    public required DateTime UpdatedAt { get; init; }
    public DateTime? PublishedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime? CancelledAt { get; init; }

    // Computed fields (context-aware)
    public bool CanManage { get; init; }
}

/// <summary>
/// Request DTO for creating a new tournament
/// </summary>
public record CreateTournamentRequest
{
    public Guid? OrganizationId { get; set; }

    // Basic info
    public required string Name { get; set; }
    public string? Description { get; set; }

    // Format & Configuration
    public required string Format { get; set; }  // SingleElimination, DoubleElimination, RoundRobin
    public required string TeamFormation { get; set; }  // OrganizerAssigned, PreFormed

    // Dates
    public required DateTime StartDate { get; set; }
    public required DateTime EndDate { get; set; }
    public required DateTime RegistrationDeadline { get; set; }

    // Team configuration
    public required int MaxTeams { get; set; }
    public int? MinPlayersPerTeam { get; set; }
    public int? MaxPlayersPerTeam { get; set; }
    public bool AllowMultiTeam { get; set; } = false;
    public bool AllowSubstitutions { get; set; } = true;

    // Payment
    public decimal EntryFee { get; set; } = 0;
    public string? FeeType { get; set; }  // null (free), PerPlayer, PerTeam

    // Round Robin config
    public int PointsWin { get; set; } = 3;
    public int PointsTie { get; set; } = 1;
    public int PointsLoss { get; set; } = 0;
    public string? PlayoffFormat { get; set; }  // null, SingleElimination, DoubleElimination
    public int? PlayoffTeamsCount { get; set; }

    // Content
    public string? RulesContent { get; set; }
    public string? WaiverUrl { get; set; }
    public string? Venue { get; set; }

    // Configuration (JSON strings)
    public string? NotificationSettings { get; set; }
    public string? CustomQuestions { get; set; }
    public string? EligibilityRequirements { get; set; }
    public string? TiebreakerOrder { get; set; }
}

/// <summary>
/// Request DTO for updating a tournament (all fields optional for patch semantics)
/// </summary>
public record UpdateTournamentRequest
{
    // Basic info
    public string? Name { get; set; }
    public string? Description { get; set; }

    // Format & Configuration (can only change in Draft status)
    public string? Format { get; set; }
    public string? TeamFormation { get; set; }

    // Dates
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? RegistrationDeadline { get; set; }

    // Team configuration
    public int? MaxTeams { get; set; }
    public int? MinPlayersPerTeam { get; set; }
    public int? MaxPlayersPerTeam { get; set; }
    public bool? AllowMultiTeam { get; set; }
    public bool? AllowSubstitutions { get; set; }

    // Payment
    public decimal? EntryFee { get; set; }
    public string? FeeType { get; set; }

    // Round Robin config
    public int? PointsWin { get; set; }
    public int? PointsTie { get; set; }
    public int? PointsLoss { get; set; }
    public string? PlayoffFormat { get; set; }
    public int? PlayoffTeamsCount { get; set; }

    // Content
    public string? RulesContent { get; set; }
    public string? WaiverUrl { get; set; }
    public string? Venue { get; set; }

    // Configuration (JSON strings)
    public string? NotificationSettings { get; set; }
    public string? CustomQuestions { get; set; }
    public string? EligibilityRequirements { get; set; }
    public string? TiebreakerOrder { get; set; }
}

/// <summary>
/// DTO for tournament admin information
/// </summary>
public record TournamentAdminDto
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }
    public required string Role { get; init; }
    public required DateTime AddedAt { get; init; }
    public Guid? AddedByUserId { get; init; }
    public string? AddedByName { get; init; }
}
