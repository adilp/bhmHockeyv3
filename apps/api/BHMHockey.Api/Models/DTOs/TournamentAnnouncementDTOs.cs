namespace BHMHockey.Api.Models.DTOs;

// Response DTO - includes creator details
public record TournamentAnnouncementDto(
    Guid Id,
    Guid TournamentId,
    string Title,
    string Body,
    string? Target,           // "All", "Captains", "Admins", or null (when targeting specific teams)
    List<Guid>? TargetTeamIds, // Parsed from JSONB, null if not team-specific
    Guid CreatedByUserId,
    string CreatedByFirstName,
    string CreatedByLastName,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

// Create request
public record CreateTournamentAnnouncementRequest(
    string Title,
    string Body,
    string? Target,           // "All", "Captains", "Admins" - null if targeting specific teams
    List<Guid>? TargetTeamIds // When provided, targets specific teams (Target should be null)
);

// Update request - all fields optional for PATCH semantics
public record UpdateTournamentAnnouncementRequest(
    string? Title,
    string? Body,
    string? Target,
    List<Guid>? TargetTeamIds
);
