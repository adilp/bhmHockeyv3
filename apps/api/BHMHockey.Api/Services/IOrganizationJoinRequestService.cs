using BHMHockey.Api.Models.DTOs;

namespace BHMHockey.Api.Services;

public interface IOrganizationJoinRequestService
{
    /// <summary>
    /// Create (or re-open) a Pending join request for a private organization and
    /// notify the org's admins. Throws InvalidOperationException when the user was
    /// previously denied - a denial blocks re-requesting.
    /// </summary>
    Task<SubscribeOutcome> RequestJoinAsync(Guid organizationId, Guid userId);

    /// <summary>
    /// Approve a join request: subscribe the user, mark the request Approved, notify
    /// the requester. Works on a Denied request too, so an accidental deny is
    /// recoverable. Idempotent when already approved/subscribed.
    /// Throws UnauthorizedAccessException when the requester is not an org admin.
    /// </summary>
    Task<bool> ApproveAsync(Guid organizationId, Guid requestUserId, Guid adminUserId);

    /// <summary>
    /// Deny a join request: mark it Denied and notify the requester. The row is
    /// retained so the user cannot re-request but an admin can still approve it.
    /// Throws UnauthorizedAccessException when the requester is not an org admin.
    /// </summary>
    Task<bool> DenyAsync(Guid organizationId, Guid requestUserId, Guid adminUserId);

    /// <summary>
    /// Join requests for an organization, newest first. Admin only.
    /// Pass null for status to get every request regardless of status.
    /// Throws UnauthorizedAccessException when the requester is not an org admin.
    /// </summary>
    Task<List<OrganizationJoinRequestDto>> GetRequestsAsync(Guid organizationId, Guid adminUserId, string? status);

    /// <summary>
    /// Drop the user's Approved request when they leave the org, so they can
    /// request to join again later. Leaving is not the same as being denied, so
    /// Denied rows are left untouched.
    /// </summary>
    Task ClearApprovedRequestAsync(Guid organizationId, Guid userId);
}
