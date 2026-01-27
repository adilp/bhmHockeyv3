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

/// <summary>
/// Request DTO for transferring team captaincy
/// </summary>
public record TransferCaptainRequest
{
    /// <summary>
    /// The user ID of the new captain. Must be an existing team member with Status = Accepted.
    /// </summary>
    public required Guid NewCaptainUserId { get; init; }
}

/// <summary>
/// Response DTO for captain transfer
/// </summary>
public record TransferCaptainResponse
{
    public required Guid TeamId { get; init; }
    public required Guid OldCaptainUserId { get; init; }
    public required Guid NewCaptainUserId { get; init; }
    public required DateTime TransferredAt { get; init; }
}

/// <summary>
/// DTO for pending tournament team invitations shown to the user
/// </summary>
public record PendingTeamInvitationDto
{
    public required Guid MemberId { get; init; }
    public required Guid TeamId { get; init; }
    public required string TeamName { get; init; }
    public required Guid TournamentId { get; init; }
    public required string TournamentName { get; init; }
    public required string CaptainName { get; init; }
    public required DateTime InvitedAt { get; init; }
}

/// <summary>
/// DTO for user search results (captain-accessible for team building, also used for adding players to events)
/// </summary>
public record UserSearchResultDto
{
    public required Guid Id { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string Email { get; init; }
    public Dictionary<string, string>? Positions { get; init; }  // position -> skill level (e.g., "goalie" -> "Silver")
}
