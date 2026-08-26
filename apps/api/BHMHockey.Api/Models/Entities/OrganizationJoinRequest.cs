namespace BHMHockey.Api.Models.Entities;

/// <summary>
/// A user's request to join a PRIVATE organization. One row per (org, user) -
/// the same row is reused as the request moves through the flow, so a denial is
/// retained (a denied user cannot re-request) while staying actionable by an
/// admin who wants to reverse an accidental deny.
/// </summary>
public class OrganizationJoinRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public string Status { get; set; } = OrganizationJoinRequestStatus.Pending;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAt { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public User? DecidedByUser { get; set; }
}

public static class OrganizationJoinRequestStatus
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Denied = "Denied";

    public static readonly HashSet<string> All = new() { Pending, Approved, Denied };
}
