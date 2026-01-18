using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BHMHockey.Api.Services;

/// <summary>
/// Service for calculating and retrieving tournament standings.
/// </summary>
public class StandingsService : IStandingsService
{
    private readonly AppDbContext _context;

    public StandingsService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets standings for a tournament. Works for all tournament formats.
    /// Teams are sorted by points, then tiebreakers (head-to-head, goal differential, goals scored).
    /// For unresolvable ties (3+ teams), returns tiedGroups array for manual resolution.
    /// </summary>
    public async Task<StandingsDto?> GetStandingsAsync(Guid tournamentId)
    {
        // 1. Load tournament
        var tournament = await _context.Tournaments.FindAsync(tournamentId);
        if (tournament == null)
        {
            return null;
        }

        // 2. Load all teams for this tournament
        var teams = await _context.TournamentTeams
            .Where(t => t.TournamentId == tournamentId)
            .ToListAsync();

        if (teams.Count == 0)
        {
            return new StandingsDto
            {
                Standings = new List<TeamStandingDto>(),
                PlayoffCutoff = tournament.PlayoffTeamsCount,
                TiedGroups = new List<TiedGroupDto>()
            };
        }

        // 3. Load all completed matches for head-to-head lookups and games played calculation
        var completedMatches = await _context.TournamentMatches
            .Where(m => m.TournamentId == tournamentId && m.Status == "Completed")
            .ToListAsync();

        // 4. Calculate GamesPlayed for each team from completed matches
        var gamesPlayedDict = teams.ToDictionary(t => t.Id, t => 0);
        foreach (var match in completedMatches)
        {
            if (match.HomeTeamId.HasValue && gamesPlayedDict.ContainsKey(match.HomeTeamId.Value))
            {
                gamesPlayedDict[match.HomeTeamId.Value]++;
            }
            if (match.AwayTeamId.HasValue && gamesPlayedDict.ContainsKey(match.AwayTeamId.Value))
            {
                gamesPlayedDict[match.AwayTeamId.Value]++;
            }
        }

        // 5. Parse tiebreaker order from tournament config
        var tiebreakerOrder = GetTiebreakerOrder(tournament.TiebreakerOrder);

        // 6. Group teams by points
        var teamsByPoints = teams
            .GroupBy(t => t.Points)
            .OrderByDescending(g => g.Key)
            .ToList();

        var standings = new List<TeamStandingDto>();
        var tiedGroups = new List<TiedGroupDto>();
        int currentRank = 1;

        foreach (var pointGroup in teamsByPoints)
        {
            var teamsInGroup = pointGroup.ToList();

            if (teamsInGroup.Count == 1)
            {
                // No tie, just add the team
                var team = teamsInGroup[0];
                standings.Add(CreateTeamStandingDto(team, currentRank, gamesPlayedDict[team.Id], false));
                currentRank++;
            }
            else
            {
                // Multiple teams with same points - apply tiebreakers
                var sortedTeams = ApplyTiebreakers(teamsInGroup, tiebreakerOrder, completedMatches);

                // Check for unresolvable 3+ way ties
                var unresolvableTies = DetectUnresolvableTies(sortedTeams, tiebreakerOrder, completedMatches);

                foreach (var team in sortedTeams)
                {
                    standings.Add(CreateTeamStandingDto(team, currentRank, gamesPlayedDict[team.Id], false));
                    currentRank++;
                }

                // Add to tiedGroups if there are unresolvable ties
                if (unresolvableTies.Count > 0)
                {
                    tiedGroups.AddRange(unresolvableTies);
                }
            }
        }

        // 7. Mark playoff-bound teams
        if (tournament.PlayoffTeamsCount.HasValue && tournament.PlayoffTeamsCount.Value > 0)
        {
            for (int i = 0; i < standings.Count && i < tournament.PlayoffTeamsCount.Value; i++)
            {
                var standing = standings[i];
                standings[i] = standing with { IsPlayoffBound = true };
            }
        }

        return new StandingsDto
        {
            Standings = standings,
            PlayoffCutoff = tournament.PlayoffTeamsCount,
            TiedGroups = tiedGroups.Count > 0 ? tiedGroups : null
        };
    }

    private List<string> GetTiebreakerOrder(string? tiebreakerOrderJson)
    {
        if (string.IsNullOrWhiteSpace(tiebreakerOrderJson))
        {
            // Default order
            return new List<string> { "HeadToHead", "GoalDifferential", "GoalsScored" };
        }

        try
        {
            var order = JsonSerializer.Deserialize<List<string>>(tiebreakerOrderJson);
            return order ?? new List<string> { "HeadToHead", "GoalDifferential", "GoalsScored" };
        }
        catch
        {
            // If parsing fails, return default
            return new List<string> { "HeadToHead", "GoalDifferential", "GoalsScored" };
        }
    }

    private List<TournamentTeam> ApplyTiebreakers(
        List<TournamentTeam> teams,
        List<string> tiebreakerOrder,
        List<TournamentMatch> completedMatches)
    {
        // Create a mutable list for sorting
        var sortedTeams = new List<TournamentTeam>(teams);

        // Apply each tiebreaker in order
        foreach (var tiebreaker in tiebreakerOrder)
        {
            switch (tiebreaker)
            {
                case "HeadToHead":
                    sortedTeams = ApplyHeadToHeadTiebreaker(sortedTeams, completedMatches);
                    break;
                case "GoalDifferential":
                    sortedTeams = sortedTeams.OrderByDescending(t => t.GoalsFor - t.GoalsAgainst).ToList();
                    break;
                case "GoalsScored":
                    sortedTeams = sortedTeams.OrderByDescending(t => t.GoalsFor).ToList();
                    break;
            }
        }

        return sortedTeams;
    }

    private List<TournamentTeam> ApplyHeadToHeadTiebreaker(
        List<TournamentTeam> teams,
        List<TournamentMatch> completedMatches)
    {
        if (teams.Count == 2)
        {
            // Two-way tie: find direct match
            var team1 = teams[0];
            var team2 = teams[1];

            var h2hMatch = completedMatches.FirstOrDefault(m =>
                (m.HomeTeamId == team1.Id && m.AwayTeamId == team2.Id) ||
                (m.HomeTeamId == team2.Id && m.AwayTeamId == team1.Id));

            if (h2hMatch != null && h2hMatch.WinnerTeamId.HasValue)
            {
                // Winner ranks higher
                if (h2hMatch.WinnerTeamId.Value == team1.Id)
                {
                    return new List<TournamentTeam> { team1, team2 };
                }
                else
                {
                    return new List<TournamentTeam> { team2, team1 };
                }
            }
        }
        else if (teams.Count >= 3)
        {
            // 3+ way tie: calculate mini-table points from games among tied teams
            var teamIds = teams.Select(t => t.Id).ToHashSet();
            var h2hMatches = completedMatches.Where(m =>
                m.HomeTeamId.HasValue && m.AwayTeamId.HasValue &&
                teamIds.Contains(m.HomeTeamId.Value) && teamIds.Contains(m.AwayTeamId.Value))
                .ToList();

            var h2hPoints = teams.ToDictionary(t => t.Id, t => 0);
            var h2hWins = teams.ToDictionary(t => t.Id, t => 0);

            foreach (var match in h2hMatches)
            {
                if (match.WinnerTeamId.HasValue)
                {
                    // Win = 3 points (assuming standard scoring)
                    h2hPoints[match.WinnerTeamId.Value] += 3;
                    h2hWins[match.WinnerTeamId.Value]++;

                    // Loss = 0 points (already initialized to 0)
                }
                else if (match.HomeScore.HasValue && match.AwayScore.HasValue &&
                         match.HomeScore.Value == match.AwayScore.Value)
                {
                    // Tie = 1 point each
                    h2hPoints[match.HomeTeamId!.Value] += 1;
                    h2hPoints[match.AwayTeamId!.Value] += 1;
                }
            }

            // Sort by H2H points, then H2H wins
            teams = teams.OrderByDescending(t => h2hPoints[t.Id])
                        .ThenByDescending(t => h2hWins[t.Id])
                        .ToList();
        }

        return teams;
    }

    private List<TiedGroupDto> DetectUnresolvableTies(
        List<TournamentTeam> teams,
        List<string> tiebreakerOrder,
        List<TournamentMatch> completedMatches)
    {
        var tiedGroups = new List<TiedGroupDto>();

        if (teams.Count < 3)
        {
            // Only check for 3+ way ties
            return tiedGroups;
        }

        // Group consecutive teams that are tied on ALL tiebreakers
        var currentGroup = new List<TournamentTeam> { teams[0] };

        for (int i = 1; i < teams.Count; i++)
        {
            if (AreTeamsTiedOnAllCriteria(currentGroup[0], teams[i], tiebreakerOrder, completedMatches))
            {
                currentGroup.Add(teams[i]);
            }
            else
            {
                // Check if current group is a 3+ way tie
                if (currentGroup.Count >= 3)
                {
                    tiedGroups.Add(new TiedGroupDto
                    {
                        TeamIds = currentGroup.Select(t => t.Id).ToList(),
                        Reason = "Teams tied on points and all configured tiebreakers"
                    });
                }

                currentGroup = new List<TournamentTeam> { teams[i] };
            }
        }

        // Check final group
        if (currentGroup.Count >= 3)
        {
            tiedGroups.Add(new TiedGroupDto
            {
                TeamIds = currentGroup.Select(t => t.Id).ToList(),
                Reason = "Teams tied on points and all configured tiebreakers"
            });
        }

        return tiedGroups;
    }

    private bool AreTeamsTiedOnAllCriteria(
        TournamentTeam team1,
        TournamentTeam team2,
        List<string> tiebreakerOrder,
        List<TournamentMatch> completedMatches)
    {
        // Same points (already guaranteed by grouping)
        if (team1.Points != team2.Points)
        {
            return false;
        }

        // Check each tiebreaker
        foreach (var tiebreaker in tiebreakerOrder)
        {
            switch (tiebreaker)
            {
                case "HeadToHead":
                    // For 2-team comparison, check if there's a direct match with a winner
                    var h2hMatch = completedMatches.FirstOrDefault(m =>
                        (m.HomeTeamId == team1.Id && m.AwayTeamId == team2.Id) ||
                        (m.HomeTeamId == team2.Id && m.AwayTeamId == team1.Id));

                    if (h2hMatch != null && h2hMatch.WinnerTeamId.HasValue)
                    {
                        // There's a clear winner in H2H, not tied
                        return false;
                    }
                    break;

                case "GoalDifferential":
                    var gd1 = team1.GoalsFor - team1.GoalsAgainst;
                    var gd2 = team2.GoalsFor - team2.GoalsAgainst;
                    if (gd1 != gd2)
                    {
                        return false;
                    }
                    break;

                case "GoalsScored":
                    if (team1.GoalsFor != team2.GoalsFor)
                    {
                        return false;
                    }
                    break;
            }
        }

        return true;
    }

    private TeamStandingDto CreateTeamStandingDto(
        TournamentTeam team,
        int rank,
        int gamesPlayed,
        bool isPlayoffBound)
    {
        return new TeamStandingDto
        {
            TeamId = team.Id,
            TeamName = team.Name,
            Rank = rank,
            Wins = team.Wins,
            Losses = team.Losses,
            Ties = team.Ties,
            Points = team.Points,
            GoalsFor = team.GoalsFor,
            GoalsAgainst = team.GoalsAgainst,
            GoalDifferential = team.GoalsFor - team.GoalsAgainst,
            GamesPlayed = gamesPlayed,
            IsPlayoffBound = isPlayoffBound
        };
    }
}
