using BHMHockey.Api.Models.DTOs;

namespace BHMHockey.Api.Services;

public interface IOrganizationWaiverService
{
    /// <summary>
    /// Get the org's current ACTIVE waiver (latest version row with non-empty text).
    /// Returns null when the org has no waiver or the latest version was cleared.
    /// </summary>
    Task<OrganizationWaiverDto?> GetCurrentWaiverAsync(Guid organizationId);

    /// <summary>
    /// Create the next waiver version from the submitted text (admin only).
    /// Text is trimmed; empty text deactivates the waiver (new empty version row,
    /// history preserved). Saving text identical to the current version is a no-op
    /// so members are not re-gated accidentally.
    /// Returns the active waiver after the save, or null when no active waiver remains.
    /// </summary>
    Task<OrganizationWaiverDto?> SetWaiverAsync(Guid organizationId, string? text, Guid userId);

    /// <summary>
    /// Record that the user accepted a SPECIFIC waiver version, including the
    /// signature fields captured at acceptance. Rejects (InvalidOperationException)
    /// when the id is not the org's current active version (stale-version
    /// protection), when the participant name/date is missing, or when the
    /// Parent/Guardian section is partially filled (all-or-nothing). Idempotent
    /// when already accepted - the original row's signature fields are preserved.
    /// </summary>
    Task AcceptWaiverAsync(Guid organizationId, AcceptWaiverRequest request, Guid userId);

    /// <summary>
    /// Orgs where the user holds an active (Registered/Waitlisted) registration on
    /// an UPCOMING, non-cancelled event whose org has an active waiver the user
    /// has not accepted. Powers the blocking accept-or-leave gate.
    /// </summary>
    Task<List<PendingWaiverDto>> GetPendingWaiversAsync(Guid userId);

    /// <summary>
    /// Id of the org's active waiver version, or null when none.
    /// </summary>
    Task<Guid?> GetActiveWaiverIdAsync(Guid organizationId);

    /// <summary>
    /// True when the org has an active waiver the user has not accepted.
    /// </summary>
    Task<bool> IsAcceptanceRequiredAsync(Guid organizationId, Guid userId);

    /// <summary>
    /// Render the org's current active waiver as a PDF.
    /// Returns null when there is no active waiver.
    /// </summary>
    Task<(byte[] Content, string FileName)?> GetCurrentWaiverPdfAsync(Guid organizationId);
}
