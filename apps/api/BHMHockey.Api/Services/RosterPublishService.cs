using BHMHockey.Api.Data;
using BHMHockey.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

/// <summary>
/// Service for handling automatic roster publishing and organizer reminders.
/// Sends reminders to organizers at 24h, 8h, 5h before event if roster not published.
/// Auto-publishes roster at 2h before event if organizer hasn't manually published.
/// </summary>
public class RosterPublishService : IRosterPublishService
{
    private readonly AppDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly ILogger<RosterPublishService> _logger;

    public RosterPublishService(
        AppDbContext context,
        INotificationService notificationService,
        ILogger<RosterPublishService> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task SendOrganizerPublishRemindersAsync()
    {
        var now = DateTime.UtcNow;

        // 24h reminder (24-25h window)
        await SendReminderIfDueAsync(
            now.AddHours(24), now.AddHours(25),
            e => e.OrganizerPublishReminder24hSentAt == null,
            (e, timestamp) => e.OrganizerPublishReminder24hSentAt = timestamp,
            "24 hours");

        // 8h reminder (8-9h window)
        await SendReminderIfDueAsync(
            now.AddHours(8), now.AddHours(9),
            e => e.OrganizerPublishReminder8hSentAt == null,
            (e, timestamp) => e.OrganizerPublishReminder8hSentAt = timestamp,
            "8 hours");

        // 5h reminder (5-6h window)
        await SendReminderIfDueAsync(
            now.AddHours(5), now.AddHours(6),
            e => e.OrganizerPublishReminder5hSentAt == null,
            (e, timestamp) => e.OrganizerPublishReminder5hSentAt = timestamp,
            "5 hours");
    }

    private async Task SendReminderIfDueAsync(
        DateTime windowStart,
        DateTime windowEnd,
        Func<Event, bool> notSentFilter,
        Action<Event, DateTime> markSent,
        string timeDescription)
    {
        var now = DateTime.UtcNow;

        // Query for unpublished events in the reminder window
        var events = await _context.Events
            .Include(e => e.Creator)
            .Where(e => !e.IsRosterPublished)
            .Where(e => e.Status == "Published")
            .Where(e => e.EventDate >= windowStart && e.EventDate < windowEnd)
            .ToListAsync();

        // Filter to only events that haven't had this reminder sent
        var eventsToRemind = events.Where(notSentFilter).ToList();

        foreach (var evt in eventsToRemind)
        {
            // Mark as sent regardless of push token (prevents re-query)
            markSent(evt, now);

            if (string.IsNullOrEmpty(evt.Creator.PushToken))
            {
                _logger.LogWarning(
                    "Cannot send {Time} publish reminder for event {EventId} - organizer has no push token",
                    timeDescription, evt.Id);
                await _context.SaveChangesAsync();
                continue;
            }

            var eventName = evt.Name ?? "Hockey Game";
            await _notificationService.SendPushNotificationAsync(
                evt.Creator.PushToken,
                "Roster Not Published",
                $"Your roster for {eventName} isn't published yet. Game starts in {timeDescription}!",
                new { eventId = evt.Id.ToString(), type = "publish_reminder" },
                userId: evt.CreatorId,
                type: "publish_reminder",
                organizationId: evt.OrganizationId,
                eventId: evt.Id);

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Sent {Time} publish reminder for event {EventId}",
                timeDescription, evt.Id);
        }
    }

    public async Task ProcessAutoPublishAsync()
    {
        var now = DateTime.UtcNow;
        var autoPublishCutoff = now.AddHours(2);

        // Find events within 2 hours of start that haven't been published
        var eventsToAutoPublish = await _context.Events
            .Include(e => e.Registrations)
                .ThenInclude(r => r.User)
            .Where(e => !e.IsRosterPublished)
            .Where(e => e.Status == "Published")
            .Where(e => e.EventDate > now)  // Not started yet
            .Where(e => e.EventDate <= autoPublishCutoff)  // Within 2 hours
            .ToListAsync();

        foreach (var evt in eventsToAutoPublish)
        {
            _logger.LogInformation(
                "Auto-publishing roster for event {EventId} starting at {EventDate}",
                evt.Id, evt.EventDate);

            // Publish the roster
            evt.IsRosterPublished = true;
            evt.PublishedAt = now;
            await _context.SaveChangesAsync();

            // Send notifications to all players
            await SendAutoPublishNotificationsAsync(evt);
        }
    }

    private async Task SendAutoPublishNotificationsAsync(Event evt)
    {
        var eventName = evt.Name ?? "Hockey Game";

        foreach (var registration in evt.Registrations)
        {
            string title, body;

            if (registration.Status == "Registered")
            {
                title = "Roster Published";
                body = registration.TeamAssignment != null
                    ? $"The roster for {eventName} is now live! You're on Team {registration.TeamAssignment}."
                    : $"The roster for {eventName} is now live! Check app for details.";
            }
            else if (registration.Status == "Waitlisted")
            {
                title = "Roster Published";
                body = $"The roster for {eventName} is now live. You're #{registration.WaitlistPosition} on the waitlist.";
            }
            else
            {
                // Skip cancelled registrations
                continue;
            }

            if (!string.IsNullOrEmpty(registration.User.PushToken))
            {
                await _notificationService.SendPushNotificationAsync(
                    registration.User.PushToken,
                    title,
                    body,
                    new { eventId = evt.Id.ToString(), type = "roster_auto_published" },
                    userId: registration.UserId,
                    type: "roster_auto_published",
                    organizationId: evt.OrganizationId,
                    eventId: evt.Id);
            }
        }

        _logger.LogInformation(
            "Sent auto-publish notifications for event {EventId} to {Count} registrations",
            evt.Id, evt.Registrations.Count);
    }
}
