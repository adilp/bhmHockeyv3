using BHMHockey.Api.Data;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// Tests for StandingsService - TDD approach for TRN-021.
/// Tests standings calculation, sorting, and tiebreaker logic.
/// </summary>
public class StandingsServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly StandingsService _sut;
    private readonly ITournamentAuthorizationService _authService;
    private readonly ITournamentAuditService _auditService;

    public StandingsServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _authService = new TournamentAuthorizationService(_context);
        var auditLogger = new Mock<ILogger<TournamentAuditService>>();
        _auditService = new TournamentAuditService(_context, _authService, auditLogger.Object);
        _sut = new StandingsService(_context, _authService, _auditService);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Helper Methods

    private async Task<User> CreateTestUser(string email = "test@example.com")
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = "hashed_password",
            FirstName = "John",
            LastName = "Doe",
            Role = "Player",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<Tournament> CreateTestTournament(
        Guid creatorId,
        string format = "RoundRobin",
        int? playoffTeamsCount = null,
        string? tiebreakerOrder = null)
    {
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            CreatorId = creatorId,
            Name = "Test Tournament",
            Format = format,
            TeamFormation = "OrganizerAssigned",
            Status = "InProgress",
            StartDate = DateTime.UtcNow.AddDays(30),
            EndDate = DateTime.UtcNow.AddDays(32),
            RegistrationDeadline = DateTime.UtcNow.AddDays(25),
            MaxTeams = 8,
            PlayoffTeamsCount = playoffTeamsCount,
            TiebreakerOrder = tiebreakerOrder,
            PointsWin = 3,
            PointsTie = 1,
            PointsLoss = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Tournaments.Add(tournament);
        await _context.SaveChangesAsync();
        return tournament;
    }

    private async Task<TournamentTeam> CreateTestTeam(
        Guid tournamentId,
        string name,
        int wins = 0,
        int losses = 0,
        int ties = 0,
        int points = 0,
        int goalsFor = 0,
        int goalsAgainst = 0)
    {
        var team = new TournamentTeam
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            Name = name,
            Status = "Active",
            Wins = wins,
            Losses = losses,
            Ties = ties,
            Points = points,
            GoalsFor = goalsFor,
            GoalsAgainst = goalsAgainst,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TournamentTeams.Add(team);
        await _context.SaveChangesAsync();
        return team;
    }

    private async Task<TournamentMatch> CreateCompletedMatch(
        Guid tournamentId,
        Guid homeTeamId,
        Guid awayTeamId,
        int homeScore,
        int awayScore,
        Guid? winnerId = null)
    {
        var match = new TournamentMatch
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            HomeScore = homeScore,
            AwayScore = awayScore,
            WinnerTeamId = winnerId,
            Round = 1,
            MatchNumber = 1,
            Status = "Completed",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TournamentMatches.Add(match);
        await _context.SaveChangesAsync();
        return match;
    }

    #endregion

    #region Basic Sorting Tests

    [Fact]
    public async Task GetStandingsAsync_SortsByPointsDescending()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A", points: 9);
        var teamB = await CreateTestTeam(tournament.Id, "Team B", points: 6);
        var teamC = await CreateTestTeam(tournament.Id, "Team C", points: 3);

        // Act
        var result = await _sut.GetStandingsAsync(tournament.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Standings.Should().HaveCount(3);
        result.Standings[0].TeamName.Should().Be("Team A");
        result.Standings[0].Points.Should().Be(9);
        result.Standings[1].TeamName.Should().Be("Team B");
        result.Standings[1].Points.Should().Be(6);
        result.Standings[2].TeamName.Should().Be("Team C");
        result.Standings[2].Points.Should().Be(3);
    }

    [Fact]
    public async Task GetStandingsAsync_ReturnsAllTeamsWithStats()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);

        var team = await CreateTestTeam(
            tournament.Id,
            "Team A",
            wins: 3,
            losses: 1,
            ties: 1,
            points: 10,
            goalsFor: 15,
            goalsAgainst: 8);

        // Create a completed match so games played is calculated
        var team2 = await CreateTestTeam(tournament.Id, "Team B");
        await CreateCompletedMatch(tournament.Id, team.Id, team2.Id, 3, 1, team.Id);

        // Act
        var result = await _sut.GetStandingsAsync(tournament.Id);

        // Assert
        result.Should().NotBeNull();
        var standing = result!.Standings.First(s => s.TeamId == team.Id);
        standing.Wins.Should().Be(3);
        standing.Losses.Should().Be(1);
        standing.Ties.Should().Be(1);
        standing.Points.Should().Be(10);
        standing.GoalsFor.Should().Be(15);
        standing.GoalsAgainst.Should().Be(8);
        standing.GoalDifferential.Should().Be(7);
        standing.GamesPlayed.Should().Be(1); // From the match we created
    }

    [Fact]
    public async Task GetStandingsAsync_AssignsCorrectRanks()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);

        await CreateTestTeam(tournament.Id, "Team A", points: 9);
        await CreateTestTeam(tournament.Id, "Team B", points: 6);
        await CreateTestTeam(tournament.Id, "Team C", points: 3);
        await CreateTestTeam(tournament.Id, "Team D", points: 0);

        // Act
        var result = await _sut.GetStandingsAsync(tournament.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Standings.Should().HaveCount(4);
        result.Standings[0].Rank.Should().Be(1);
        result.Standings[1].Rank.Should().Be(2);
        result.Standings[2].Rank.Should().Be(3);
        result.Standings[3].Rank.Should().Be(4);
    }

    #endregion

    #region Tiebreaker Tests

    [Fact]
    public async Task GetStandingsAsync_TiedPoints_UsesHeadToHeadFirst()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A", points: 6, goalsFor: 5, goalsAgainst: 3);
        var teamB = await CreateTestTeam(tournament.Id, "Team B", points: 6, goalsFor: 5, goalsAgainst: 3);

        // Team B beat Team A directly
        await CreateCompletedMatch(tournament.Id, teamA.Id, teamB.Id, 1, 2, teamB.Id);

        // Act
        var result = await _sut.GetStandingsAsync(tournament.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Standings.Should().HaveCount(2);
        result.Standings[0].TeamName.Should().Be("Team B"); // B won head-to-head
        result.Standings[1].TeamName.Should().Be("Team A");
    }

    [Fact]
    public async Task GetStandingsAsync_TiedPoints_TiedHeadToHead_UsesGoalDifferential()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);

        // Same points, no H2H match, different GD
        var teamA = await CreateTestTeam(tournament.Id, "Team A", points: 6, goalsFor: 10, goalsAgainst: 5); // GD = +5
        var teamB = await CreateTestTeam(tournament.Id, "Team B", points: 6, goalsFor: 8, goalsAgainst: 6);  // GD = +2

        // Act
        var result = await _sut.GetStandingsAsync(tournament.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Standings.Should().HaveCount(2);
        result.Standings[0].TeamName.Should().Be("Team A"); // Better GD
        result.Standings[0].GoalDifferential.Should().Be(5);
        result.Standings[1].TeamName.Should().Be("Team B");
        result.Standings[1].GoalDifferential.Should().Be(2);
    }

    [Fact]
    public async Task GetStandingsAsync_TiedPoints_TiedGD_UsesGoalsScored()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);

        // Same points, same GD, different GF
        var teamA = await CreateTestTeam(tournament.Id, "Team A", points: 6, goalsFor: 12, goalsAgainst: 6); // GD = +6, GF = 12
        var teamB = await CreateTestTeam(tournament.Id, "Team B", points: 6, goalsFor: 10, goalsAgainst: 4); // GD = +6, GF = 10

        // Act
        var result = await _sut.GetStandingsAsync(tournament.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Standings.Should().HaveCount(2);
        result.Standings[0].TeamName.Should().Be("Team A"); // More goals scored
        result.Standings[0].GoalsFor.Should().Be(12);
        result.Standings[1].TeamName.Should().Be("Team B");
        result.Standings[1].GoalsFor.Should().Be(10);
    }

    [Fact]
    public async Task GetStandingsAsync_RespectsCustomTiebreakerOrder()
    {
        // Arrange
        var user = await CreateTestUser();
        // Custom order: GoalDifferential first, then HeadToHead
        var tiebreakerOrder = JsonSerializer.Serialize(new[] { "GoalDifferential", "HeadToHead", "GoalsScored" });
        var tournament = await CreateTestTournament(user.Id, tiebreakerOrder: tiebreakerOrder);

        var teamA = await CreateTestTeam(tournament.Id, "Team A", points: 6, goalsFor: 10, goalsAgainst: 2); // GD = +8
        var teamB = await CreateTestTeam(tournament.Id, "Team B", points: 6, goalsFor: 5, goalsAgainst: 3);  // GD = +2

        // Team B beat Team A head-to-head, but GD should take precedence with custom order
        await CreateCompletedMatch(tournament.Id, teamA.Id, teamB.Id, 1, 2, teamB.Id);

        // Act
        var result = await _sut.GetStandingsAsync(tournament.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Standings.Should().HaveCount(2);
        result.Standings[0].TeamName.Should().Be("Team A"); // Better GD wins despite H2H loss
    }

    #endregion

    #region 3+ Way Tie Tests

    [Fact]
    public async Task GetStandingsAsync_ThreeWayTie_CannotResolve_ReturnsTiedGroups()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);

        // Three teams with identical stats - circular H2H would be unresolvable
        var teamA = await CreateTestTeam(tournament.Id, "Team A", points: 6, goalsFor: 6, goalsAgainst: 6);
        var teamB = await CreateTestTeam(tournament.Id, "Team B", points: 6, goalsFor: 6, goalsAgainst: 6);
        var teamC = await CreateTestTeam(tournament.Id, "Team C", points: 6, goalsFor: 6, goalsAgainst: 6);

        // Circular results: A beats B, B beats C, C beats A (would need matches for full test)
        // For simplicity, no H2H matches - they're fully tied

        // Act
        var result = await _sut.GetStandingsAsync(tournament.Id);

        // Assert
        result.Should().NotBeNull();
        result!.TiedGroups.Should().NotBeNull();
        result.TiedGroups.Should().HaveCountGreaterThanOrEqualTo(1);

        var tiedGroup = result.TiedGroups!.First();
        tiedGroup.TeamIds.Should().HaveCount(3);
        tiedGroup.TeamIds.Should().Contain(teamA.Id);
        tiedGroup.TeamIds.Should().Contain(teamB.Id);
        tiedGroup.TeamIds.Should().Contain(teamC.Id);
    }

    [Fact]
    public async Task GetStandingsAsync_TiedGroups_IncludesReason()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);

        // Three teams with identical stats
        await CreateTestTeam(tournament.Id, "Team A", points: 6, goalsFor: 6, goalsAgainst: 6);
        await CreateTestTeam(tournament.Id, "Team B", points: 6, goalsFor: 6, goalsAgainst: 6);
        await CreateTestTeam(tournament.Id, "Team C", points: 6, goalsFor: 6, goalsAgainst: 6);

        // Act
        var result = await _sut.GetStandingsAsync(tournament.Id);

        // Assert
        result.Should().NotBeNull();
        result!.TiedGroups.Should().NotBeNull();
        result.TiedGroups!.First().Reason.Should().NotBeNullOrEmpty();
        result.TiedGroups!.First().Reason.Should().Contain("tied");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task GetStandingsAsync_NoTeams_ReturnsEmptyStandings()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        // No teams added

        // Act
        var result = await _sut.GetStandingsAsync(tournament.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Standings.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStandingsAsync_NoMatches_ReturnsAllTeamsWithZeroGamesPlayed()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);

        await CreateTestTeam(tournament.Id, "Team A", points: 0);
        await CreateTestTeam(tournament.Id, "Team B", points: 0);

        // Act
        var result = await _sut.GetStandingsAsync(tournament.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Standings.Should().HaveCount(2);
        result.Standings.Should().AllSatisfy(s => s.GamesPlayed.Should().Be(0));
    }

    [Fact]
    public async Task GetStandingsAsync_ReturnsPlayoffCutoff()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, playoffTeamsCount: 4);

        await CreateTestTeam(tournament.Id, "Team A", points: 12);
        await CreateTestTeam(tournament.Id, "Team B", points: 9);
        await CreateTestTeam(tournament.Id, "Team C", points: 6);
        await CreateTestTeam(tournament.Id, "Team D", points: 3);
        await CreateTestTeam(tournament.Id, "Team E", points: 0);

        // Act
        var result = await _sut.GetStandingsAsync(tournament.Id);

        // Assert
        result.Should().NotBeNull();
        result!.PlayoffCutoff.Should().Be(4);

        // Top 4 should be playoff bound
        result.Standings[0].IsPlayoffBound.Should().BeTrue();
        result.Standings[1].IsPlayoffBound.Should().BeTrue();
        result.Standings[2].IsPlayoffBound.Should().BeTrue();
        result.Standings[3].IsPlayoffBound.Should().BeTrue();
        result.Standings[4].IsPlayoffBound.Should().BeFalse();
    }

    [Fact]
    public async Task GetStandingsAsync_TournamentNotFound_ReturnsNull()
    {
        // Arrange - no tournament created

        // Act
        var result = await _sut.GetStandingsAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetStandingsAsync_SingleElimination_StillReturnsStandings()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, format: "SingleElimination");

        await CreateTestTeam(tournament.Id, "Team A", wins: 2, points: 6);
        await CreateTestTeam(tournament.Id, "Team B", wins: 1, points: 3);

        // Act
        var result = await _sut.GetStandingsAsync(tournament.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Standings.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetStandingsAsync_CalculatesGamesPlayedFromMatches()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A", wins: 2, points: 6);
        var teamB = await CreateTestTeam(tournament.Id, "Team B", wins: 1, points: 3);
        var teamC = await CreateTestTeam(tournament.Id, "Team C", wins: 0, points: 0);

        // Team A played 2 games, Team B played 2 games, Team C played 1 game
        await CreateCompletedMatch(tournament.Id, teamA.Id, teamB.Id, 3, 1, teamA.Id);
        await CreateCompletedMatch(tournament.Id, teamA.Id, teamC.Id, 2, 0, teamA.Id);
        await CreateCompletedMatch(tournament.Id, teamB.Id, teamC.Id, 2, 1, teamB.Id);

        // Act
        var result = await _sut.GetStandingsAsync(tournament.Id);

        // Assert
        result.Should().NotBeNull();
        var standingA = result!.Standings.First(s => s.TeamId == teamA.Id);
        var standingB = result.Standings.First(s => s.TeamId == teamB.Id);
        var standingC = result.Standings.First(s => s.TeamId == teamC.Id);

        standingA.GamesPlayed.Should().Be(2);
        standingB.GamesPlayed.Should().Be(2);
        standingC.GamesPlayed.Should().Be(2);
    }

    #endregion
}
