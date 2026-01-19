namespace BHMHockey.Api.Services;

/// <summary>
/// Service for checking tournament role-based permissions.
/// Tournament roles: Owner (full control), Admin (manage tournament), Scorekeeper (scores only).
/// Organization admins automatically get Owner-level permissions on their org's tournaments.
/// </summary>
public interface ITournamentAuthorizationService
{
    /// <summary>
    /// Checks if a user has Owner role for the tournament.
    /// Organization admins on org-owned tournaments also count as owners.
    /// </summary>
    Task<bool> IsOwnerAsync(Guid tournamentId, Guid userId);

    /// <summary>
    /// Checks if a user has Admin role or higher (Owner or Admin).
    /// Organization admins on org-owned tournaments also count as admins.
    /// </summary>
    Task<bool> IsAdminAsync(Guid tournamentId, Guid userId);

    /// <summary>
    /// Checks if a user has any tournament admin role (Owner, Admin, or Scorekeeper).
    /// Organization admins on org-owned tournaments are also included.
    /// </summary>
    Task<bool> IsScorekeeperAsync(Guid tournamentId, Guid userId);

    /// <summary>
    /// Gets the user's actual role for the tournament.
    /// Returns "Owner", "Admin", or "Scorekeeper" if user is a tournament admin.
    /// Returns null if user has no tournament admin role.
    /// Organization admins on org-owned tournaments return "Owner".
    /// </summary>
    Task<string?> GetRoleAsync(Guid tournamentId, Guid userId);

    /// <summary>
    /// Checks if a user can manage tournament admins (add/remove admins, transfer ownership).
    /// Only Owners can manage admins.
    /// </summary>
    Task<bool> CanManageAdminsAsync(Guid tournamentId, Guid userId);

    /// <summary>
    /// Checks if a user can manage tournament teams (create, edit, delete teams).
    /// Owners and Admins can manage teams.
    /// </summary>
    Task<bool> CanManageTeamsAsync(Guid tournamentId, Guid userId);

    /// <summary>
    /// Checks if a user can manage tournament registrations (approve, reject, manage assignments).
    /// Owners and Admins can manage registrations.
    /// </summary>
    Task<bool> CanManageRegistrationsAsync(Guid tournamentId, Guid userId);

    /// <summary>
    /// Checks if a user can manage the tournament schedule (create, edit, delete matches).
    /// Owners and Admins can manage the schedule.
    /// </summary>
    Task<bool> CanManageScheduleAsync(Guid tournamentId, Guid userId);

    /// <summary>
    /// Checks if a user can enter scores for tournament matches.
    /// All roles (Owner, Admin, Scorekeeper) can enter scores.
    /// </summary>
    Task<bool> CanEnterScoresAsync(Guid tournamentId, Guid userId);

    /// <summary>
    /// Checks if a user can delete the tournament.
    /// Only Owners can delete tournaments.
    /// </summary>
    Task<bool> CanDeleteTournamentAsync(Guid tournamentId, Guid userId);

    /// <summary>
    /// Checks if a user can transfer tournament ownership to another user.
    /// Only Owners can transfer ownership.
    /// </summary>
    Task<bool> CanTransferOwnershipAsync(Guid tournamentId, Guid userId);
}
