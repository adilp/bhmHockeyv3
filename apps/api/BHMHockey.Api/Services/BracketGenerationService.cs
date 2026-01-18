using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BHMHockey.Api.Services;

public class BracketGenerationService : IBracketGenerationService
{
    private readonly AppDbContext _context;
    private readonly ITournamentService _tournamentService;

    // Allowed statuses for bracket generation
    private static readonly HashSet<string> BracketGenerationStatuses = new()
    {
        "Draft", "Open", "RegistrationClosed"
    };

    public BracketGenerationService(AppDbContext context, ITournamentService tournamentService)
    {
        _context = context;
        _tournamentService = tournamentService;
    }

    public async Task<List<TournamentMatchDto>> GenerateSingleEliminationBracketAsync(Guid tournamentId, Guid userId)
    {
        // 1. Validate tournament exists
        var tournament = await _context.Tournaments.FindAsync(tournamentId);
        if (tournament == null)
        {
            throw new InvalidOperationException("Tournament not found");
        }

        // 2. Check authorization
        var canManage = await _tournamentService.CanUserManageTournamentAsync(tournamentId, userId);
        if (!canManage)
        {
            throw new UnauthorizedAccessException("You are not authorized to manage this tournament");
        }

        // 3. Check tournament status
        if (!BracketGenerationStatuses.Contains(tournament.Status))
        {
            throw new InvalidOperationException(
                $"Cannot generate bracket when tournament is in '{tournament.Status}' status. " +
                $"Bracket generation is only allowed in: {string.Join(", ", BracketGenerationStatuses)}");
        }

        // 4. Check no existing matches
        var existingMatchCount = await _context.TournamentMatches
            .CountAsync(m => m.TournamentId == tournamentId);
        if (existingMatchCount > 0)
        {
            throw new InvalidOperationException(
                "Tournament already has matches. Use ClearBracket first to regenerate.");
        }

        // 5. Get teams ordered by seed
        var teams = await _context.TournamentTeams
            .Where(t => t.TournamentId == tournamentId)
            .OrderBy(t => t.Seed)
            .ToListAsync();

        if (teams.Count < 2)
        {
            throw new InvalidOperationException("Tournament must have at least 2 teams to generate bracket");
        }

        // 6. Validate seeding
        ValidateSeeding(teams);

        // 7. Calculate bracket size
        var teamCount = teams.Count;
        var totalRounds = (int)Math.Ceiling(Math.Log2(teamCount));
        var bracketSize = (int)Math.Pow(2, totalRounds);
        var byeCount = bracketSize - teamCount;

        // 8. Generate bracket positions using proper seeding separation
        var bracketPositions = GenerateBracketPositions(bracketSize);

        // 10. Identify teams with byes (top seeds)
        var teamsWithByes = teams.Take(byeCount).ToList();
        var byeTeamIds = teamsWithByes.Select(t => t.Id).ToHashSet();

        // 11. Create all matches (empty structure first)
        var allMatches = new List<TournamentMatch>();
        var totalMatches = bracketSize - 1;
        var matchesPerRound = new Dictionary<int, List<TournamentMatch>>();

        // Create matches round by round
        var matchesInRound = bracketSize / 2;
        var matchCounter = 1;

        for (int round = 1; round <= totalRounds; round++)
        {
            matchesPerRound[round] = new List<TournamentMatch>();

            for (int matchNum = 1; matchNum <= matchesInRound; matchNum++)
            {
                var match = new TournamentMatch
                {
                    Id = Guid.NewGuid(),
                    TournamentId = tournamentId,
                    Round = round,
                    MatchNumber = matchNum,
                    Status = "Scheduled",
                    IsBye = false,
                    HomeTeamId = null,
                    AwayTeamId = null,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                matchesPerRound[round].Add(match);
                allMatches.Add(match);
                matchCounter++;
            }

            matchesInRound /= 2;
        }

        // 12. Assign teams to Round 1 matches based on bracket positions
        var round1Matches = matchesPerRound[1];
        for (int i = 0; i < round1Matches.Count; i++)
        {
            var match = round1Matches[i];

            // bracketPositions tells us which seed appears at each bracket slot
            // Position i*2 and i*2+1 are the two slots for this match
            var homeSeed = bracketPositions[i * 2];
            var awaySeed = bracketPositions[i * 2 + 1];

            // Find teams by seed (may be null if seed > teamCount due to byes)
            var homeTeam = teams.FirstOrDefault(t => t.Seed == homeSeed);
            var awayTeam = teams.FirstOrDefault(t => t.Seed == awaySeed);

            // Determine if this is a bye match (one team doesn't exist due to bye)
            var homeIsBye = homeTeam != null && byeTeamIds.Contains(homeTeam.Id);
            var awayIsBye = awayTeam != null && byeTeamIds.Contains(awayTeam.Id);
            var isActualBye = homeTeam == null || awayTeam == null;

            if (homeIsBye || awayIsBye || isActualBye)
            {
                // Bye match - only one team advances
                var byeTeam = homeTeam ?? awayTeam;
                if (byeTeam != null)
                {
                    match.HomeTeamId = byeTeam.Id;
                    match.AwayTeamId = null;
                    match.IsBye = true;
                    match.Status = "Completed";
                    match.WinnerTeamId = byeTeam.Id;

                    // Mark team as having a bye
                    byeTeam.HasBye = true;
                }
            }
            else
            {
                // Regular match - both teams play
                match.HomeTeamId = homeTeam!.Id;
                match.AwayTeamId = awayTeam!.Id;
                match.IsBye = false;
                match.Status = "Scheduled";
            }
        }

        // 13. Link matches with NextMatchId (bottom-up)
        for (int round = 1; round < totalRounds; round++)
        {
            var currentRoundMatches = matchesPerRound[round];
            var nextRoundMatches = matchesPerRound[round + 1];

            for (int i = 0; i < currentRoundMatches.Count; i++)
            {
                var nextMatchIndex = i / 2;
                currentRoundMatches[i].NextMatchId = nextRoundMatches[nextMatchIndex].Id;
            }
        }

        // 14. Propagate bye winners to next round
        for (int round = 1; round < totalRounds; round++)
        {
            var currentRoundMatches = matchesPerRound[round];
            var nextRoundMatches = matchesPerRound[round + 1];

            for (int i = 0; i < currentRoundMatches.Count; i++)
            {
                var match = currentRoundMatches[i];
                if (match.IsBye && match.WinnerTeamId.HasValue)
                {
                    var nextMatchIndex = i / 2;
                    var nextMatch = nextRoundMatches[nextMatchIndex];

                    // Determine if this bye winner goes to home or away
                    if (i % 2 == 0)
                    {
                        // Even index -> home team of next match
                        nextMatch.HomeTeamId = match.WinnerTeamId;
                    }
                    else
                    {
                        // Odd index -> away team of next match
                        nextMatch.AwayTeamId = match.WinnerTeamId;
                    }
                }
            }
        }

        // 15. Set BracketPosition labels
        SetBracketPositionLabels(matchesPerRound, totalRounds, teamCount);

        // 16. Save all matches
        await _context.TournamentMatches.AddRangeAsync(allMatches);
        await _context.SaveChangesAsync();

        // 17. Return as DTOs
        var matchDtos = allMatches
            .OrderBy(m => m.Round)
            .ThenBy(m => m.MatchNumber)
            .Select(m => MapToDto(m, teams))
            .ToList();

        return matchDtos;
    }

    public async Task ClearBracketAsync(Guid tournamentId, Guid userId)
    {
        // 1. Check authorization
        var canManage = await _tournamentService.CanUserManageTournamentAsync(tournamentId, userId);
        if (!canManage)
        {
            throw new UnauthorizedAccessException("You are not authorized to manage this tournament");
        }

        // 2. Delete all matches
        var matches = await _context.TournamentMatches
            .Where(m => m.TournamentId == tournamentId)
            .ToListAsync();

        _context.TournamentMatches.RemoveRange(matches);

        // 3. Reset team HasBye flags
        var teams = await _context.TournamentTeams
            .Where(t => t.TournamentId == tournamentId && t.HasBye)
            .ToListAsync();

        foreach (var team in teams)
        {
            team.HasBye = false;
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Generates a round robin schedule for the tournament using the Circle Method algorithm.
    /// Each team plays every other team exactly once.
    /// </summary>
    public async Task<List<TournamentMatchDto>> GenerateRoundRobinScheduleAsync(Guid tournamentId, Guid userId)
    {
        // 1. Validate tournament exists
        var tournament = await _context.Tournaments.FindAsync(tournamentId);
        if (tournament == null)
        {
            throw new InvalidOperationException("Tournament not found");
        }

        // 2. Check authorization
        var canManage = await _tournamentService.CanUserManageTournamentAsync(tournamentId, userId);
        if (!canManage)
        {
            throw new UnauthorizedAccessException("You are not authorized to manage this tournament");
        }

        // 3. Check tournament status
        if (!BracketGenerationStatuses.Contains(tournament.Status))
        {
            throw new InvalidOperationException(
                $"Cannot generate bracket when tournament is in '{tournament.Status}' status. " +
                $"Bracket generation is only allowed in: {string.Join(", ", BracketGenerationStatuses)}");
        }

        // 4. Check no existing matches
        var existingMatchCount = await _context.TournamentMatches
            .CountAsync(m => m.TournamentId == tournamentId);
        if (existingMatchCount > 0)
        {
            throw new InvalidOperationException(
                "Tournament already has matches. Use ClearBracket first to regenerate.");
        }

        // 5. Get teams ordered by seed
        var teams = await _context.TournamentTeams
            .Where(t => t.TournamentId == tournamentId)
            .OrderBy(t => t.Seed)
            .ToListAsync();

        // 6. Validate minimum 2 teams
        if (teams.Count < 2)
        {
            throw new InvalidOperationException("Tournament must have at least 2 teams to generate bracket");
        }

        // 7. Validate seeding
        ValidateSeeding(teams);

        // 8. Circle method algorithm
        var teamList = teams.ToList();
        bool isOdd = teamList.Count % 2 == 1;
        if (isOdd)
        {
            teamList.Add(null!); // Phantom bye team
        }

        int n = teamList.Count;
        int rounds = n - 1;
        var matches = new List<TournamentMatch>();
        int matchCounter = 0;

        for (int round = 1; round <= rounds; round++)
        {
            int matchInRound = 0;
            for (int i = 0; i < n / 2; i++)
            {
                var team1 = teamList[i];
                var team2 = teamList[n - 1 - i];

                // Skip if either is phantom (null)
                if (team1 == null || team2 == null) continue;

                matchInRound++;
                matchCounter++;

                // Alternate home/away based on seed and round for balanced distribution
                // Lower seed is home on even rounds, higher seed on odd rounds
                var lowerSeedTeam = team1.Seed < team2.Seed ? team1 : team2;
                var higherSeedTeam = team1.Seed < team2.Seed ? team2 : team1;
                var homeTeam = (round % 2 == 0) ? lowerSeedTeam : higherSeedTeam;
                var awayTeam = (round % 2 == 0) ? higherSeedTeam : lowerSeedTeam;

                matches.Add(new TournamentMatch
                {
                    Id = Guid.NewGuid(),
                    TournamentId = tournamentId,
                    Round = round,
                    MatchNumber = matchInRound,
                    BracketPosition = $"RR-R{round}-M{matchInRound}",
                    HomeTeamId = homeTeam.Id,
                    AwayTeamId = awayTeam.Id,
                    Status = "Scheduled",
                    ScheduledTime = null,
                    IsBye = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            // Rotate: keep position 0 fixed, rotate others
            // Last element moves to position 1, all others shift right
            var last = teamList[n - 1];
            teamList.RemoveAt(n - 1);
            teamList.Insert(1, last);
        }

        // 9. Save all matches
        await _context.TournamentMatches.AddRangeAsync(matches);
        await _context.SaveChangesAsync();

        // 10. Return as DTOs
        var matchDtos = matches
            .OrderBy(m => m.Round)
            .ThenBy(m => m.MatchNumber)
            .Select(m => MapToDto(m, teams))
            .ToList();

        return matchDtos;
    }

    /// <summary>
    /// Generates a bracket/schedule based on tournament format.
    /// Dispatches to the appropriate generation method based on format.
    /// </summary>
    public async Task<List<TournamentMatchDto>> GenerateBracketAsync(Guid tournamentId, Guid userId)
    {
        var tournament = await _context.Tournaments.FindAsync(tournamentId);
        if (tournament == null)
        {
            throw new InvalidOperationException("Tournament not found");
        }

        return tournament.Format switch
        {
            "SingleElimination" => await GenerateSingleEliminationBracketAsync(tournamentId, userId),
            "RoundRobin" => await GenerateRoundRobinScheduleAsync(tournamentId, userId),
            "DoubleElimination" => throw new NotImplementedException("Double elimination not yet implemented"),
            _ => throw new InvalidOperationException($"Unknown tournament format: {tournament.Format}")
        };
    }

    #region Private Helper Methods

    /// <summary>
    /// Validates that all teams have seeds and they are contiguous without duplicates.
    /// </summary>
    private void ValidateSeeding(List<TournamentTeam> teams)
    {
        // Check all teams have seeds
        var missingSeeds = teams.Where(t => !t.Seed.HasValue).ToList();
        if (missingSeeds.Any())
        {
            throw new InvalidOperationException(
                $"All teams must have a seed assigned. {missingSeeds.Count} team(s) are missing seeds.");
        }

        // Check for duplicate seeds
        var seedGroups = teams.GroupBy(t => t.Seed!.Value).Where(g => g.Count() > 1).ToList();
        if (seedGroups.Any())
        {
            var duplicates = string.Join(", ", seedGroups.Select(g => g.Key));
            throw new InvalidOperationException($"Duplicate seeds found: {duplicates}");
        }

        // Check seeds are contiguous (1, 2, 3, ..., N)
        var seeds = teams.Select(t => t.Seed!.Value).OrderBy(s => s).ToList();
        for (int i = 0; i < seeds.Count; i++)
        {
            if (seeds[i] != i + 1)
            {
                throw new InvalidOperationException(
                    $"Seeds must be contiguous starting from 1. Missing seed: {i + 1}");
            }
        }
    }

    /// <summary>
    /// Generates standard tournament bracket seeding positions.
    /// For 8 teams: [1, 8, 4, 5, 3, 6, 2, 7]
    /// This ensures top seeds are maximally separated (1 and 2 only meet in final).
    /// </summary>
    private List<int> GenerateBracketPositions(int bracketSize)
    {
        // Standard single-elimination bracket seeding
        // Position i plays against position (bracketSize + 1 - i)
        // Arranged so top seeds are separated

        if (bracketSize == 2) return new List<int> { 1, 2 };
        if (bracketSize == 4) return new List<int> { 1, 4, 3, 2 };
        if (bracketSize == 8) return new List<int> { 1, 8, 4, 5, 3, 6, 2, 7 };
        if (bracketSize == 16) return new List<int> { 1, 16, 8, 9, 4, 13, 5, 12, 3, 14, 6, 11, 2, 15, 7, 10 };

        // For larger brackets, build algorithmically
        // (This covers 99% of real tournament use cases with the hardcoded values above)
        var positions = new List<int> { 1, 2 };
        while (positions.Count < bracketSize)
        {
            var newPositions = new List<int>();
            var sum = positions.Count * 2 + 1;
            var half = positions.Count;

            // Interleave: take from start and end alternately
            for (int i = 0; i < half; i++)
            {
                newPositions.Add(positions[i]);
                newPositions.Add(sum - positions[half - 1 - i]);
            }
            positions = newPositions;
        }
        return positions;
    }

    /// <summary>
    /// Sets bracket position labels based on tournament size and round.
    /// Examples: QF1-QF4, SF1-SF2, Final, or R1-M1, R1-M2 for small brackets
    /// </summary>
    private void SetBracketPositionLabels(
        Dictionary<int, List<TournamentMatch>> matchesPerRound,
        int totalRounds,
        int teamCount)
    {
        foreach (var round in matchesPerRound.Keys)
        {
            var matches = matchesPerRound[round];
            var roundsFromEnd = totalRounds - round;

            if (roundsFromEnd == 0)
            {
                // Final
                matches[0].BracketPosition = "Final";
            }
            else if (roundsFromEnd == 1 && teamCount >= 4)
            {
                // Semi-finals (only if 4+ teams)
                for (int i = 0; i < matches.Count; i++)
                {
                    matches[i].BracketPosition = $"SF{i + 1}";
                }
            }
            else if (roundsFromEnd == 2 && teamCount >= 8)
            {
                // Quarter-finals (only if 8+ teams)
                for (int i = 0; i < matches.Count; i++)
                {
                    matches[i].BracketPosition = $"QF{i + 1}";
                }
            }
            else
            {
                // Earlier rounds or small brackets - use R{round}-M{matchNumber}
                for (int i = 0; i < matches.Count; i++)
                {
                    matches[i].BracketPosition = $"R{round}-M{i + 1}";
                }
            }
        }
    }

    /// <summary>
    /// Maps a TournamentMatch entity to a DTO, including team names.
    /// </summary>
    private TournamentMatchDto MapToDto(TournamentMatch match, List<TournamentTeam> teams)
    {
        var homeTeam = match.HomeTeamId.HasValue
            ? teams.FirstOrDefault(t => t.Id == match.HomeTeamId.Value)
            : null;
        var awayTeam = match.AwayTeamId.HasValue
            ? teams.FirstOrDefault(t => t.Id == match.AwayTeamId.Value)
            : null;
        var winnerTeam = match.WinnerTeamId.HasValue
            ? teams.FirstOrDefault(t => t.Id == match.WinnerTeamId.Value)
            : null;

        return new TournamentMatchDto
        {
            Id = match.Id,
            TournamentId = match.TournamentId,
            HomeTeamId = match.HomeTeamId,
            HomeTeamName = homeTeam?.Name,
            AwayTeamId = match.AwayTeamId,
            AwayTeamName = awayTeam?.Name,
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
            WinnerTeamName = winnerTeam?.Name,
            ForfeitReason = match.ForfeitReason,
            NextMatchId = match.NextMatchId,
            LoserNextMatchId = match.LoserNextMatchId,
            CreatedAt = match.CreatedAt,
            UpdatedAt = match.UpdatedAt
        };
    }

    #endregion
}
