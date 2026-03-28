namespace BHMHockey.Api.Services;

public interface INotFullGameReminderService
{
    /// <summary>
    /// Send push notifications to non-registered users when a game is not full,
    /// approximately 48 hours before event start. Org events notify org subscribers;
    /// public events notify all eligible users. Push-only (no in-app persistence).
    /// </summary>
    Task SendNotFullRemindersAsync();
}
