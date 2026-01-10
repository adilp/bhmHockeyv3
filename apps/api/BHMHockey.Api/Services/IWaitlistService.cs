using BHMHockey.Api.Models.DTOs;
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

    /// <summary>
    /// Get waitlisted users with verified payment status, ordered by registration time.
    /// Used for priority queue promotion (verified users promoted first).
    /// </summary>
    Task<List<EventRegistration>> GetVerifiedWaitlistUsersAsync(Guid eventId);

    /// <summary>
    /// Promotes users from waitlist using priority queue: verified users first (by RegisteredAt),
    /// then notifies unverified users of available spots (by WaitlistPosition).
    /// </summary>
    /// <param name="eventId">The event ID</param>
    /// <param name="spotCount">Number of spots to fill</param>
    /// <param name="callerOwnsTransaction">If true, notifications are deferred (caller sends after their commit)</param>
    /// <returns>PromotionResult with promoted registrations and pending notifications</returns>
    Task<PromotionResult> PromoteFromWaitlistAsync(
        Guid eventId,
        int spotCount = 1,
        bool callerOwnsTransaction = false);

    /// <summary>
    /// Sends pending notifications after a transaction has committed.
    /// </summary>
    Task SendPendingNotificationsAsync(List<PendingNotification> notifications);

    /// <summary>
    /// Reorders the entire waitlist for an event.
    /// Validates that all waitlisted users are included and positions are sequential starting from 1.
    /// Reordering affects notification priority for open spots, not auto-promotion order (which is by registration time).
    /// </summary>
    /// <param name="eventId">The event ID</param>
    /// <param name="items">List of registration IDs with their new positions</param>
    /// <returns>True if reorder was successful</returns>
    /// <exception cref="InvalidOperationException">Thrown if validation fails</exception>
    Task<bool> ReorderWaitlistAsync(Guid eventId, List<WaitlistReorderItem> items);
}
