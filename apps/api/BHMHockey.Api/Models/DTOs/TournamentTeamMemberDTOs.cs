namespace BHMHockey.Api.Models.DTOs;

/// <summary>
/// DTO for tournament team member responses - includes all member fields and user information
/// </summary>
public record TournamentTeamMemberDto
{
    public required Guid Id { get; init; }
    public required Guid TeamId { get; init; }
    public required Guid UserId { get; init; }
    public required string UserFirstName { get; init; }
    public required string UserLastName { get; init; }
    public required string UserEmail { get; init; }
    public required string Role { get; init; }
    public required string Status { get; init; }
    public string? Position { get; init; }
    public required DateTime JoinedAt { get; init; }
    public DateTime? RespondedAt { get; init; }
    public DateTime? LeftAt { get; init; }
}

/// <summary>
/// Request DTO for adding a player to a team (organizer action)
/// </summary>
public record AddTeamMemberRequest
{
    public required Guid UserId { get; set; }
}

/// <summary>
/// Request DTO for player responding to a team invitation
/// </summary>
public record RespondToTeamInviteRequest
{
    /// <summary>
    /// True to accept the invitation, false to decline
    /// </summary>
    public required bool Accept { get; set; }

    /// <summary>
    /// Position (Goalie or Skater) - required if Accept is true
    /// </summary>
    public string? Position { get; set; }

    /// <summary>
    /// Optional JSON string containing custom form responses
    /// </summary>
    public string? CustomResponses { get; set; }
}
