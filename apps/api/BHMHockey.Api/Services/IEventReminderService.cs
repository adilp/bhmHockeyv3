namespace BHMHockey.Api.Services;

public interface IEventReminderService
{
    /// <summary>
    /// Send game reminders to registered players 1 hour before event starts.
    /// Includes game time, venue, and team assignment.
    /// </summary>
    Task SendPlayerRemindersAsync();

    /// <summary>
    /// Send payment reminders to organizers 5 hours before event starts
    /// if there are any players with pending payments.
    /// </summary>
    Task SendOrganizerPaymentRemindersAsync();
}
