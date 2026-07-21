namespace BHMHockey.Api.Models.Entities;

/// <summary>
/// Records that a user accepted a SPECIFIC waiver version (OrganizationWaivers row).
/// A new waiver version requires a new acceptance; old acceptances are kept as
/// part of the legal audit trail.
/// </summary>
public class WaiverAcceptance
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid WaiverId { get; set; }
    public OrganizationWaiver Waiver { get; set; } = null!;
    public DateTime AcceptedAt { get; set; } = DateTime.UtcNow;

    // Signature fields captured at acceptance time. Immutable audit data:
    // written once when the acceptance is recorded and never updated
    // (re-accepting the same version keeps the original values). Null on
    // rows recorded before signatures were introduced. Dates are calendar
    // dates stored at UTC midnight.
    public string? ParticipantName { get; set; }
    public DateTime? ParticipantDate { get; set; }

    // Parent/Guardian section - all-or-nothing: either every field below is
    // set (participant under 19) or all are null (adult-only acceptance)
    public string? MinorParticipantName { get; set; }
    public DateTime? MinorDateOfBirth { get; set; }
    public string? GuardianName { get; set; }
    public string? GuardianSignature { get; set; }
    public DateTime? GuardianDate { get; set; }
}
