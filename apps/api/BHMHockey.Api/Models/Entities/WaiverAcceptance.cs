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
}
