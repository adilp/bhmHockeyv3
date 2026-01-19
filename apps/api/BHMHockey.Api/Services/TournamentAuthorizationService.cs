using BHMHockey.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

/// <summary>
/// Service for checking tournament role-based permissions.
/// Tournament roles: Owner (full control), Admin (manage tournament), Scorekeeper (scores only).
/// Organization admins automatically get Owner-level permissions on their org's tournaments.
/// </summary>
public class TournamentAuthorizationService : ITournamentAuthorizationService
{
    private readonly AppDbContext _context;

    public TournamentAuthorizationService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Helper: Get tournament admin entry (not soft-deleted)
    /// </summary>
    private async Task<string?> GetTournamentRoleAsync(Guid tournamentId, Guid userId)
    {
        var admin = await _context.TournamentAdmins
            .FirstOrDefaultAsync(a => a.TournamentId == tournamentId
                && a.UserId == userId
                && a.RemovedAt == null);
        return admin?.Role;
    }

    /// <summary>
    /// Helper: Check if user is org admin for tournament's org
    /// </summary>
    private async Task<bool> IsOrgAdminForTournamentAsync(Guid tournamentId, Guid userId)
    {
        var tournament = await _context.Tournaments
            .FirstOrDefaultAsync(t => t.Id == tournamentId);

        if (tournament?.OrganizationId == null)
            return false;

        return await _context.OrganizationAdmins
            .AnyAsync(a => a.OrganizationId == tournament.OrganizationId && a.UserId == userId);
    }

    public async Task<string?> GetRoleAsync(Guid tournamentId, Guid userId)
    {
        // First check tournament admin role
        var role = await GetTournamentRoleAsync(tournamentId, userId);
        if (role != null) return role;

        // Check if org admin (gets Owner-level)
        if (await IsOrgAdminForTournamentAsync(tournamentId, userId))
            return "Owner";

        return null;
    }

    public async Task<bool> IsOwnerAsync(Guid tournamentId, Guid userId)
    {
        var role = await GetRoleAsync(tournamentId, userId);
        return role == "Owner";
    }

    public async Task<bool> IsAdminAsync(Guid tournamentId, Guid userId)
    {
        var role = await GetRoleAsync(tournamentId, userId);
        return role == "Owner" || role == "Admin";
    }

    public async Task<bool> IsScorekeeperAsync(Guid tournamentId, Guid userId)
    {
        var role = await GetRoleAsync(tournamentId, userId);
        return role != null; // Any role can enter scores
    }

    // Permission checks
    public Task<bool> CanManageAdminsAsync(Guid tournamentId, Guid userId)
        => IsOwnerAsync(tournamentId, userId);

    public Task<bool> CanManageTeamsAsync(Guid tournamentId, Guid userId)
        => IsAdminAsync(tournamentId, userId);

    public Task<bool> CanManageRegistrationsAsync(Guid tournamentId, Guid userId)
        => IsAdminAsync(tournamentId, userId);

    public Task<bool> CanManageScheduleAsync(Guid tournamentId, Guid userId)
        => IsAdminAsync(tournamentId, userId);

    public Task<bool> CanEnterScoresAsync(Guid tournamentId, Guid userId)
        => IsScorekeeperAsync(tournamentId, userId);

    public Task<bool> CanDeleteTournamentAsync(Guid tournamentId, Guid userId)
        => IsOwnerAsync(tournamentId, userId);

    public Task<bool> CanTransferOwnershipAsync(Guid tournamentId, Guid userId)
        => IsOwnerAsync(tournamentId, userId);
}
