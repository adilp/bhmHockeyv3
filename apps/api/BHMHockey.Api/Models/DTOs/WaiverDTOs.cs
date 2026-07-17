namespace BHMHockey.Api.Models.DTOs;

// A single (immutable) waiver version
public record OrganizationWaiverDto(
    Guid Id,
    Guid OrganizationId,
    string Text,
    int Version,
    DateTime CreatedAt
);

// PUT waiver request - trimmed server-side; empty text deactivates the waiver
public record SetOrganizationWaiverRequest(
    string? Text
);

// PUT waiver response - Waiver is null when no active waiver remains (cleared)
public record SetOrganizationWaiverResponse(
    OrganizationWaiverDto? Waiver
);

// Accept a SPECIFIC waiver version (stale ids are rejected with 400)
public record AcceptWaiverRequest(
    Guid WaiverId
);

// Blocking-gate entry: an org where the current user holds an upcoming
// registration but has not accepted the current active waiver
public record PendingWaiverDto(
    Guid OrganizationId,
    string OrganizationName,
    OrganizationWaiverDto Waiver
);
