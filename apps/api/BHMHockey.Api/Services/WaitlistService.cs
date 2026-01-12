using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

public class WaitlistService : IWaitlistService
{
    private readonly AppDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly ILogger<WaitlistService> _logger;

    // Payment deadline duration (2 hours)
    private static readonly TimeSpan PaymentDeadlineDuration = TimeSpan.FromHours(2);

    public WaitlistService(
        AppDbContext context,
        INotificationService notificationService,
        ILogger<WaitlistService> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<int> GetNextWaitlistPositionAsync(Guid eventId)
    {
        var maxPosition = await _context.EventRegistrations
            .Where(r => r.EventId == eventId && r.Status == "Waitlisted")
            .MaxAsync(r => (int?)r.WaitlistPosition) ?? 0;

        return maxPosition + 1;
    }

    public async Task<EventRegistration?> PromoteNextFromWaitlistAsync(Guid eventId)
    {
        var nextInLine = await _context.EventRegistrations
            .Where(r => r.EventId == eventId && r.Status == "Waitlisted")
            .OrderBy(r => r.WaitlistPosition)
            .Include(r => r.User)
            .Include(r => r.Event)
                .ThenInclude(e => e.Creator)
            .FirstOrDefaultAsync();

        if (nextInLine == null)
        {
            _logger.LogInformation("No waitlisted users to promote for event {EventId}", eventId);
            return null;
        }

        // Promote the user
        nextInLine.Status = "Registered";
        nextInLine.WaitlistPosition = null;
        nextInLine.PromotedAt = DateTime.UtcNow;

        // Set payment deadline if event has a cost
        if (nextInLine.Event.Cost > 0)
        {
            nextInLine.PaymentDeadlineAt = DateTime.UtcNow.Add(PaymentDeadlineDuration);
        }

        // Assign team based on position balance (same logic as regular registration)
        nextInLine.TeamAssignment = await DetermineTeamAssignmentAsync(eventId, nextInLine.RegisteredPosition);

        await _context.SaveChangesAsync();

        // Send push notifications
        await NotifyUserAutoPromotedAsync(nextInLine);
        await NotifyOrganizerAutoPromotionAsync(nextInLine);

        // Update remaining waitlist positions
        await UpdateWaitlistPositionsAsync(eventId);

        _logger.LogInformation(
            "Promoted user {UserId} from waitlist for event {EventId}. Payment deadline: {Deadline}",
            nextInLine.UserId, eventId, nextInLine.PaymentDeadlineAt);

        return nextInLine;
    }

    public async Task UpdateWaitlistPositionsAsync(Guid eventId)
    {
        var waitlistedRegistrations = await _context.EventRegistrations
            .Where(r => r.EventId == eventId && r.Status == "Waitlisted")
            .OrderBy(r => r.WaitlistPosition)
            .ToListAsync();

        // Renumber positions starting from 1
        for (int i = 0; i < waitlistedRegistrations.Count; i++)
        {
            waitlistedRegistrations[i].WaitlistPosition = i + 1;
        }

        await _context.SaveChangesAsync();
    }

    public async Task ProcessExpiredPaymentDeadlinesAsync()
    {
        var expiredRegistrations = await _context.EventRegistrations
            .Where(r => r.Status == "Registered")
            .Where(r => r.PaymentDeadlineAt != null)
            .Where(r => r.PaymentDeadlineAt < DateTime.UtcNow)
            .Where(r => r.PaymentStatus == "Pending" || r.PaymentStatus == null)
            .Include(r => r.User)
            .Include(r => r.Event)
            .ToListAsync();

        foreach (var registration in expiredRegistrations)
        {
            _logger.LogInformation(
                "Payment deadline expired for user {UserId} on event {EventId}. Cancelling registration.",
                registration.UserId, registration.EventId);

            // Cancel the registration
            registration.Status = "Cancelled";
            registration.PaymentDeadlineAt = null;

            await _context.SaveChangesAsync();

            // Notify user their registration was cancelled
            await NotifyRegistrationExpiredAsync(registration);

            // Promote next verified user from waitlist (or notify unverified users of spot)
            var promotionResult = await PromoteFromWaitlistAsync(registration.EventId, spotCount: 1);
            await SendPendingNotificationsAsync(promotionResult.PendingNotifications);
        }
    }

    public async Task<List<EventRegistration>> GetWaitlistAsync(Guid eventId)
    {
        return await _context.EventRegistrations
            .Where(r => r.EventId == eventId && r.Status == "Waitlisted")
            .OrderBy(r => r.WaitlistPosition)
            .Include(r => r.User)
            .ToListAsync();
    }

    public async Task<List<EventRegistration>> GetVerifiedWaitlistUsersAsync(Guid eventId)
    {
        return await _context.EventRegistrations
            .Include(r => r.User)
            .Where(r => r.EventId == eventId
                     && r.Status == "Waitlisted"
                     && r.PaymentStatus == "Verified")
            .OrderBy(r => r.RegisteredAt)
            .ThenBy(r => r.Id)
            .ToListAsync();
    }

    /// <summary>
    /// Determines team assignment based on current team balance by position.
    /// Same logic as EventService - assigns to the team with fewer players of the same position.
    /// </summary>
    private async Task<string> DetermineTeamAssignmentAsync(Guid eventId, string? registeredPosition)
    {
        var blackCount = await _context.EventRegistrations
            .CountAsync(r => r.EventId == eventId && r.Status == "Registered"
                && r.TeamAssignment == "Black" && r.RegisteredPosition == registeredPosition);
        var whiteCount = await _context.EventRegistrations
            .CountAsync(r => r.EventId == eventId && r.Status == "Registered"
                && r.TeamAssignment == "White" && r.RegisteredPosition == registeredPosition);

        return blackCount <= whiteCount ? "Black" : "White";
    }

    private async Task NotifyUserAutoPromotedAsync(EventRegistration registration)
    {
        if (string.IsNullOrEmpty(registration.User.PushToken))
        {
            return;
        }

        var deadlineText = registration.PaymentDeadlineAt.HasValue
            ? " Pay within 2 hours to secure your spot!"
            : "";

        var eventName = registration.Event.Name ?? $"Event on {registration.Event.EventDate:MMM d}";

        await _notificationService.SendPushNotificationAsync(
            registration.User.PushToken,
            "You're In!",
            $"A spot opened up for {eventName}.{deadlineText}",
            new { eventId = registration.EventId.ToString(), type = "auto_promoted" },
            userId: registration.UserId,
            type: "auto_promoted",
            organizationId: registration.Event.OrganizationId,
            eventId: registration.EventId);
    }

    private async Task NotifyPaymentReminderAsync(EventRegistration registration)
    {
        if (string.IsNullOrEmpty(registration.User.PushToken))
        {
            return;
        }

        var eventName = registration.Event.Name ?? $"Event on {registration.Event.EventDate:MMM d}";

        await _notificationService.SendPushNotificationAsync(
            registration.User.PushToken,
            "Payment Reminder",
            $"Don't forget to pay for {eventName}! Pay now to secure your spot.",
            new { eventId = registration.EventId.ToString(), type = "payment_reminder" },
            userId: registration.UserId,
            type: "payment_reminder",
            organizationId: registration.Event.OrganizationId,
            eventId: registration.EventId);
    }

    private async Task NotifyRegistrationExpiredAsync(EventRegistration registration)
    {
        if (string.IsNullOrEmpty(registration.User.PushToken))
        {
            return;
        }

        var eventName = registration.Event.Name ?? $"Event on {registration.Event.EventDate:MMM d}";

        await _notificationService.SendPushNotificationAsync(
            registration.User.PushToken,
            "Registration Expired",
            $"Your registration for {eventName} was cancelled due to missed payment deadline.",
            new { eventId = registration.EventId.ToString(), type = "registration_expired" },
            userId: registration.UserId,
            type: "registration_expired",
            organizationId: registration.Event.OrganizationId,
            eventId: registration.EventId);
    }

    private async Task NotifyOrganizerAutoPromotionAsync(EventRegistration registration)
    {
        if (string.IsNullOrEmpty(registration.Event.Creator.PushToken))
        {
            return;
        }

        var eventName = registration.Event.Name ?? $"Event on {registration.Event.EventDate:MMM d}";
        var userName = $"{registration.User.FirstName} {registration.User.LastName}".Trim();
        if (string.IsNullOrEmpty(userName)) userName = registration.User.Email;

        await _notificationService.SendPushNotificationAsync(
            registration.Event.Creator.PushToken,
            "Auto-Promotion",
            $"{userName} was auto-promoted from the waitlist for {eventName}",
            new { eventId = registration.EventId.ToString(), type = "auto_promotion" },
            userId: registration.Event.CreatorId,
            type: "auto_promotion",
            organizationId: registration.Event.OrganizationId,
            eventId: registration.EventId);
    }

    private async Task NotifyOrganizerPaymentMarkedAsync(EventRegistration registration)
    {
        if (string.IsNullOrEmpty(registration.Event.Creator.PushToken))
        {
            return;
        }

        var eventName = registration.Event.Name ?? $"Event on {registration.Event.EventDate:MMM d}";
        var userName = $"{registration.User.FirstName} {registration.User.LastName}".Trim();
        if (string.IsNullOrEmpty(userName)) userName = registration.User.Email;

        await _notificationService.SendPushNotificationAsync(
            registration.Event.Creator.PushToken,
            "Payment Marked",
            $"{userName} marked payment for {eventName}",
            new { eventId = registration.EventId.ToString(), type = "payment_marked" },
            userId: registration.Event.CreatorId,
            type: "payment_marked",
            organizationId: registration.Event.OrganizationId,
            eventId: registration.EventId);
    }

    private async Task NotifyUserPaymentVerifiedButFullAsync(EventRegistration registration)
    {
        if (string.IsNullOrEmpty(registration.User.PushToken))
        {
            return;
        }

        var eventName = registration.Event.Name ?? $"Event on {registration.Event.EventDate:MMM d}";

        await _notificationService.SendPushNotificationAsync(
            registration.User.PushToken,
            "Payment Verified - On Waitlist",
            $"Your payment for {eventName} was verified. You're on the priority waitlist and will be added when a spot opens.",
            new { eventId = registration.EventId.ToString(), type = "payment_verified_waitlist" },
            userId: registration.UserId,
            type: "payment_verified_waitlist",
            organizationId: registration.Event.OrganizationId,
            eventId: registration.EventId);
    }

    private async Task NotifySpotAvailableAsync(EventRegistration registration)
    {
        if (string.IsNullOrEmpty(registration.User.PushToken))
        {
            return;
        }

        var eventName = registration.Event.Name ?? $"Event on {registration.Event.EventDate:MMM d}";

        await _notificationService.SendPushNotificationAsync(
            registration.User.PushToken,
            "Spot Available!",
            $"A spot opened up for {eventName}. Complete payment to secure your spot!",
            new { eventId = registration.EventId.ToString(), type = "spot_available" },
            userId: registration.UserId,
            type: "spot_available",
            organizationId: registration.Event.OrganizationId,
            eventId: registration.EventId);
    }

    /// <summary>
    /// Promotes users from waitlist using priority queue: verified users first (by RegisteredAt),
    /// then notifies unverified users of available spots (by WaitlistPosition).
    /// </summary>
    /// <param name="eventId">The event ID</param>
    /// <param name="spotCount">Number of spots to fill</param>
    /// <param name="callerOwnsTransaction">If true, notifications are deferred (caller sends after their commit)</param>
    /// <returns>PromotionResult with promoted registrations and pending notifications</returns>
    public async Task<PromotionResult> PromoteFromWaitlistAsync(
        Guid eventId,
        int spotCount = 1,
        bool callerOwnsTransaction = false)
    {
        var result = new PromotionResult();

        // Get verified waitlist users ordered by RegisteredAt, then by Id
        var verifiedUsers = await _context.EventRegistrations
            .Include(r => r.User)
            .Include(r => r.Event)
                .ThenInclude(e => e.Creator)
            .Where(r => r.EventId == eventId
                     && r.Status == "Waitlisted"
                     && r.PaymentStatus == "Verified")
            .OrderBy(r => r.RegisteredAt)
            .ThenBy(r => r.Id)
            .ToListAsync();

        var spotsRemaining = spotCount;

        // Promote verified users first
        foreach (var registration in verifiedUsers.Take(spotsRemaining))
        {
            registration.Status = "Registered";
            registration.WaitlistPosition = null;
            registration.PromotedAt = DateTime.UtcNow;
            registration.PaymentDeadlineAt = null; // Already paid
            registration.TeamAssignment = await DetermineTeamAssignmentAsync(eventId, registration.RegisteredPosition);

            result.Promoted.Add(registration);
            result.PendingNotifications.Add(new PendingNotification
            {
                User = registration.User,
                Event = registration.Event,
                Organizer = registration.Event.Creator,
                Type = NotificationType.AutoPromoted
            });

            spotsRemaining--;
        }

        // If spots remain, notify unverified users (by WaitlistPosition order)
        if (spotsRemaining > 0)
        {
            var unverifiedUsers = await _context.EventRegistrations
                .Include(r => r.User)
                .Include(r => r.Event)
                    .ThenInclude(e => e.Creator)
                .Where(r => r.EventId == eventId
                         && r.Status == "Waitlisted"
                         && r.PaymentStatus != "Verified")
                .OrderBy(r => r.WaitlistPosition)
                .ThenBy(r => r.Id)
                .Take(spotsRemaining)
                .ToListAsync();

            foreach (var registration in unverifiedUsers)
            {
                result.PendingNotifications.Add(new PendingNotification
                {
                    User = registration.User,
                    Event = registration.Event,
                    Organizer = registration.Event.Creator,
                    Type = NotificationType.SpotAvailable
                });
            }
        }

        await _context.SaveChangesAsync();

        // Update waitlist positions
        await UpdateWaitlistPositionsAsync(eventId);

        // Send notifications if we own the transaction
        if (!callerOwnsTransaction)
        {
            await SendPendingNotificationsAsync(result.PendingNotifications);
        }

        _logger.LogInformation(
            "PromoteFromWaitlistAsync: Promoted {PromotedCount} users, notified {NotifiedCount} for event {EventId}",
            result.Promoted.Count, result.PendingNotifications.Count, eventId);

        return result;
    }

    /// <summary>
    /// Sends pending notifications after a transaction has committed.
    /// </summary>
    public async Task SendPendingNotificationsAsync(List<PendingNotification> notifications)
    {
        foreach (var notification in notifications)
        {
            if (notification.Type == NotificationType.AutoPromoted)
            {
                // Send auto-promoted notification to user
                if (!string.IsNullOrEmpty(notification.User.PushToken))
                {
                    var eventName = notification.Event.Name ?? $"Event on {notification.Event.EventDate:MMM d}";
                    await _notificationService.SendPushNotificationAsync(
                        notification.User.PushToken,
                        "You're In!",
                        $"A spot opened up for {eventName}. You've been promoted from the waitlist!",
                        new { eventId = notification.Event.Id.ToString(), type = "auto_promoted" },
                        userId: notification.User.Id,
                        type: "auto_promoted",
                        organizationId: notification.Event.OrganizationId,
                        eventId: notification.Event.Id);
                }

                // Send organizer notification
                if (!string.IsNullOrEmpty(notification.Organizer?.PushToken))
                {
                    var eventName = notification.Event.Name ?? $"Event on {notification.Event.EventDate:MMM d}";
                    var userName = $"{notification.User.FirstName} {notification.User.LastName}".Trim();
                    if (string.IsNullOrEmpty(userName)) userName = notification.User.Email;

                    await _notificationService.SendPushNotificationAsync(
                        notification.Organizer.PushToken,
                        "Auto-Promotion",
                        $"{userName} was auto-promoted from the waitlist for {eventName}",
                        new { eventId = notification.Event.Id.ToString(), type = "auto_promotion" },
                        userId: notification.Organizer.Id,
                        type: "auto_promotion",
                        organizationId: notification.Event.OrganizationId,
                        eventId: notification.Event.Id);
                }
            }
            else if (notification.Type == NotificationType.SpotAvailable)
            {
                // Send spot available notification to unverified user
                if (!string.IsNullOrEmpty(notification.User.PushToken))
                {
                    var eventName = notification.Event.Name ?? $"Event on {notification.Event.EventDate:MMM d}";
                    await _notificationService.SendPushNotificationAsync(
                        notification.User.PushToken,
                        "Spot Available!",
                        $"A spot opened up for {eventName}. Complete payment to secure your spot!",
                        new { eventId = notification.Event.Id.ToString(), type = "spot_available" },
                        userId: notification.User.Id,
                        type: "spot_available",
                        organizationId: notification.Event.OrganizationId,
                        eventId: notification.Event.Id);
                }
            }
        }
    }

    public async Task<bool> ReorderWaitlistAsync(Guid eventId, List<WaitlistReorderItem> items)
    {
        // Get current waitlist for this event
        var waitlist = await _context.EventRegistrations
            .Where(r => r.EventId == eventId && r.Status == "Waitlisted")
            .ToListAsync();

        // Validation 1: All waitlisted users must be included
        var itemIds = items.Select(i => i.RegistrationId).ToHashSet();
        var waitlistIds = waitlist.Select(w => w.Id).ToHashSet();
        if (!itemIds.SetEquals(waitlistIds))
        {
            throw new InvalidOperationException("All waitlisted users must be included");
        }

        // Validation 2: Positions must be sequential starting from 1
        var positions = items.Select(i => i.Position).OrderBy(p => p).ToList();
        var expected = Enumerable.Range(1, items.Count).ToList();
        if (!positions.SequenceEqual(expected))
        {
            throw new InvalidOperationException("Positions must be sequential starting from 1");
        }

        // Apply new positions
        foreach (var item in items)
        {
            var registration = waitlist.First(r => r.Id == item.RegistrationId);
            registration.WaitlistPosition = item.Position;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Reordered waitlist for event {EventId}: {ItemCount} positions updated",
            eventId, items.Count);

        return true;
    }
}

/// <summary>
/// Result of a waitlist promotion operation.
/// </summary>
public class PromotionResult
{
    /// <summary>
    /// Registrations that were promoted from waitlist to registered.
    /// </summary>
    public List<EventRegistration> Promoted { get; set; } = new();

    /// <summary>
    /// Notifications pending to be sent (auto-promoted or spot-available).
    /// </summary>
    public List<PendingNotification> PendingNotifications { get; set; } = new();
}

/// <summary>
/// A notification pending to be sent after transaction commit.
/// </summary>
public class PendingNotification
{
    public User User { get; set; } = null!;
    public Event Event { get; set; } = null!;
    public User? Organizer { get; set; }
    public NotificationType Type { get; set; }
}

/// <summary>
/// Type of notification to send.
/// </summary>
public enum NotificationType
{
    /// <summary>User was auto-promoted from waitlist.</summary>
    AutoPromoted,
    /// <summary>Spot became available for unverified user.</summary>
    SpotAvailable
}
