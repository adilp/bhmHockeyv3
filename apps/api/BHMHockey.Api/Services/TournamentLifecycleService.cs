using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BHMHockey.Api.Services;

/// <summary>
/// Service for managing tournament lifecycle state transitions.
/// </summary>
public class TournamentLifecycleService : ITournamentLifecycleService
{
    private readonly AppDbContext _context;
    private readonly ITournamentService _tournamentService;

    public TournamentLifecycleService(AppDbContext context, ITournamentService tournamentService)
    {
        _context = context;
        _tournamentService = tournamentService;
    }

    public async Task<TournamentDto> PublishAsync(Guid tournamentId, Guid userId)
    {
        return await TransitionAsync(tournamentId, userId, "Open", tournament =>
        {
            tournament.PublishedAt = DateTime.UtcNow;
        });
    }

    public async Task<TournamentDto> CloseRegistrationAsync(Guid tournamentId, Guid userId)
    {
        return await TransitionAsync(tournamentId, userId, "RegistrationClosed");
    }

    public async Task<TournamentDto> StartAsync(Guid tournamentId, Guid userId)
    {
        return await TransitionAsync(tournamentId, userId, "InProgress", tournament =>
        {
            tournament.StartedAt = DateTime.UtcNow;
        });
    }

    public async Task<TournamentDto> CompleteAsync(Guid tournamentId, Guid userId)
    {
        return await TransitionAsync(tournamentId, userId, "Completed", tournament =>
        {
            tournament.CompletedAt = DateTime.UtcNow;
        });
    }

    public async Task<TournamentDto> PostponeAsync(Guid tournamentId, Guid userId, DateTime? newStartDate = null, DateTime? newEndDate = null)
    {
        string? details = null;
        if (newStartDate.HasValue || newEndDate.HasValue)
        {
            details = JsonSerializer.Serialize(new
            {
                newStartDate,
                newEndDate
            });
        }

        return await TransitionAsync(tournamentId, userId, "Postponed", tournament =>
        {
            if (newStartDate.HasValue)
            {
                tournament.PostponedToDate = newStartDate.Value;
                tournament.StartDate = newStartDate.Value;
            }
            if (newEndDate.HasValue)
            {
                tournament.EndDate = newEndDate.Value;
            }
        }, details);
    }

    public async Task<TournamentDto> ResumeAsync(Guid tournamentId, Guid userId)
    {
        return await TransitionAsync(tournamentId, userId, "InProgress");
    }

    public async Task<TournamentDto> CancelAsync(Guid tournamentId, Guid userId)
    {
        return await TransitionAsync(tournamentId, userId, "Cancelled", tournament =>
        {
            tournament.CancelledAt = DateTime.UtcNow;
        });
    }

    /// <summary>
    /// Core transition method that handles validation, state change, audit logging, and saving.
    /// </summary>
    private async Task<TournamentDto> TransitionAsync(
        Guid tournamentId,
        Guid userId,
        string toStatus,
        Action<Tournament>? additionalUpdates = null,
        string? auditDetails = null)
    {
        // Get tournament
        var tournament = await _context.Tournaments
            .Include(t => t.Organization)
            .FirstOrDefaultAsync(t => t.Id == tournamentId);

        if (tournament == null)
        {
            throw new InvalidOperationException("Tournament not found");
        }

        // Check authorization
        var canManage = await _tournamentService.CanUserManageTournamentAsync(tournamentId, userId);
        if (!canManage)
        {
            throw new UnauthorizedAccessException("You are not authorized to manage this tournament");
        }

        var fromStatus = tournament.Status;

        // Validate transition using state machine
        TournamentStateMachine.ValidateTransition(fromStatus, toStatus);

        // Get action name for audit log
        var action = TournamentStateMachine.GetActionForTransition(fromStatus, toStatus);

        // Update tournament status
        tournament.Status = toStatus;
        tournament.UpdatedAt = DateTime.UtcNow;

        // Apply any additional updates (timestamps, etc.)
        additionalUpdates?.Invoke(tournament);

        // Create audit log entry
        var auditLog = new TournamentAuditLog
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            UserId = userId,
            Action = action,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Details = auditDetails,
            Timestamp = DateTime.UtcNow
        };
        _context.TournamentAuditLogs.Add(auditLog);

        // Save changes
        await _context.SaveChangesAsync();

        // Return updated DTO
        return await MapToDto(tournament, userId);
    }

    /// <summary>
    /// Maps a Tournament entity to TournamentDto.
    /// </summary>
    private async Task<TournamentDto> MapToDto(Tournament tournament, Guid userId)
    {
        var canManage = await _tournamentService.CanUserManageTournamentAsync(tournament.Id, userId);

        return new TournamentDto
        {
            Id = tournament.Id,
            OrganizationId = tournament.OrganizationId,
            OrganizationName = tournament.Organization?.Name,
            CreatorId = tournament.CreatorId,
            Name = tournament.Name,
            Description = tournament.Description,
            Format = tournament.Format,
            TeamFormation = tournament.TeamFormation,
            Status = tournament.Status,
            StartDate = tournament.StartDate,
            EndDate = tournament.EndDate,
            RegistrationDeadline = tournament.RegistrationDeadline,
            PostponedToDate = tournament.PostponedToDate,
            MaxTeams = tournament.MaxTeams,
            MinPlayersPerTeam = tournament.MinPlayersPerTeam,
            MaxPlayersPerTeam = tournament.MaxPlayersPerTeam,
            AllowMultiTeam = tournament.AllowMultiTeam,
            AllowSubstitutions = tournament.AllowSubstitutions,
            EntryFee = tournament.EntryFee,
            FeeType = tournament.FeeType,
            PointsWin = tournament.PointsWin,
            PointsTie = tournament.PointsTie,
            PointsLoss = tournament.PointsLoss,
            PlayoffFormat = tournament.PlayoffFormat,
            PlayoffTeamsCount = tournament.PlayoffTeamsCount,
            RulesContent = tournament.RulesContent,
            WaiverUrl = tournament.WaiverUrl,
            Venue = tournament.Venue,
            NotificationSettings = tournament.NotificationSettings,
            CustomQuestions = tournament.CustomQuestions,
            EligibilityRequirements = tournament.EligibilityRequirements,
            TiebreakerOrder = tournament.TiebreakerOrder,
            CreatedAt = tournament.CreatedAt,
            UpdatedAt = tournament.UpdatedAt,
            PublishedAt = tournament.PublishedAt,
            StartedAt = tournament.StartedAt,
            CompletedAt = tournament.CompletedAt,
            CancelledAt = tournament.CancelledAt,
            CanManage = canManage
        };
    }
}
