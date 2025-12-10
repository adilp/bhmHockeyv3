using BHMHockey.Api.Models.Entities;

namespace BHMHockey.Api.Services;

public interface IWaitlistService
{
    /// <summary>
    /// Get the next waitlist position for an event
    /// </summary>
    Task<int> GetNextWaitlistPositionAsync(Guid eventId);

    /// <summary>
    /// Promote the next person from waitlist after a cancellation
    /// </summary>
    Task<EventRegistration?> PromoteNextFromWaitlistAsync(Guid eventId);

    /// <summary>
    /// Renumber waitlist positions after a promotion/cancellation
    /// </summary>
    Task UpdateWaitlistPositionsAsync(Guid eventId);

    /// <summary>
    /// Check and process expired payment deadlines
    /// </summary>
    Task ProcessExpiredPaymentDeadlinesAsync();

    /// <summary>
    /// Get current waitlist for an event
    /// </summary>
    Task<List<EventRegistration>> GetWaitlistAsync(Guid eventId);
}
