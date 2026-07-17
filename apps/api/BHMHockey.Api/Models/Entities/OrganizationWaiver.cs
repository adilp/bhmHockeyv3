namespace BHMHockey.Api.Models.Entities;

/// <summary>
/// A single immutable version of an organization's legal waiver.
/// Editing the waiver always creates a NEW row with the next version number;
/// rows are never updated or deleted so we can prove exactly what text any
/// user accepted (legal audit trail). The "active" waiver is the org's latest
/// version row IF its Text is non-empty - saving an empty text creates a new
/// version that deactivates the waiver while preserving history.
/// </summary>
public class OrganizationWaiver
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public string Text { get; set; } = string.Empty;
    public int Version { get; set; }  // Per-org incrementing, starting at 1
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
}
