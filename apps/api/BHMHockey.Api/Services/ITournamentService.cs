using BHMHockey.Api.Models.DTOs;

namespace BHMHockey.Api.Services;

public interface ITournamentService
{
    /// <summary>
    /// Creates a new tournament in Draft status.
    /// If organizationId is provided, requires the creator to be an org admin.
    /// Automatically creates a TournamentAdmin entry with Owner role for the creator.
    /// </summary>
    Task<TournamentDto> CreateAsync(CreateTournamentRequest request, Guid creatorId);

    /// <summary>
    /// Gets all public tournaments (Open, InProgress, Completed statuses).
    /// Does not include Draft, RegistrationClosed, Postponed, or Cancelled tournaments.
    /// </summary>
    Task<List<TournamentDto>> GetAllAsync(Guid? currentUserId = null);

    /// <summary>
    /// Gets a tournament by ID.
    /// Returns null if not found.
    /// </summary>
    Task<TournamentDto?> GetByIdAsync(Guid id, Guid? currentUserId = null);

    /// <summary>
    /// Updates a tournament's settings.
    /// Only allowed when tournament is in Draft or Open status.
    /// Requires the user to be a tournament admin.
    /// Returns null if tournament not found.
    /// </summary>
    Task<TournamentDto?> UpdateAsync(Guid id, UpdateTournamentRequest request, Guid userId);

    /// <summary>
    /// Deletes a tournament (hard delete).
    /// Only allowed when tournament is in Draft status.
    /// Requires the user to be a tournament admin.
    /// Returns true if deleted, false if not found.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, Guid userId);

    /// <summary>
    /// Checks if a user can manage the tournament (is a tournament admin).
    /// </summary>
    Task<bool> CanUserManageTournamentAsync(Guid tournamentId, Guid userId);

    /// <summary>
    /// Gets all tournaments a user has participated in, organized, or is currently active in.
    /// When viewing own profile (userId == currentUserId), returns Active, Past, and Organizing sections.
    /// When viewing another user's profile (public view), returns only Past section.
    /// </summary>
    /// <param name="userId">The user whose tournaments to retrieve</param>
    /// <param name="currentUserId">The current authenticated user (null for public view)</param>
    /// <param name="filter">Optional filter for "won" or by year</param>
    Task<MyTournamentsResponseDto> GetUserTournamentsAsync(Guid userId, Guid? currentUserId, UserTournamentsFilterDto? filter);
}
