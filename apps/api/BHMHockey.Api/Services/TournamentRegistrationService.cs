using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

/// <summary>
/// Service for managing tournament registrations (individual player sign-ups).
/// </summary>
public class TournamentRegistrationService : ITournamentRegistrationService
{
    private readonly AppDbContext _context;
    private readonly ITournamentService _tournamentService;
    private readonly INotificationService _notificationService;

    // Statuses that do not allow registration
    private static readonly HashSet<string> ClosedStatuses = new()
    {
        "Draft", "RegistrationClosed", "InProgress", "Completed", "Postponed", "Cancelled"
    };

    public TournamentRegistrationService(
        AppDbContext context,
        ITournamentService tournamentService,
        INotificationService notificationService)
    {
        _context = context;
        _tournamentService = tournamentService;
        _notificationService = notificationService;
    }

    public async Task<TournamentRegistrationResultDto> RegisterAsync(
        Guid tournamentId,
        CreateTournamentRegistrationRequest request,
        Guid userId)
    {
        // 1. Validate tournament exists
        var tournament = await _context.Tournaments.FindAsync(tournamentId);
        if (tournament == null)
        {
            throw new InvalidOperationException("Tournament not found");
        }

        // 2. Check tournament status is "Open"
        if (ClosedStatuses.Contains(tournament.Status))
        {
            throw new InvalidOperationException("Tournament is not open for registration");
        }

        // 3. Check registration deadline hasn't passed
        if (tournament.RegistrationDeadline < DateTime.UtcNow)
        {
            throw new InvalidOperationException("Registration deadline has passed");
        }

        // 4. Check for duplicate registration (same user, same tournament, status not "Cancelled")
        var existingRegistration = await _context.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.TournamentId == tournamentId && r.UserId == userId);

        if (existingRegistration != null)
        {
            // 5. Allow re-registration if previous was cancelled
            if (existingRegistration.Status == "Cancelled")
            {
                // Reactivate the cancelled registration
                existingRegistration.Status = "Registered";
                existingRegistration.Position = request.Position;
                existingRegistration.CustomResponses = request.CustomResponses;
                existingRegistration.WaiverStatus = request.WaiverAccepted ? "Signed" : "Pending";
                existingRegistration.PaymentStatus = tournament.EntryFee > 0 ? "Pending" : null;
                existingRegistration.RegisteredAt = DateTime.UtcNow;
                existingRegistration.UpdatedAt = DateTime.UtcNow;
                existingRegistration.CancelledAt = null;
                existingRegistration.WaitlistPosition = null;
                existingRegistration.PromotedAt = null;
                existingRegistration.PaymentMarkedAt = null;
                existingRegistration.PaymentVerifiedAt = null;
                existingRegistration.PaymentDeadlineAt = null;
                existingRegistration.AssignedTeamId = null;

                await _context.SaveChangesAsync();

                return new TournamentRegistrationResultDto(
                    "Registered",
                    null,
                    "Successfully registered for the tournament"
                );
            }

            // Duplicate: status is not Cancelled
            throw new InvalidOperationException("Already registered for this tournament");
        }

        // 6 & 7. Create new registration
        var registration = new TournamentRegistration
        {
            TournamentId = tournamentId,
            UserId = userId,
            Status = "Registered",
            Position = request.Position,
            CustomResponses = request.CustomResponses,
            WaiverStatus = request.WaiverAccepted ? "Signed" : "Pending",
            PaymentStatus = tournament.EntryFee > 0 ? "Pending" : null,
            RegisteredAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TournamentRegistrations.Add(registration);
        await _context.SaveChangesAsync();

        return new TournamentRegistrationResultDto(
            "Registered",
            null,
            "Successfully registered for the tournament"
        );
    }

    public async Task<TournamentRegistrationDto?> GetMyRegistrationAsync(Guid tournamentId, Guid userId)
    {
        // Find registration for user (where status != "Cancelled")
        var registration = await _context.TournamentRegistrations
            .Include(r => r.User)
            .Include(r => r.AssignedTeam)
            .FirstOrDefaultAsync(r =>
                r.TournamentId == tournamentId &&
                r.UserId == userId &&
                r.Status != "Cancelled");

        if (registration == null)
        {
            return null;
        }

        return MapToDto(registration);
    }

    public async Task<TournamentRegistrationDto?> UpdateAsync(
        Guid tournamentId,
        UpdateTournamentRegistrationRequest request,
        Guid userId,
        Guid? targetUserId = null)
    {
        // Determine whose registration to update
        var registrationUserId = targetUserId ?? userId;
        var isAdminUpdate = targetUserId.HasValue && targetUserId != userId;

        // 1. Find registration (status != "Cancelled")
        var registration = await _context.TournamentRegistrations
            .Include(r => r.User)
            .Include(r => r.AssignedTeam)
            .Include(r => r.Tournament)
            .FirstOrDefaultAsync(r =>
                r.TournamentId == tournamentId &&
                r.UserId == registrationUserId &&
                r.Status != "Cancelled");

        if (registration == null)
        {
            if (isAdminUpdate)
            {
                // Admin targeting a non-existent registration
                return null;
            }

            // User trying to update their own registration but has none
            // Check if any registration exists - if so, they might be trying to access others' data
            var anyRegistrationExists = await _context.TournamentRegistrations
                .AnyAsync(r => r.TournamentId == tournamentId && r.Status != "Cancelled");

            if (anyRegistrationExists)
            {
                // There are registrations, but not for this user - unauthorized
                throw new UnauthorizedAccessException("You do not have permission to update this registration");
            }

            // No registrations at all - just return null
            return null;
        }

        // 2. Check ownership OR admin access
        var isOwner = registration.UserId == userId;
        var isAdmin = await _tournamentService.CanUserManageTournamentAsync(tournamentId, userId);

        // 3. If not owner and not admin, throw UnauthorizedAccessException
        if (!isOwner && !isAdmin)
        {
            throw new UnauthorizedAccessException("You do not have permission to update this registration");
        }

        // 4. Check registration deadline hasn't passed (unless admin)
        if (!isAdmin && registration.Tournament.RegistrationDeadline < DateTime.UtcNow)
        {
            throw new InvalidOperationException("Registration deadline has passed");
        }

        // 5. Update fields and timestamp
        if (request.Position != null)
        {
            registration.Position = request.Position;
        }
        if (request.CustomResponses != null)
        {
            registration.CustomResponses = request.CustomResponses;
        }
        registration.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return MapToDto(registration);
    }

    public async Task<bool> WithdrawAsync(Guid tournamentId, Guid userId, Guid? targetUserId = null)
    {
        // Determine whose registration to withdraw
        var registrationUserId = targetUserId ?? userId;
        var isAdminWithdraw = targetUserId.HasValue && targetUserId != userId;

        // 1. Find registration (status != "Cancelled")
        var registration = await _context.TournamentRegistrations
            .FirstOrDefaultAsync(r =>
                r.TournamentId == tournamentId &&
                r.UserId == registrationUserId &&
                r.Status != "Cancelled");

        if (registration == null)
        {
            if (isAdminWithdraw)
            {
                // Admin targeting a non-existent registration
                return false;
            }

            // User trying to withdraw their own registration but has none
            // Check if any registration exists - if so, they might be trying to access others' data
            var anyRegistrationExists = await _context.TournamentRegistrations
                .AnyAsync(r => r.TournamentId == tournamentId && r.Status != "Cancelled");

            if (anyRegistrationExists)
            {
                // There are registrations, but not for this user - unauthorized
                throw new UnauthorizedAccessException("You do not have permission to withdraw this registration");
            }

            // No registrations at all - just return false
            return false;
        }

        // 2. Check ownership OR admin access
        var isOwner = registration.UserId == userId;
        var isAdmin = isAdminWithdraw && await _tournamentService.CanUserManageTournamentAsync(tournamentId, userId);

        if (!isOwner && !isAdmin)
        {
            throw new UnauthorizedAccessException("You do not have permission to withdraw this registration");
        }

        // 3. Set status to "Cancelled", set CancelledAt
        registration.Status = "Cancelled";
        registration.CancelledAt = DateTime.UtcNow;
        registration.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return true;
    }

    public async Task<List<TournamentRegistrationDto>> GetAllAsync(Guid tournamentId, Guid userId)
    {
        // 1. Check user is tournament admin
        var isAdmin = await _tournamentService.CanUserManageTournamentAsync(tournamentId, userId);

        // 2. If not admin, throw UnauthorizedAccessException
        if (!isAdmin)
        {
            throw new UnauthorizedAccessException("You do not have permission to view all registrations");
        }

        // 3. Return all registrations (including cancelled) with user details
        var registrations = await _context.TournamentRegistrations
            .Include(r => r.User)
            .Include(r => r.AssignedTeam)
            .Where(r => r.TournamentId == tournamentId)
            .OrderBy(r => r.RegisteredAt)
            .ToListAsync();

        return registrations.Select(MapToDto).ToList();
    }

    /// <summary>
    /// Maps a TournamentRegistration entity to TournamentRegistrationDto.
    /// Includes UserDto for the user and AssignedTeamName if AssignedTeamId is set.
    /// </summary>
    private TournamentRegistrationDto MapToDto(TournamentRegistration registration)
    {
        var user = registration.User;
        var userDto = new UserDto(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            user.PhoneNumber,
            user.Positions,
            user.VenmoHandle,
            user.Role,
            user.CreatedAt,
            null,  // Badges not included in tournament registration responses
            0      // Total badge count
        );

        return new TournamentRegistrationDto(
            registration.Id,
            registration.TournamentId,
            userDto,
            registration.Status,
            registration.Position,
            registration.WaitlistPosition,
            registration.PromotedAt,
            registration.IsWaitlisted,
            registration.AssignedTeamId,
            registration.AssignedTeam?.Name,
            registration.CustomResponses,
            registration.WaiverStatus,
            registration.PaymentStatus,
            registration.PaymentMarkedAt,
            registration.PaymentVerifiedAt,
            registration.PaymentDeadlineAt,
            registration.RegisteredAt,
            registration.UpdatedAt,
            registration.CancelledAt
        );
    }
}
