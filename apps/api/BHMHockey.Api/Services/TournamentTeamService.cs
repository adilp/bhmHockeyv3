using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

public class TournamentTeamService : ITournamentTeamService
{
    private readonly AppDbContext _context;
    private readonly ITournamentService _tournamentService;

    // Valid statuses for team creation and deletion (before tournament starts)
    private static readonly HashSet<string> TeamManagementStatuses = new()
    {
        "Draft", "Open", "RegistrationClosed"
    };

    public TournamentTeamService(AppDbContext context, ITournamentService tournamentService)
    {
        _context = context;
        _tournamentService = tournamentService;
    }

    public async Task<TournamentTeamDto> CreateAsync(Guid tournamentId, CreateTournamentTeamRequest request, Guid userId)
    {
        // Validate tournament exists
        var tournament = await _context.Tournaments.FindAsync(tournamentId);
        if (tournament == null)
        {
            throw new InvalidOperationException("Tournament not found");
        }

        // Check user can manage tournament
        var canManage = await _tournamentService.CanUserManageTournamentAsync(tournamentId, userId);
        if (!canManage)
        {
            throw new UnauthorizedAccessException("You are not authorized to manage teams for this tournament");
        }

        // Only allow creation when tournament is in Draft, Open, or RegistrationClosed status
        if (!TeamManagementStatuses.Contains(tournament.Status))
        {
            throw new InvalidOperationException(
                $"Cannot create team when tournament is in '{tournament.Status}' status. Team creation is only allowed in: {string.Join(", ", TeamManagementStatuses)}");
        }

        var team = new TournamentTeam
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            Name = request.Name,
            Seed = request.Seed,
            Status = "Registered",
            HasBye = false,
            Wins = 0,
            Losses = 0,
            Ties = 0,
            Points = 0,
            GoalsFor = 0,
            GoalsAgainst = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TournamentTeams.Add(team);
        await _context.SaveChangesAsync();

        return await MapToDto(team);
    }

    public async Task<List<TournamentTeamDto>> GetAllAsync(Guid tournamentId)
    {
        var teams = await _context.TournamentTeams
            .Include(t => t.Captain)
            .Where(t => t.TournamentId == tournamentId)
            .OrderBy(t => t.Seed.HasValue ? 0 : 1) // Nulls last
            .ThenBy(t => t.Seed)
            .ThenBy(t => t.Name)
            .ToListAsync();

        var dtos = new List<TournamentTeamDto>();
        foreach (var team in teams)
        {
            dtos.Add(await MapToDto(team));
        }

        return dtos;
    }

    public async Task<TournamentTeamDto?> GetByIdAsync(Guid tournamentId, Guid teamId)
    {
        var team = await _context.TournamentTeams
            .Include(t => t.Captain)
            .FirstOrDefaultAsync(t => t.TournamentId == tournamentId && t.Id == teamId);

        if (team == null)
        {
            return null;
        }

        return await MapToDto(team);
    }

    public async Task<TournamentTeamDto?> UpdateAsync(Guid tournamentId, Guid teamId, UpdateTournamentTeamRequest request, Guid userId)
    {
        var team = await _context.TournamentTeams
            .Include(t => t.Captain)
            .FirstOrDefaultAsync(t => t.TournamentId == tournamentId && t.Id == teamId);

        if (team == null)
        {
            return null;
        }

        // Check authorization
        var canManage = await _tournamentService.CanUserManageTournamentAsync(tournamentId, userId);
        if (!canManage)
        {
            throw new UnauthorizedAccessException("You are not authorized to manage teams for this tournament");
        }

        // Apply updates (patch semantics - only update provided fields)
        if (request.Name != null) team.Name = request.Name;
        if (request.Seed.HasValue) team.Seed = request.Seed.Value;

        team.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await MapToDto(team);
    }

    public async Task<bool> DeleteAsync(Guid tournamentId, Guid teamId, Guid userId)
    {
        var team = await _context.TournamentTeams.FindAsync(teamId);

        if (team == null || team.TournamentId != tournamentId)
        {
            return false;
        }

        // Check authorization
        var canManage = await _tournamentService.CanUserManageTournamentAsync(tournamentId, userId);
        if (!canManage)
        {
            throw new UnauthorizedAccessException("You are not authorized to manage teams for this tournament");
        }

        // Get tournament to check status
        var tournament = await _context.Tournaments.FindAsync(tournamentId);
        if (tournament == null)
        {
            throw new InvalidOperationException("Tournament not found");
        }

        // Only allow deletion when tournament is in Draft, Open, or RegistrationClosed status
        if (!TeamManagementStatuses.Contains(tournament.Status))
        {
            throw new InvalidOperationException(
                $"Cannot delete team when tournament is in '{tournament.Status}' status. Team deletion is only allowed in: {string.Join(", ", TeamManagementStatuses)}");
        }

        _context.TournamentTeams.Remove(team);
        await _context.SaveChangesAsync();

        return true;
    }

    private Task<TournamentTeamDto> MapToDto(TournamentTeam team)
    {
        return Task.FromResult(new TournamentTeamDto
        {
            Id = team.Id,
            TournamentId = team.TournamentId,
            Name = team.Name,
            CaptainUserId = team.CaptainUserId,
            CaptainName = team.Captain != null ? $"{team.Captain.FirstName} {team.Captain.LastName}" : null,
            Status = team.Status,
            WaitlistPosition = team.WaitlistPosition,
            Seed = team.Seed,
            FinalPlacement = team.FinalPlacement,
            HasBye = team.HasBye,
            Wins = team.Wins,
            Losses = team.Losses,
            Ties = team.Ties,
            Points = team.Points,
            GoalsFor = team.GoalsFor,
            GoalsAgainst = team.GoalsAgainst,
            GoalDifferential = team.GoalsFor - team.GoalsAgainst,
            PaymentStatus = team.PaymentStatus,
            CreatedAt = team.CreatedAt,
            UpdatedAt = team.UpdatedAt
        });
    }
}
