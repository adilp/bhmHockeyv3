namespace BHMHockey.Api.Services;

/// <summary>
/// Service for handling automatic roster publishing and organizer reminders.
/// </summary>
public interface IRosterPublishService
{
    /// <summary>
    /// Send organizer reminders for unpublished events at 24h, 8h, 5h before event.
    /// </summary>
    Task SendOrganizerPublishRemindersAsync();

    /// <summary>
    /// Auto-publish rosters for events starting within 2 hours that haven't been published.
    /// </summary>
    Task ProcessAutoPublishAsync();
}
