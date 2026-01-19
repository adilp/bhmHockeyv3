namespace BHMHockey.Api.Models.DTOs;

public record TournamentAdminDto(
    Guid Id,
    Guid TournamentId,
    Guid UserId,
    string UserFirstName,
    string UserLastName,
    string UserEmail,
    string Role,
    DateTime AddedAt,
    Guid? AddedByUserId,
    string? AddedByName
);

public record AddTournamentAdminRequest(
    Guid UserId,
    string Role  // Admin or Scorekeeper only
);

public record UpdateTournamentAdminRoleRequest(
    string Role  // Admin or Scorekeeper only
);

public record TransferOwnershipRequest(
    Guid NewOwnerUserId
);
