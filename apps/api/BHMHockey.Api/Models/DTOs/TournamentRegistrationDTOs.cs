namespace BHMHockey.Api.Models.DTOs;

/// <summary>
/// DTO for tournament registration responses - includes all registration fields and user info
/// </summary>
public record TournamentRegistrationDto(
    Guid Id,
    Guid TournamentId,
    UserDto User,
    string Status,
    string? Position,
    // Waitlist fields
    int? WaitlistPosition,
    DateTime? PromotedAt,
    bool IsWaitlisted,
    // Team assignment
    Guid? AssignedTeamId,
    string? AssignedTeamName,
    // Custom responses
    string? CustomResponses,
    // Waiver tracking
    string? WaiverStatus,
    // Payment tracking
    string? PaymentStatus,
    DateTime? PaymentMarkedAt,
    DateTime? PaymentVerifiedAt,
    DateTime? PaymentDeadlineAt,
    // Timestamps
    DateTime RegisteredAt,
    DateTime UpdatedAt,
    DateTime? CancelledAt
);

/// <summary>
/// Request DTO for creating a tournament registration (individual player registration)
/// </summary>
public record CreateTournamentRegistrationRequest
{
    /// <summary>
    /// Position for this tournament: "Goalie" or "Skater"
    /// </summary>
    public required string Position { get; set; }

    /// <summary>
    /// JSON string containing responses to custom registration questions
    /// </summary>
    public string? CustomResponses { get; set; }

    /// <summary>
    /// Whether the user has accepted the waiver (required if tournament has waiver)
    /// </summary>
    public bool WaiverAccepted { get; set; }
}

/// <summary>
/// Request DTO for updating a tournament registration
/// </summary>
public record UpdateTournamentRegistrationRequest
{
    /// <summary>
    /// Updated position: "Goalie" or "Skater" (optional - only if allowed to change)
    /// </summary>
    public string? Position { get; set; }

    /// <summary>
    /// Updated JSON string containing responses to custom registration questions
    /// </summary>
    public string? CustomResponses { get; set; }
}

/// <summary>
/// Result DTO for tournament registration operations (includes waitlist info)
/// </summary>
public record TournamentRegistrationResultDto(
    string Status,
    int? WaitlistPosition,
    string Message
);
