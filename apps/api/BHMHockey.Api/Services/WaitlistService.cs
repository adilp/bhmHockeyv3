using BHMHockey.Api.Data;
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
        await NotifyUserPromotedAsync(nextInLine);
        await NotifyOrganizerUserPromotedAsync(nextInLine);

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
                "Payment deadline expired for user {UserId} on event {EventId}. Sending reminder.",
                registration.UserId, registration.EventId);

            // Clear the deadline so we only send one reminder
            registration.PaymentDeadlineAt = null;

            // Send payment reminder
            await NotifyPaymentReminderAsync(registration);

            await _context.SaveChangesAsync();
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

    private async Task NotifyUserPromotedAsync(EventRegistration registration)
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
            new { eventId = registration.EventId.ToString(), type = "waitlist_promoted" });
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
            new { eventId = registration.EventId.ToString(), type = "payment_reminder" });
    }

    private async Task NotifyOrganizerUserPromotedAsync(EventRegistration registration)
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
            "Waitlist Promotion",
            $"{userName} was promoted from the waitlist for {eventName}",
            new { eventId = registration.EventId.ToString(), type = "waitlist_promotion" });
    }
}
