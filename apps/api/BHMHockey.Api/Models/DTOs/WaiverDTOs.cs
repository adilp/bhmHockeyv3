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

// Accept a SPECIFIC waiver version (stale ids are rejected with 400).
// Signature fields are recorded once on the acceptance row (immutable audit
// data). ParticipantName/ParticipantDate are required (400 when missing or
// blank). The Parent/Guardian section is all-or-nothing: either every minor
// field is provided (participant under 19) or all are omitted (400 when
// partially filled). Dates are calendar dates (client sends YYYY-MM-DD).
public record AcceptWaiverRequest(
    Guid WaiverId,
    string? ParticipantName,
    DateTime? ParticipantDate,
    string? MinorParticipantName = null,
    DateTime? MinorDateOfBirth = null,
    string? GuardianName = null,
    string? GuardianSignature = null,
    DateTime? GuardianDate = null
);

// Blocking-gate entry: an org where the current user holds an upcoming
// registration but has not accepted the current active waiver
public record PendingWaiverDto(
    Guid OrganizationId,
    string OrganizationName,
    OrganizationWaiverDto Waiver
);
