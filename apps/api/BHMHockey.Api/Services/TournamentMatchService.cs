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

    public async Task<TournamentMatchDto> EnterScoreAsync(Guid tournamentId, Guid matchId, EnterScoreRequest request, Guid userId)
    {
        // 1. Authorization Check
        var isAdmin = await _context.TournamentAdmins.AnyAsync(a => a.TournamentId == tournamentId && a.UserId == userId);
        if (!isAdmin)
        {
            throw new UnauthorizedAccessException("User is not a tournament admin");
        }

        // 2. Validation
        var match = await _context.TournamentMatches
            .Include(m => m.Tournament)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .FirstOrDefaultAsync(m => m.Id == matchId && m.TournamentId == tournamentId);

        if (match == null)
        {
            throw new InvalidOperationException("Match not found");
        }

        if (match.Tournament.Status != "InProgress")
        {
            throw new InvalidOperationException($"Cannot enter scores for tournament not in InProgress status");
        }

        if (match.HomeTeamId == null || match.AwayTeamId == null)
        {
            throw new InvalidOperationException("Cannot enter scores for match with TBD teams");
        }

        // 3. Handle Tied Scores in Elimination
        var isElimination = match.Tournament.Format == "SingleElimination" || match.Tournament.Format == "DoubleElimination";

        if (request.HomeScore == request.AwayScore)
        {
            if (isElimination && !request.OvertimeWinnerId.HasValue)
            {
                throw new InvalidOperationException("Scores are tied in elimination format. Please specify overtime winner.");
            }

            if (request.OvertimeWinnerId.HasValue)
            {
                if (request.OvertimeWinnerId != match.HomeTeamId && request.OvertimeWinnerId != match.AwayTeamId)
                {
                    throw new InvalidOperationException("OvertimeWinnerId must be either HomeTeamId or AwayTeamId");
                }
            }
        }

        // 4. Determine Winner
        Guid? winnerId = null;
        Guid? loserId = null;

        if (request.HomeScore > request.AwayScore)
        {
            winnerId = match.HomeTeamId;
            loserId = match.AwayTeamId;
        }
        else if (request.AwayScore > request.HomeScore)
        {
            winnerId = match.AwayTeamId;
            loserId = match.HomeTeamId;
        }
        else if (request.OvertimeWinnerId.HasValue)
        {
            winnerId = request.OvertimeWinnerId;
            loserId = winnerId == match.HomeTeamId ? match.AwayTeamId : match.HomeTeamId;
        }
        // For round robin ties, winnerId stays null

        // 5. Handle Score Edit (if match already has scores)
        if (match.HomeScore.HasValue)
        {
            var homeTeam = await _context.TournamentTeams.FindAsync(match.HomeTeamId);
            var awayTeam = await _context.TournamentTeams.FindAsync(match.AwayTeamId);

            // Reverse old goals
            homeTeam!.GoalsFor -= match.HomeScore.Value;
            homeTeam.GoalsAgainst -= match.AwayScore!.Value;
            awayTeam!.GoalsFor -= match.AwayScore.Value;
            awayTeam.GoalsAgainst -= match.HomeScore.Value;

            // Reverse old wins/losses/ties
            if (match.WinnerTeamId == match.HomeTeamId)
            {
                homeTeam.Wins--;
                awayTeam.Losses--;
            }
            else if (match.WinnerTeamId == match.AwayTeamId)
            {
                awayTeam.Wins--;
                homeTeam.Losses--;
            }
            else // Tie
            {
                homeTeam.Ties--;
                awayTeam.Ties--;
            }
        }

        // 6. Update Match
        match.HomeScore = request.HomeScore;
        match.AwayScore = request.AwayScore;
        match.WinnerTeamId = winnerId;
        match.Status = "Completed";
        match.UpdatedAt = DateTime.UtcNow;

        // 7. Update Team Statistics
        var homeTeamEntity = await _context.TournamentTeams.FindAsync(match.HomeTeamId);
        var awayTeamEntity = await _context.TournamentTeams.FindAsync(match.AwayTeamId);

        // Update goals
        homeTeamEntity!.GoalsFor += request.HomeScore;
        homeTeamEntity.GoalsAgainst += request.AwayScore;
        awayTeamEntity!.GoalsFor += request.AwayScore;
        awayTeamEntity.GoalsAgainst += request.HomeScore;

        // Update wins/losses/ties
        if (winnerId == match.HomeTeamId)
        {
            homeTeamEntity.Wins++;
            awayTeamEntity.Losses++;
        }
        else if (winnerId == match.AwayTeamId)
        {
            awayTeamEntity.Wins++;
            homeTeamEntity.Losses++;
        }
        else // Tie in round robin
        {
            homeTeamEntity.Ties++;
            awayTeamEntity.Ties++;
        }

        // 8. Update Team Status (Elimination formats only)
        if (isElimination && loserId.HasValue)
        {
            var loserTeam = loserId == match.HomeTeamId ? homeTeamEntity : awayTeamEntity;
            loserTeam.Status = "Eliminated";
        }

        // 9. Bracket Advancement
        if (match.NextMatchId.HasValue && winnerId.HasValue)
        {
            var nextMatch = await _context.TournamentMatches.FindAsync(match.NextMatchId);
            if (nextMatch != null)
            {
                // Odd match number → home team, Even match number → away team
                if (match.MatchNumber % 2 == 1) // Odd
                {
                    nextMatch.HomeTeamId = winnerId;
                }
                else // Even
                {
                    nextMatch.AwayTeamId = winnerId;
                }
                nextMatch.UpdatedAt = DateTime.UtcNow;
            }
        }

        // 10. Final Match - Set Winner Status
        if (!match.NextMatchId.HasValue && winnerId.HasValue)
        {
            var winnerTeam = winnerId == match.HomeTeamId ? homeTeamEntity : awayTeamEntity;
            winnerTeam.Status = "Winner";
        }

        // 11. Save and Return
        await _context.SaveChangesAsync();

        // Reload with includes for proper DTO mapping
        var updatedMatch = await _context.TournamentMatches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.WinnerTeam)
            .FirstAsync(m => m.Id == matchId);

        return MapToDto(updatedMatch);
    }

    public async Task<TournamentMatchDto> ForfeitMatchAsync(Guid tournamentId, Guid matchId, ForfeitMatchRequest request, Guid userId)
    {
        // 1. Authorization Check
        var isAdmin = await _context.TournamentAdmins.AnyAsync(a => a.TournamentId == tournamentId && a.UserId == userId);
        if (!isAdmin)
        {
            throw new UnauthorizedAccessException("User is not a tournament admin");
        }

        // 2. Validation
        var match = await _context.TournamentMatches
            .Include(m => m.Tournament)
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .FirstOrDefaultAsync(m => m.Id == matchId && m.TournamentId == tournamentId);

        if (match == null)
        {
            throw new InvalidOperationException("Match not found");
        }

        if (match.Tournament.Status != "InProgress")
        {
            throw new InvalidOperationException($"Cannot forfeit match for tournament not in InProgress status");
        }

        if (match.HomeTeamId == null || match.AwayTeamId == null)
        {
            throw new InvalidOperationException("Cannot forfeit match with TBD teams");
        }

        // 3. Validate forfeiting team is a participant
        if (request.ForfeitingTeamId != match.HomeTeamId && request.ForfeitingTeamId != match.AwayTeamId)
        {
            throw new InvalidOperationException("Forfeiting team is not a participant in this match");
        }

        // 4. Determine winner (non-forfeiting team)
        var winnerId = request.ForfeitingTeamId == match.HomeTeamId ? match.AwayTeamId : match.HomeTeamId;
        var loserId = request.ForfeitingTeamId;

        // 5. Update Match
        match.WinnerTeamId = winnerId;
        match.Status = "Forfeit";
        match.ForfeitReason = request.Reason;
        match.UpdatedAt = DateTime.UtcNow;

        // 6. Update Team Statistics
        var winnerTeam = await _context.TournamentTeams.FindAsync(winnerId);
        var loserTeam = await _context.TournamentTeams.FindAsync(loserId);

        winnerTeam!.Wins++;
        loserTeam!.Losses++;

        // 7. Update Team Status (Elimination formats only)
        var isElimination = match.Tournament.Format == "SingleElimination" || match.Tournament.Format == "DoubleElimination";
        if (isElimination)
        {
            loserTeam.Status = "Eliminated";
        }

        // 8. Bracket Advancement
        if (match.NextMatchId.HasValue)
        {
            var nextMatch = await _context.TournamentMatches.FindAsync(match.NextMatchId);
            if (nextMatch != null)
            {
                // Odd match number → home team, Even match number → away team
                if (match.MatchNumber % 2 == 1) // Odd
                {
                    nextMatch.HomeTeamId = winnerId;
                }
                else // Even
                {
                    nextMatch.AwayTeamId = winnerId;
                }
                nextMatch.UpdatedAt = DateTime.UtcNow;
            }
        }

        // 9. Final Match - Set Winner Status
        if (!match.NextMatchId.HasValue)
        {
            winnerTeam.Status = "Winner";
        }

        // 10. Save and Return
        await _context.SaveChangesAsync();

        // Reload with includes for proper DTO mapping
        var updatedMatch = await _context.TournamentMatches
            .Include(m => m.HomeTeam)
            .Include(m => m.AwayTeam)
            .Include(m => m.WinnerTeam)
            .FirstAsync(m => m.Id == matchId);

        return MapToDto(updatedMatch);
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
