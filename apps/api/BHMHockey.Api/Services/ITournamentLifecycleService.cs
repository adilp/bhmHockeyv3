using BHMHockey.Api.Models.DTOs;

namespace BHMHockey.Api.Services;

/// <summary>
/// Service for managing tournament lifecycle state transitions.
/// Each method transitions the tournament to a new state with appropriate validation,
/// timestamp updates, and audit logging.
/// </summary>
public interface ITournamentLifecycleService
{
    /// <summary>
    /// Publishes a tournament, transitioning from Draft to Open.
    /// Makes the tournament visible to the public and opens registration.
    /// </summary>
    /// <param name="tournamentId">The tournament ID</param>
    /// <param name="userId">The user performing the action (must be tournament admin)</param>
    /// <returns>Updated tournament DTO</returns>
    /// <exception cref="InvalidOperationException">If tournament not found or invalid transition</exception>
    /// <exception cref="UnauthorizedAccessException">If user is not a tournament admin</exception>
    Task<TournamentDto> PublishAsync(Guid tournamentId, Guid userId);

    /// <summary>
    /// Closes registration for a tournament, transitioning from Open to RegistrationClosed.
    /// No new registrations will be accepted after this.
    /// </summary>
    /// <param name="tournamentId">The tournament ID</param>
    /// <param name="userId">The user performing the action (must be tournament admin)</param>
    /// <returns>Updated tournament DTO</returns>
    Task<TournamentDto> CloseRegistrationAsync(Guid tournamentId, Guid userId);

    /// <summary>
    /// Starts a tournament, transitioning from RegistrationClosed to InProgress.
    /// Locks the bracket/schedule for the tournament.
    /// </summary>
    /// <param name="tournamentId">The tournament ID</param>
    /// <param name="userId">The user performing the action (must be tournament admin)</param>
    /// <returns>Updated tournament DTO</returns>
    Task<TournamentDto> StartAsync(Guid tournamentId, Guid userId);

    /// <summary>
    /// Completes a tournament, transitioning from InProgress to Completed.
    /// This is a terminal state - no further transitions allowed.
    /// </summary>
    /// <param name="tournamentId">The tournament ID</param>
    /// <param name="userId">The user performing the action (must be tournament admin)</param>
    /// <returns>Updated tournament DTO</returns>
    Task<TournamentDto> CompleteAsync(Guid tournamentId, Guid userId);

    /// <summary>
    /// Postpones a tournament, transitioning from InProgress to Postponed.
    /// Optionally sets new start and end dates.
    /// </summary>
    /// <param name="tournamentId">The tournament ID</param>
    /// <param name="userId">The user performing the action (must be tournament admin)</param>
    /// <param name="newStartDate">Optional new start date</param>
    /// <param name="newEndDate">Optional new end date</param>
    /// <returns>Updated tournament DTO</returns>
    Task<TournamentDto> PostponeAsync(Guid tournamentId, Guid userId, DateTime? newStartDate = null, DateTime? newEndDate = null);

    /// <summary>
    /// Resumes a postponed tournament, transitioning from Postponed to InProgress.
    /// </summary>
    /// <param name="tournamentId">The tournament ID</param>
    /// <param name="userId">The user performing the action (must be tournament admin)</param>
    /// <returns>Updated tournament DTO</returns>
    Task<TournamentDto> ResumeAsync(Guid tournamentId, Guid userId);

    /// <summary>
    /// Cancels a tournament, transitioning from any state (except Completed) to Cancelled.
    /// This is a terminal state - no further transitions allowed.
    /// </summary>
    /// <param name="tournamentId">The tournament ID</param>
    /// <param name="userId">The user performing the action (must be tournament admin)</param>
    /// <returns>Updated tournament DTO</returns>
    Task<TournamentDto> CancelAsync(Guid tournamentId, Guid userId);
}
