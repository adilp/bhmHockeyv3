using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

/// <summary>
/// Service for managing tournament administrators.
/// Handles adding, removing, and updating roles for tournament admins.
/// Only Owners can manage admins.
/// </summary>
public class TournamentAdminService : ITournamentAdminService
{
    private readonly AppDbContext _context;
    private readonly ITournamentAuthorizationService _authService;

    public TournamentAdminService(AppDbContext context, ITournamentAuthorizationService authService)
    {
        _context = context;
        _authService = authService;
    }

    public async Task<List<TournamentAdminDto>> GetAdminsAsync(Guid tournamentId)
    {
        var admins = await _context.TournamentAdmins
            .Include(a => a.User)
            .Include(a => a.AddedByUser)
            .Where(a => a.TournamentId == tournamentId && a.RemovedAt == null)
            .OrderBy(a => a.AddedAt)
            .ToListAsync();

        return admins.Select(a => new TournamentAdminDto(
            a.Id,
            a.TournamentId,
            a.UserId,
            a.User.FirstName,
            a.User.LastName,
            a.User.Email,
            a.Role,
            a.AddedAt,
            a.AddedByUserId,
            a.AddedByUser != null ? $"{a.AddedByUser.FirstName} {a.AddedByUser.LastName}" : null
        )).ToList();
    }

    public async Task<TournamentAdminDto> AddAdminAsync(Guid tournamentId, Guid userIdToAdd, string role, Guid requesterId)
    {
        // Check requester is Owner
        var isOwner = await _authService.IsOwnerAsync(tournamentId, requesterId);
        if (!isOwner)
        {
            throw new UnauthorizedAccessException("Only the tournament owner can add admins.");
        }

        // Validate role is not Owner
        if (role.Equals("Owner", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot add a user with Owner role. Use TransferOwnership instead.");
        }

        // Validate role is Admin or Scorekeeper
        if (!role.Equals("Admin", StringComparison.OrdinalIgnoreCase) &&
            !role.Equals("Scorekeeper", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Role must be 'Admin' or 'Scorekeeper'.");
        }

        // Check user exists
        var userExists = await _context.Users.AnyAsync(u => u.Id == userIdToAdd);
        if (!userExists)
        {
            throw new InvalidOperationException("User does not exist.");
        }

        // Check user is not already admin
        var alreadyAdmin = await _context.TournamentAdmins
            .AnyAsync(a => a.TournamentId == tournamentId && a.UserId == userIdToAdd && a.RemovedAt == null);
        if (alreadyAdmin)
        {
            throw new InvalidOperationException("User is already an admin of this tournament.");
        }

        // Create new admin entry
        var admin = new TournamentAdmin
        {
            TournamentId = tournamentId,
            UserId = userIdToAdd,
            Role = role,
            AddedByUserId = requesterId,
            AddedAt = DateTime.UtcNow
        };

        _context.TournamentAdmins.Add(admin);
        await _context.SaveChangesAsync();

        // Load the user and addedBy user for the DTO
        await _context.Entry(admin).Reference(a => a.User).LoadAsync();
        await _context.Entry(admin).Reference(a => a.AddedByUser).LoadAsync();

        return new TournamentAdminDto(
            admin.Id,
            admin.TournamentId,
            admin.UserId,
            admin.User.FirstName,
            admin.User.LastName,
            admin.User.Email,
            admin.Role,
            admin.AddedAt,
            admin.AddedByUserId,
            admin.AddedByUser != null ? $"{admin.AddedByUser.FirstName} {admin.AddedByUser.LastName}" : null
        );
    }

    public async Task<TournamentAdminDto?> UpdateRoleAsync(Guid tournamentId, Guid userId, string newRole, Guid requesterId)
    {
        // Check requester is Owner
        var isOwner = await _authService.IsOwnerAsync(tournamentId, requesterId);
        if (!isOwner)
        {
            throw new UnauthorizedAccessException("Only the tournament owner can update admin roles.");
        }

        // Find admin entry
        var admin = await _context.TournamentAdmins
            .Include(a => a.User)
            .Include(a => a.AddedByUser)
            .FirstOrDefaultAsync(a => a.TournamentId == tournamentId && a.UserId == userId && a.RemovedAt == null);

        if (admin == null)
        {
            return null;
        }

        // Cannot update Owner's role
        if (admin.Role.Equals("Owner", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot update the owner's role. Transfer ownership first.");
        }

        // Cannot update TO Owner role
        if (newRole.Equals("Owner", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot update to Owner role. Use TransferOwnership instead.");
        }

        // Validate new role is Admin or Scorekeeper
        if (!newRole.Equals("Admin", StringComparison.OrdinalIgnoreCase) &&
            !newRole.Equals("Scorekeeper", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Role must be 'Admin' or 'Scorekeeper'.");
        }

        // Update and save
        admin.Role = newRole;
        await _context.SaveChangesAsync();

        return new TournamentAdminDto(
            admin.Id,
            admin.TournamentId,
            admin.UserId,
            admin.User.FirstName,
            admin.User.LastName,
            admin.User.Email,
            admin.Role,
            admin.AddedAt,
            admin.AddedByUserId,
            admin.AddedByUser != null ? $"{admin.AddedByUser.FirstName} {admin.AddedByUser.LastName}" : null
        );
    }

    public async Task<bool> RemoveAdminAsync(Guid tournamentId, Guid userIdToRemove, Guid requesterId)
    {
        // Check requester is Owner
        var isOwner = await _authService.IsOwnerAsync(tournamentId, requesterId);
        if (!isOwner)
        {
            throw new UnauthorizedAccessException("Only the tournament owner can remove admins.");
        }

        // Find admin entry
        var admin = await _context.TournamentAdmins
            .FirstOrDefaultAsync(a => a.TournamentId == tournamentId && a.UserId == userIdToRemove && a.RemovedAt == null);

        if (admin == null)
        {
            return false;
        }

        // Cannot remove Owner
        if (admin.Role.Equals("Owner", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Cannot remove owner. Transfer ownership first.");
        }

        // Count remaining admins
        var adminCount = await _context.TournamentAdmins
            .CountAsync(a => a.TournamentId == tournamentId && a.RemovedAt == null);

        if (adminCount <= 1)
        {
            throw new InvalidOperationException("Cannot remove the last admin");
        }

        // Remove (hard delete) the admin entry
        _context.TournamentAdmins.Remove(admin);
        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<TournamentAdminDto> TransferOwnershipAsync(Guid tournamentId, Guid newOwnerUserId, Guid requesterId)
    {
        // Check requester is Owner
        var isOwner = await _authService.IsOwnerAsync(tournamentId, requesterId);
        if (!isOwner)
        {
            throw new UnauthorizedAccessException("Only the tournament owner can transfer ownership.");
        }

        // Cannot transfer to self
        if (newOwnerUserId == requesterId)
        {
            throw new InvalidOperationException("Cannot transfer ownership to yourself.");
        }

        // New owner must already be admin
        var newOwnerAdmin = await _context.TournamentAdmins
            .Include(a => a.User)
            .Include(a => a.AddedByUser)
            .FirstOrDefaultAsync(a => a.TournamentId == tournamentId && a.UserId == newOwnerUserId && a.RemovedAt == null);

        if (newOwnerAdmin == null)
        {
            throw new InvalidOperationException("New owner must already be an admin of the tournament.");
        }

        // Find current owner
        var currentOwner = await _context.TournamentAdmins
            .FirstOrDefaultAsync(a => a.TournamentId == tournamentId && a.UserId == requesterId && a.RemovedAt == null);

        if (currentOwner == null)
        {
            throw new InvalidOperationException("Current owner admin record not found.");
        }

        // Atomically: old owner becomes "Admin", new owner becomes "Owner"
        currentOwner.Role = "Admin";
        newOwnerAdmin.Role = "Owner";

        await _context.SaveChangesAsync();

        return new TournamentAdminDto(
            newOwnerAdmin.Id,
            newOwnerAdmin.TournamentId,
            newOwnerAdmin.UserId,
            newOwnerAdmin.User.FirstName,
            newOwnerAdmin.User.LastName,
            newOwnerAdmin.User.Email,
            newOwnerAdmin.Role,
            newOwnerAdmin.AddedAt,
            newOwnerAdmin.AddedByUserId,
            newOwnerAdmin.AddedByUser != null ? $"{newOwnerAdmin.AddedByUser.FirstName} {newOwnerAdmin.AddedByUser.LastName}" : null
        );
    }
}
