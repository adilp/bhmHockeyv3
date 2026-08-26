using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

/// <summary>
/// Join requests for private organizations: request, approve, deny, list.
/// One reusable row per (org, user) - see OrganizationJoinRequest.
/// </summary>
public class OrganizationJoinRequestService : IOrganizationJoinRequestService
{
    private readonly AppDbContext _context;
    private readonly IOrganizationAdminService _adminService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<OrganizationJoinRequestService> _logger;

    public OrganizationJoinRequestService(
        AppDbContext context,
        IOrganizationAdminService adminService,
        INotificationService notificationService,
        ILogger<OrganizationJoinRequestService> logger)
    {
        _context = context;
        _adminService = adminService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<SubscribeOutcome> RequestJoinAsync(Guid organizationId, Guid userId)
    {
        var organization = await _context.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId && o.IsActive);

        if (organization == null)
        {
            throw new InvalidOperationException("Organization not found.");
        }

        var existing = await _context.OrganizationJoinRequests
            .FirstOrDefaultAsync(r => r.OrganizationId == organizationId && r.UserId == userId);

        if (existing != null)
        {
            if (existing.Status == OrganizationJoinRequestStatus.Denied)
            {
                throw new InvalidOperationException(
                    "Your request to join this organization was declined. Contact an organizer if you think this is a mistake.");
            }

            if (existing.Status == OrganizationJoinRequestStatus.Pending)
            {
                return SubscribeOutcome.JoinRequestAlreadyPending;
            }

            // Approved but no longer subscribed (removed by an admin) - re-open the request
            existing.Status = OrganizationJoinRequestStatus.Pending;
            existing.RequestedAt = DateTime.UtcNow;
            existing.DecidedAt = null;
            existing.DecidedByUserId = null;
        }
        else
        {
            _context.OrganizationJoinRequests.Add(new OrganizationJoinRequest
            {
                OrganizationId = organizationId,
                UserId = userId
            });
        }

        await _context.SaveChangesAsync();

        await NotifyAdminsOfRequestAsync(organization, userId);

        _logger.LogInformation(
            "User {UserId} requested to join private organization {OrganizationId}", userId, organizationId);

        return SubscribeOutcome.JoinRequestCreated;
    }

    public async Task<bool> ApproveAsync(Guid organizationId, Guid requestUserId, Guid adminUserId)
    {
        var organization = await RequireAdminAsync(organizationId, adminUserId, "approve join requests");

        var request = await _context.OrganizationJoinRequests
            .FirstOrDefaultAsync(r => r.OrganizationId == organizationId && r.UserId == requestUserId);

        if (request == null)
        {
            return false;
        }

        var alreadyApproved = request.Status == OrganizationJoinRequestStatus.Approved;

        request.Status = OrganizationJoinRequestStatus.Approved;
        request.DecidedAt = DateTime.UtcNow;
        request.DecidedByUserId = adminUserId;

        var alreadySubscribed = await _context.OrganizationSubscriptions
            .AnyAsync(s => s.OrganizationId == organizationId && s.UserId == requestUserId);

        if (!alreadySubscribed)
        {
            _context.OrganizationSubscriptions.Add(new OrganizationSubscription
            {
                OrganizationId = organizationId,
                UserId = requestUserId
            });
        }

        await _context.SaveChangesAsync();

        // Idempotent: an already-approved+subscribed request is not re-announced
        if (!alreadyApproved || !alreadySubscribed)
        {
            await NotifyRequesterOfDecisionAsync(organization, requestUserId, approved: true, adminUserId);
        }

        _logger.LogInformation(
            "Admin {AdminUserId} approved join request for user {UserId} in organization {OrganizationId}",
            adminUserId, requestUserId, organizationId);

        return true;
    }

    public async Task<bool> DenyAsync(Guid organizationId, Guid requestUserId, Guid adminUserId)
    {
        var organization = await RequireAdminAsync(organizationId, adminUserId, "deny join requests");

        var request = await _context.OrganizationJoinRequests
            .FirstOrDefaultAsync(r => r.OrganizationId == organizationId && r.UserId == requestUserId);

        if (request == null)
        {
            return false;
        }

        var alreadyDenied = request.Status == OrganizationJoinRequestStatus.Denied;

        request.Status = OrganizationJoinRequestStatus.Denied;
        request.DecidedAt = DateTime.UtcNow;
        request.DecidedByUserId = adminUserId;

        await _context.SaveChangesAsync();

        if (!alreadyDenied)
        {
            await NotifyRequesterOfDecisionAsync(organization, requestUserId, approved: false, adminUserId);
        }

        _logger.LogInformation(
            "Admin {AdminUserId} denied join request for user {UserId} in organization {OrganizationId}",
            adminUserId, requestUserId, organizationId);

        return true;
    }

    public async Task<List<OrganizationJoinRequestDto>> GetRequestsAsync(Guid organizationId, Guid adminUserId, string? status)
    {
        await RequireAdminAsync(organizationId, adminUserId, "view join requests");

        if (status != null && !OrganizationJoinRequestStatus.All.Contains(status))
        {
            throw new InvalidOperationException(
                $"Invalid status: '{status}'. Valid values: Pending, Approved, Denied");
        }

        var query = _context.OrganizationJoinRequests
            .Include(r => r.User)
            .Where(r => r.OrganizationId == organizationId);

        if (status != null)
        {
            query = query.Where(r => r.Status == status);
        }

        var requests = await query
            .OrderByDescending(r => r.RequestedAt)
            .ToListAsync();

        return requests.Select(r => new OrganizationJoinRequestDto(
            r.Id,
            r.OrganizationId,
            r.UserId,
            r.User.FirstName,
            r.User.LastName,
            r.Status,
            r.RequestedAt,
            r.DecidedAt
        )).ToList();
    }

    public async Task ClearApprovedRequestAsync(Guid organizationId, Guid userId)
    {
        var request = await _context.OrganizationJoinRequests
            .FirstOrDefaultAsync(r => r.OrganizationId == organizationId
                && r.UserId == userId
                && r.Status == OrganizationJoinRequestStatus.Approved);

        if (request == null)
        {
            return;
        }

        _context.OrganizationJoinRequests.Remove(request);
        await _context.SaveChangesAsync();
    }

    private async Task<Organization> RequireAdminAsync(Guid organizationId, Guid adminUserId, string action)
    {
        var organization = await _context.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId && o.IsActive);

        if (organization == null)
        {
            throw new InvalidOperationException("Organization not found.");
        }

        var isAdmin = await _adminService.IsUserAdminAsync(organizationId, adminUserId);
        if (!isAdmin)
        {
            throw new UnauthorizedAccessException($"Only organization admins can {action}.");
        }

        return organization;
    }

    private static string DisplayName(User user)
    {
        var name = $"{user.FirstName} {user.LastName}".Trim();
        return string.IsNullOrEmpty(name) ? user.Email : name;
    }

    // Notification helpers. Push token may be missing - pass string.Empty rather
    // than returning early, so the in-app notification row is still recorded.

    private async Task NotifyAdminsOfRequestAsync(Organization organization, Guid requesterId)
    {
        var requester = await _context.Users.FirstOrDefaultAsync(u => u.Id == requesterId);
        if (requester == null) return;

        var adminIds = await _context.OrganizationAdmins
            .Where(a => a.OrganizationId == organization.Id)
            .Select(a => a.UserId)
            .ToListAsync();

        var admins = await _context.Users
            .Where(u => adminIds.Contains(u.Id) && u.Id != requesterId && !u.IsGhostPlayer)
            .ToListAsync();

        if (admins.Count == 0) return;

        var requesterName = DisplayName(requester);

        foreach (var admin in admins)
        {
            await _notificationService.SendPushNotificationAsync(
                admin.PushToken ?? string.Empty,
                "New Join Request",
                $"{requesterName} asked to join {organization.Name}",
                new { organizationId = organization.Id.ToString(), type = "join_request" },
                userId: admin.Id,
                type: "join_request",
                organizationId: organization.Id);
        }
    }

    private async Task NotifyRequesterOfDecisionAsync(Organization organization, Guid requesterId, bool approved, Guid adminUserId)
    {
        // The admin never gets a notification about their own action
        if (requesterId == adminUserId) return;

        var requester = await _context.Users.FirstOrDefaultAsync(u => u.Id == requesterId);
        if (requester == null || requester.IsGhostPlayer) return;

        var type = approved ? "join_request_approved" : "join_request_denied";

        await _notificationService.SendPushNotificationAsync(
            requester.PushToken ?? string.Empty,
            approved ? "Join Request Approved" : "Join Request Declined",
            approved
                ? $"You're now a member of {organization.Name}."
                : $"Your request to join {organization.Name} was declined.",
            new { organizationId = organization.Id.ToString(), type },
            userId: requester.Id,
            type: type,
            organizationId: organization.Id);
    }
}
