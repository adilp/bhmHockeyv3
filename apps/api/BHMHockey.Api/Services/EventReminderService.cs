using BHMHockey.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

public class EventReminderService : IEventReminderService
{
    private readonly AppDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly ILogger<EventReminderService> _logger;

    // Central Time Zone for displaying times to users (local community app)
    private static readonly TimeZoneInfo CentralTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");

    // Send player reminders 1 hour before event
    private static readonly TimeSpan PlayerReminderWindow = TimeSpan.FromHours(1);
    // Send organizer payment reminders 5 hours before event
    private static readonly TimeSpan OrganizerPaymentReminderWindow = TimeSpan.FromHours(5);

    public EventReminderService(
        AppDbContext context,
        INotificationService notificationService,
        ILogger<EventReminderService> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task SendPlayerRemindersAsync()
    {
        var now = DateTime.UtcNow;
        var reminderCutoff = now.Add(PlayerReminderWindow);

        // Find events starting within the next hour that haven't had reminders sent
        var eventsToRemind = await _context.Events
            .Include(e => e.Registrations)
                .ThenInclude(r => r.User)
            .Where(e => e.Status == "Published")
            .Where(e => e.EventDate > now) // Event hasn't started yet
            .Where(e => e.EventDate <= reminderCutoff) // Starting within 1 hour
            .Where(e => e.PlayerReminderSentAt == null) // Reminder not sent yet
            .ToListAsync();

        foreach (var evt in eventsToRemind)
        {
            _logger.LogInformation(
                "Sending player reminders for event {EventId} starting at {EventDate}",
                evt.Id, evt.EventDate);

            var eventName = evt.Name ?? $"Hockey Game";
            // Convert UTC to Central Time for display
            var localEventDate = TimeZoneInfo.ConvertTimeFromUtc(evt.EventDate, CentralTimeZone);
            var eventTime = localEventDate.ToString("h:mm tt");

            // Send to each registered player with a push token
            var registeredPlayers = evt.Registrations
                .Where(r => r.Status == "Registered" && !string.IsNullOrEmpty(r.User.PushToken))
                .ToList();

            foreach (var registration in registeredPlayers)
            {
                var teamInfo = registration.TeamAssignment != null
                    ? $"You're on Team {registration.TeamAssignment}."
                    : "";

                await _notificationService.SendPushNotificationAsync(
                    registration.User.PushToken!,
                    "Game Starting Soon!",
                    $"{eventName} starts at {eventTime}. {teamInfo}",
                    new { eventId = evt.Id.ToString(), type = "game_reminder" },
                    userId: registration.UserId,
                    type: "game_reminder",
                    organizationId: evt.OrganizationId,
                    eventId: evt.Id);
            }

            // Mark reminder as sent
            evt.PlayerReminderSentAt = now;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Sent player reminders to {Count} players for event {EventId}",
                registeredPlayers.Count, evt.Id);
        }
    }

    public async Task SendOrganizerPaymentRemindersAsync()
    {
        var now = DateTime.UtcNow;
        var reminderCutoff = now.Add(OrganizerPaymentReminderWindow);

        // Find events with cost, starting within 5 hours, that haven't had organizer reminders sent
        var eventsToRemind = await _context.Events
            .Include(e => e.Creator)
            .Include(e => e.Registrations)
            .Where(e => e.Status == "Published")
            .Where(e => e.Cost > 0) // Only events with a cost
            .Where(e => e.EventDate > now) // Event hasn't started yet
            .Where(e => e.EventDate <= reminderCutoff) // Starting within 5 hours
            .Where(e => e.OrganizerPaymentReminderSentAt == null) // Reminder not sent yet
            .ToListAsync();

        foreach (var evt in eventsToRemind)
        {
            // Count unpaid registrations (Pending or MarkedPaid but not Verified)
            var unpaidCount = evt.Registrations
                .Count(r => r.Status == "Registered" && r.PaymentStatus != "Verified");

            if (unpaidCount == 0)
            {
                // No unpaid players, mark as sent and skip
                evt.OrganizerPaymentReminderSentAt = now;
                await _context.SaveChangesAsync();
                continue;
            }

            // Only send if organizer has push token
            if (string.IsNullOrEmpty(evt.Creator.PushToken))
            {
                _logger.LogWarning(
                    "Cannot send organizer payment reminder for event {EventId} - no push token",
                    evt.Id);
                evt.OrganizerPaymentReminderSentAt = now;
                await _context.SaveChangesAsync();
                continue;
            }

            var eventName = evt.Name ?? $"Hockey Game";
            var playerText = unpaidCount == 1 ? "1 player has" : $"{unpaidCount} players have";

            await _notificationService.SendPushNotificationAsync(
                evt.Creator.PushToken,
                "Unpaid Players Reminder",
                $"{playerText} not paid for {eventName}. Game starts in less than 5 hours!",
                new { eventId = evt.Id.ToString(), type = "organizer_payment_reminder" },
                userId: evt.CreatorId,
                type: "organizer_payment_reminder",
                organizationId: evt.OrganizationId,
                eventId: evt.Id);

            evt.OrganizerPaymentReminderSentAt = now;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Sent organizer payment reminder for event {EventId} - {UnpaidCount} unpaid players",
                evt.Id, unpaidCount);
        }
    }
}
