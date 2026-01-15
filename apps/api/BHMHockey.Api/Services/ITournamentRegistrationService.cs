using BHMHockey.Api.Models.DTOs;

namespace BHMHockey.Api.Services;

/// <summary>
/// Service for managing tournament registrations (individual player sign-ups).
/// </summary>
public interface ITournamentRegistrationService
{
    /// <summary>
    /// Registers a user for a tournament.
    /// Only allowed when tournament is in Open status and before registration deadline.
    /// </summary>
    /// <param name="tournamentId">The tournament to register for</param>
    /// <param name="request">Registration details including position and custom responses</param>
    /// <param name="userId">The user registering</param>
    /// <returns>Registration result with status and waitlist info if applicable</returns>
    /// <exception cref="InvalidOperationException">Thrown when registration is not allowed</exception>
    Task<TournamentRegistrationResultDto> RegisterAsync(Guid tournamentId, CreateTournamentRegistrationRequest request, Guid userId);

    /// <summary>
    /// Gets the current user's registration for a tournament.
    /// Returns null if not registered or registration is cancelled.
    /// </summary>
    /// <param name="tournamentId">The tournament to check</param>
    /// <param name="userId">The user to check registration for</param>
    /// <returns>Registration DTO or null if not registered</returns>
    Task<TournamentRegistrationDto?> GetMyRegistrationAsync(Guid tournamentId, Guid userId);

    /// <summary>
    /// Updates a user's registration details.
    /// Only allowed before registration deadline and by the registration owner (or admin).
    /// </summary>
    /// <param name="tournamentId">The tournament</param>
    /// <param name="request">Updated registration details</param>
    /// <param name="userId">The user making the update</param>
    /// <param name="targetUserId">Optional: for admin to update another user's registration</param>
    /// <returns>Updated registration DTO or null if not found</returns>
    /// <exception cref="InvalidOperationException">Thrown when update is not allowed (deadline passed)</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when user cannot update this registration</exception>
    Task<TournamentRegistrationDto?> UpdateAsync(Guid tournamentId, UpdateTournamentRegistrationRequest request, Guid userId, Guid? targetUserId = null);

    /// <summary>
    /// Withdraws (cancels) a registration.
    /// </summary>
    /// <param name="tournamentId">The tournament</param>
    /// <param name="userId">The user withdrawing</param>
    /// <param name="targetUserId">Optional: for admin to withdraw another user's registration</param>
    /// <returns>True if withdrawal succeeded, false if not registered</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when user cannot withdraw this registration</exception>
    Task<bool> WithdrawAsync(Guid tournamentId, Guid userId, Guid? targetUserId = null);

    /// <summary>
    /// Gets all registrations for a tournament (admin only).
    /// </summary>
    /// <param name="tournamentId">The tournament</param>
    /// <param name="userId">The admin user requesting the list</param>
    /// <returns>List of all registrations including user details</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when user is not a tournament admin</exception>
    Task<List<TournamentRegistrationDto>> GetAllAsync(Guid tournamentId, Guid userId);
}
