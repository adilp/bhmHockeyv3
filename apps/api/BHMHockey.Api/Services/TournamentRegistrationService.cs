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
    private readonly ITournamentAuthorizationService _authService;

    // Statuses that do not allow registration
    private static readonly HashSet<string> ClosedStatuses = new()
    {
        "Draft", "RegistrationClosed", "InProgress", "Completed", "Postponed", "Cancelled"
    };

    public TournamentRegistrationService(
        AppDbContext context,
        ITournamentService tournamentService,
        INotificationService notificationService,
        ITournamentAuthorizationService authService)
    {
        _context = context;
        _tournamentService = tournamentService;
        _notificationService = notificationService;
        _authService = authService;
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
                // Validate custom questions before reactivating
                ValidateCustomQuestions(tournament.CustomQuestions, request.CustomResponses);

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

        // 6. Validate custom questions
        ValidateCustomQuestions(tournament.CustomQuestions, request.CustomResponses);

        // 7. Create new registration
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
        var isAdmin = await _authService.CanManageRegistrationsAsync(tournamentId, userId);

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
        var isAdmin = isAdminWithdraw && await _authService.CanManageRegistrationsAsync(tournamentId, userId);

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
        var isAdmin = await _authService.CanManageRegistrationsAsync(tournamentId, userId);

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

    public async Task<bool> MarkPaymentAsync(Guid tournamentId, Guid userId)
    {
        var registration = await _context.TournamentRegistrations
            .Include(r => r.Tournament)
                .ThenInclude(t => t.Creator)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r =>
                r.TournamentId == tournamentId &&
                r.UserId == userId &&
                r.Status != "Cancelled");

        if (registration == null) return false;

        // Can't mark payment for free tournaments
        if (registration.Tournament.EntryFee <= 0) return false;

        // Can only mark as paid if currently Pending
        if (registration.PaymentStatus != "Pending") return false;

        registration.PaymentStatus = "MarkedPaid";
        registration.PaymentMarkedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Notify the tournament creator that a user marked their payment
        if (!string.IsNullOrEmpty(registration.Tournament.Creator?.PushToken))
        {
            var userName = $"{registration.User.FirstName} {registration.User.LastName}".Trim();
            if (string.IsNullOrEmpty(userName)) userName = registration.User.Email;
            var tournamentName = registration.Tournament.Name ?? "Tournament";

            await _notificationService.SendPushNotificationAsync(
                registration.Tournament.Creator.PushToken,
                "Payment Pending Verification",
                $"{userName} marked their payment for {tournamentName}. Tap to view.",
                new Dictionary<string, string>
                {
                    { "type", "payment_marked" },
                    { "tournamentId", tournamentId.ToString() }
                }
            );
        }

        return true;
    }

    public async Task<TournamentRegistrationDto?> VerifyPaymentAsync(Guid tournamentId, Guid registrationId, bool verified, Guid adminUserId)
    {
        // Check if user is admin
        var isAdmin = await _authService.CanManageRegistrationsAsync(tournamentId, adminUserId);
        if (!isAdmin)
        {
            throw new UnauthorizedAccessException("You do not have permission to verify payments for this tournament");
        }

        // Find registration by registrationId and tournamentId (include User and Tournament)
        var registration = await _context.TournamentRegistrations
            .Include(r => r.User)
            .Include(r => r.AssignedTeam)
            .Include(r => r.Tournament)
            .FirstOrDefaultAsync(r =>
                r.Id == registrationId &&
                r.TournamentId == tournamentId);

        if (registration == null) return null;

        // Cannot verify payment for free tournaments
        if (registration.Tournament.EntryFee <= 0) return null;

        // Update payment status based on verified flag
        if (verified)
        {
            registration.PaymentStatus = "Verified";
            registration.PaymentVerifiedAt = DateTime.UtcNow;
        }
        else
        {
            registration.PaymentStatus = "Pending";
            registration.PaymentVerifiedAt = null;
        }

        await _context.SaveChangesAsync();

        return MapToDto(registration);
    }

    /// <summary>
    /// Validates that all required custom questions have non-empty answers.
    /// </summary>
    private void ValidateCustomQuestions(string? customQuestionsJson, string? customResponsesJson)
    {
        // If no custom questions defined, nothing to validate
        if (string.IsNullOrEmpty(customQuestionsJson))
        {
            return;
        }

        // Parse questions
        List<CustomQuestion>? questions;
        try
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            questions = System.Text.Json.JsonSerializer.Deserialize<List<CustomQuestion>>(customQuestionsJson, options);
        }
        catch
        {
            // If questions JSON is malformed, skip validation (shouldn't happen)
            return;
        }

        if (questions == null || questions.Count == 0)
        {
            return;
        }

        // Parse responses (empty object if null/empty)
        Dictionary<string, System.Text.Json.JsonElement>? responses = null;
        if (!string.IsNullOrEmpty(customResponsesJson))
        {
            try
            {
                responses = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, System.Text.Json.JsonElement>>(customResponsesJson);
            }
            catch
            {
                responses = null;
            }
        }
        responses ??= new Dictionary<string, System.Text.Json.JsonElement>();

        // Check each required question has a non-empty answer
        foreach (var question in questions.Where(q => q.Required))
        {
            if (!responses.TryGetValue(question.Id, out var answer))
            {
                throw new InvalidOperationException($"Answer required for: {question.Label}");
            }

            // Check for empty string answers
            if (answer.ValueKind == System.Text.Json.JsonValueKind.String && string.IsNullOrWhiteSpace(answer.GetString()))
            {
                throw new InvalidOperationException($"Answer required for: {question.Label}");
            }
        }
    }

    /// <summary>
    /// Represents a custom question definition.
    /// </summary>
    private class CustomQuestion
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public bool Required { get; set; }
        public List<string>? Options { get; set; }
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
            0,     // Total badge count
            user.IsGhostPlayer
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
