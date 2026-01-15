namespace BHMHockey.Api.Models.DTOs;

/// <summary>
/// Request DTO for manually assigning a player to a team
/// </summary>
public record AssignPlayerToTeamRequest
{
    /// <summary>
    /// The ID of the team to assign the player to
    /// </summary>
    public required Guid TeamId { get; set; }
}

/// <summary>
/// Request DTO for auto-assigning all unassigned players to teams
/// </summary>
public record AutoAssignTeamsRequest
{
    /// <summary>
    /// Whether to balance teams by skill level (defaults to false)
    /// </summary>
    public bool BalanceBySkillLevel { get; set; } = false;
}

/// <summary>
/// Request DTO for creating multiple empty teams in bulk
/// </summary>
public record BulkCreateTeamsRequest
{
    /// <summary>
    /// Number of teams to create
    /// </summary>
    public required int Count { get; set; }

    /// <summary>
    /// Prefix for team names (e.g., "Team" creates "Team 1", "Team 2", etc.)
    /// </summary>
    public required string NamePrefix { get; set; }
}

/// <summary>
/// Response DTO for bulk team creation
/// </summary>
public record BulkCreateTeamsResponse
{
    /// <summary>
    /// The created teams
    /// </summary>
    public required List<TournamentTeamDto> Teams { get; init; }

    /// <summary>
    /// Success message describing the operation result
    /// </summary>
    public required string Message { get; init; }
}

/// <summary>
/// Result DTO for team assignment operations
/// </summary>
public record TeamAssignmentResultDto
{
    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Message describing the operation result
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Number of players successfully assigned
    /// </summary>
    public required int AssignedCount { get; init; }

    /// <summary>
    /// Number of players still unassigned
    /// </summary>
    public required int UnassignedCount { get; init; }
}
