namespace BHMHockey.Api.Models.DTOs;

public record EventDto(
    Guid Id,
    Guid OrganizationId,
    string OrganizationName,
    string Name,
    string? Description,
    DateTime EventDate,
    int Duration,
    string? Venue,
    int MaxPlayers,
    int RegisteredCount,
    decimal Cost,
    DateTime? RegistrationDeadline,
    string Status,
    bool IsRegistered,
    DateTime CreatedAt
);

public record CreateEventRequest(
    Guid OrganizationId,
    string Name,
    string? Description,
    DateTime EventDate,
    int Duration,
    string? Venue,
    int MaxPlayers,
    decimal Cost,
    DateTime? RegistrationDeadline
);

public record UpdateEventRequest(
    string? Name,
    string? Description,
    DateTime? EventDate,
    int? Duration,
    string? Venue,
    int? MaxPlayers,
    decimal? Cost,
    DateTime? RegistrationDeadline,
    string? Status
);

public record EventRegistrationDto(
    Guid Id,
    Guid EventId,
    UserDto User,
    string Status,
    DateTime RegisteredAt
);
