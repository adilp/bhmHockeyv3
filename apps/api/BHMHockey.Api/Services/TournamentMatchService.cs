using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

/// <summary>
/// Service for read-only operations on tournament matches.
/// No authentication required - these are public read operations.
/// </summary>
public class TournamentMatchService : ITournamentMatchService
{
    private readonly AppDbContext _context;

    public TournamentMatchService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<TournamentMatchDto>> GetAllAsync(Guid tournamentId)
    {
        var matches = await _context.TournamentMatches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.WinnerTeam)
            .Where(m => m.TournamentId == tournamentId)
            .OrderBy(m => m.Round)
            .ThenBy(m => m.MatchNumber)
            .ToListAsync();

        return matches.Select(MapToDto).ToList();
    }

    public async Task<TournamentMatchDto?> GetByIdAsync(Guid tournamentId, Guid matchId)
    {
        var match = await _context.TournamentMatches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.WinnerTeam)
            .FirstOrDefaultAsync(m => m.Id == matchId && m.TournamentId == tournamentId);

        if (match == null)
        {
            return null;
        }

        return MapToDto(match);
    }

    private static TournamentMatchDto MapToDto(TournamentMatch match)
    {
        return new TournamentMatchDto
        {
            Id = match.Id,
            TournamentId = match.TournamentId,
            HomeTeamId = match.HomeTeamId,
            HomeTeamName = match.HomeTeam?.Name,
            AwayTeamId = match.AwayTeamId,
            AwayTeamName = match.AwayTeam?.Name,
            Round = match.Round,
            MatchNumber = match.MatchNumber,
            BracketPosition = match.BracketPosition,
            IsBye = match.IsBye,
            ScheduledTime = match.ScheduledTime,
            Venue = match.Venue,
            Status = match.Status,
            HomeScore = match.HomeScore,
            AwayScore = match.AwayScore,
            WinnerTeamId = match.WinnerTeamId,
            WinnerTeamName = match.WinnerTeam?.Name,
            ForfeitReason = match.ForfeitReason,
            NextMatchId = match.NextMatchId,
            LoserNextMatchId = match.LoserNextMatchId,
            CreatedAt = match.CreatedAt,
            UpdatedAt = match.UpdatedAt
        };
    }
}
