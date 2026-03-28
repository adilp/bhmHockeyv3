using BHMHockey.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

public class NotFullGameReminderService : INotFullGameReminderService
{
    private readonly AppDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotFullGameReminderService> _logger;

    private static readonly TimeZoneInfo CentralTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Chicago");
    private static readonly TimeSpan ReminderWindow = TimeSpan.FromHours(48);

    public NotFullGameReminderService(
        AppDbContext context,
        INotificationService notificationService,
        ILogger<NotFullGameReminderService> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task SendNotFullRemindersAsync()
    {
        var now = DateTime.UtcNow;
        var reminderCutoff = now.Add(ReminderWindow);

        // Find events within 48h that haven't had this reminder sent
        var events = await _context.Events
            .Include(e => e.Registrations)
            .Where(e => e.EventDate > now)
            .Where(e => e.EventDate <= reminderCutoff)
            .Where(e => e.Status != "Draft" && e.Status != "Cancelled" && e.Status != "Completed")
            .Where(e => e.Visibility != "InviteOnly")
            .Where(e => e.NotFullReminderSentAt == null)
            .Where(e => e.RegistrationDeadline == null || e.RegistrationDeadline > now)
            .ToListAsync();

        foreach (var evt in events)
        {
            // Goalies don't count against MaxPlayers — match EventService logic
            var skaterCount = evt.Registrations
                .Count(r => r.Status == "Registered" && r.RegisteredPosition != "Goalie");

            if (skaterCount >= evt.MaxPlayers)
            {
                // Event is full, skip
                continue;
            }

            var spotsRemaining = evt.MaxPlayers - skaterCount;

            // Get user IDs already on roster or waitlist (exclude cancelled)
            var excludedUserIds = evt.Registrations
                .Where(r => r.Status == "Registered" || r.Status == "Waitlisted")
                .Select(r => r.UserId)
                .ToHashSet();

            // Get eligible recipients based on visibility
            List<string> pushTokens;

            if (evt.Visibility == "OrganizationMembers" && evt.OrganizationId.HasValue)
            {
                // Org event: only org subscribers with notifications enabled
                pushTokens = await _context.OrganizationSubscriptions
                    .Where(s => s.OrganizationId == evt.OrganizationId.Value)
                    .Where(s => s.NotificationEnabled)
                    .Where(s => !excludedUserIds.Contains(s.UserId))
                    .Join(_context.Users.Where(u =>
                        u.IsActive &&
                        !u.IsGhostPlayer &&
                        u.PushToken != null),
                        s => s.UserId,
                        u => u.Id,
                        (s, u) => u.PushToken!)
                    .ToListAsync();
            }
            else
            {
                // Public event: all eligible users
                pushTokens = await _context.Users
                    .Where(u => u.IsActive)
                    .Where(u => !u.IsGhostPlayer)
                    .Where(u => u.PushToken != null)
                    .Where(u => !excludedUserIds.Contains(u.Id))
                    .Select(u => u.PushToken!)
                    .ToListAsync();
            }

            if (!pushTokens.Any())
            {
                _logger.LogInformation(
                    "No recipients for not-full reminder for event {EventId}", evt.Id);
                evt.NotFullReminderSentAt = now;
                await _context.SaveChangesAsync();
                continue;
            }

            var eventName = evt.Name ?? "Hockey Game";
            var localEventDate = TimeZoneInfo.ConvertTimeFromUtc(evt.EventDate, CentralTimeZone);
            var eventTime = localEventDate.ToString("dddd, M/d 'at' h:mm tt");
            var spotText = spotsRemaining == 1 ? "1 spot" : $"{spotsRemaining} spots";

            await _notificationService.SendBatchPushNotificationsAsync(
                pushTokens,
                "Spots Available!",
                $"{eventName} on {eventTime} still has {spotText} open.",
                new { eventId = evt.Id.ToString(), type = "not_full_reminder" });

            evt.NotFullReminderSentAt = now;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Sent not-full reminder for event {EventId} to {Count} users ({SpotsRemaining} spots remaining)",
                evt.Id, pushTokens.Count, spotsRemaining);
        }
    }
}
