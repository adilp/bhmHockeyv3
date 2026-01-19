using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// Tests for TournamentMatchService.EnterScoreAsync - TDD approach for TRN-005.
/// Tests written FIRST before implementation.
/// Tests score entry, bracket advancement, team statistics, and team status updates.
/// </summary>
public class ScoreEntryServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly TournamentMatchService _sut;
    private readonly TournamentAuthorizationService _authService;

    public ScoreEntryServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _authService = new TournamentAuthorizationService(_context);
        var auditLogger = new Mock<ILogger<TournamentAuditService>>();
        var auditService = new TournamentAuditService(_context, _authService, auditLogger.Object);
        _sut = new TournamentMatchService(_context, _authService, auditService);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Helper Methods

    private async Task<User> CreateTestUser(string email = "test@example.com", string role = "Player")
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = "hashed_password",
            FirstName = "John",
            LastName = "Doe",
            Role = role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<Tournament> CreateTestTournament(Guid creatorId, string status = "InProgress", string format = "SingleElimination", int pointsWin = 3, int pointsTie = 1, int pointsLoss = 0)
    {
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            CreatorId = creatorId,
            Name = "Test Tournament",
            Format = format,
            TeamFormation = "OrganizerAssigned",
            Status = status,
            StartDate = DateTime.UtcNow.AddDays(30),
            EndDate = DateTime.UtcNow.AddDays(32),
            RegistrationDeadline = DateTime.UtcNow.AddDays(25),
            MaxTeams = 8,
            PointsWin = pointsWin,
            PointsTie = pointsTie,
            PointsLoss = pointsLoss,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Tournaments.Add(tournament);
        await _context.SaveChangesAsync();
        return tournament;
    }

    private async Task<TournamentTeam> CreateTestTeam(Guid tournamentId, string name, string status = "Active")
    {
        var team = new TournamentTeam
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            Name = name,
            Status = status,
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
        return team;
    }

    private async Task<TournamentMatch> CreateTestMatch(
        Guid tournamentId,
        Guid? homeTeamId = null,
        Guid? awayTeamId = null,
        int round = 1,
        int matchNumber = 1,
        string status = "Scheduled",
        string? bracketPosition = null,
        Guid? nextMatchId = null)
    {
        var match = new TournamentMatch
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            HomeTeamId = homeTeamId,
            AwayTeamId = awayTeamId,
            Round = round,
            MatchNumber = matchNumber,
            BracketPosition = bracketPosition,
            Status = status,
            NextMatchId = nextMatchId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TournamentMatches.Add(match);
        await _context.SaveChangesAsync();
        return match;
    }

    private async Task AddTournamentAdmin(Guid tournamentId, Guid userId)
    {
        var admin = new TournamentAdmin
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            UserId = userId,
            Role = "Admin",
            AddedAt = DateTime.UtcNow
        };

        _context.TournamentAdmins.Add(admin);
        await _context.SaveChangesAsync();
    }

    #endregion

    #region EnterScoreAsync Tests

    [Fact]
    public async Task EnterScoreAsync_WithValidScores_SetsScoresAndWinner()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id);

        var request = new EnterScoreRequest
        {
            HomeScore = 5,
            AwayScore = 3
        };

        // Act
        var result = await _sut.EnterScoreAsync(tournament.Id, match.Id, request, user.Id);

        // Assert
        result.Should().NotBeNull();
        result.HomeScore.Should().Be(5);
        result.AwayScore.Should().Be(3);
        result.WinnerTeamId.Should().Be(teamA.Id);
    }

    [Fact]
    public async Task EnterScoreAsync_WithValidScores_SetsMatchStatusToCompleted()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id);

        var request = new EnterScoreRequest
        {
            HomeScore = 4,
            AwayScore = 2
        };

        // Act
        var result = await _sut.EnterScoreAsync(tournament.Id, match.Id, request, user.Id);

        // Assert
        result.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task EnterScoreAsync_HomeTeamWins_SetsHomeTeamAsWinner()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id);

        var request = new EnterScoreRequest
        {
            HomeScore = 6,
            AwayScore = 2
        };

        // Act
        var result = await _sut.EnterScoreAsync(tournament.Id, match.Id, request, user.Id);

        // Assert
        result.WinnerTeamId.Should().Be(teamA.Id);
        result.WinnerTeamName.Should().Be("Team A");
    }

    [Fact]
    public async Task EnterScoreAsync_AwayTeamWins_SetsAwayTeamAsWinner()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id);

        var request = new EnterScoreRequest
        {
            HomeScore = 2,
            AwayScore = 7
        };

        // Act
        var result = await _sut.EnterScoreAsync(tournament.Id, match.Id, request, user.Id);

        // Assert
        result.WinnerTeamId.Should().Be(teamB.Id);
        result.WinnerTeamName.Should().Be("Team B");
    }

    [Fact]
    public async Task EnterScoreAsync_WithTiedScores_InElimination_RequiresOvertimeWinner()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, format: "SingleElimination");
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id);

        var request = new EnterScoreRequest
        {
            HomeScore = 3,
            AwayScore = 3,
            OvertimeWinnerId = null
        };

        // Act & Assert
        var act = async () => await _sut.EnterScoreAsync(tournament.Id, match.Id, request, user.Id);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*tied*elimination*overtime winner*");
    }

    [Fact]
    public async Task EnterScoreAsync_WithTiedScores_InElimination_WithOvertimeWinner_Succeeds()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, format: "SingleElimination");
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id);

        var request = new EnterScoreRequest
        {
            HomeScore = 3,
            AwayScore = 3,
            OvertimeWinnerId = teamA.Id
        };

        // Act
        var result = await _sut.EnterScoreAsync(tournament.Id, match.Id, request, user.Id);

        // Assert
        result.Should().NotBeNull();
        result.HomeScore.Should().Be(3);
        result.AwayScore.Should().Be(3);
        result.WinnerTeamId.Should().Be(teamA.Id);
        result.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task EnterScoreAsync_WithTiedScores_InRoundRobin_AllowsTie()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, format: "RoundRobin");
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id);

        var request = new EnterScoreRequest
        {
            HomeScore = 4,
            AwayScore = 4
        };

        // Act
        var result = await _sut.EnterScoreAsync(tournament.Id, match.Id, request, user.Id);

        // Assert
        result.Should().NotBeNull();
        result.HomeScore.Should().Be(4);
        result.AwayScore.Should().Be(4);
        result.WinnerTeamId.Should().BeNull();
        result.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task EnterScoreAsync_ForMatchWithTBDTeams_ThrowsInvalidOperationException()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id);

        var match = await CreateTestMatch(tournament.Id, null, null);

        var request = new EnterScoreRequest
        {
            HomeScore = 3,
            AwayScore = 2
        };

        // Act & Assert
        var act = async () => await _sut.EnterScoreAsync(tournament.Id, match.Id, request, user.Id);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*TBD*");
    }

    [Fact]
    public async Task EnterScoreAsync_ForNonInProgressTournament_ThrowsInvalidOperationException()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, status: "Draft");
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id);

        var request = new EnterScoreRequest
        {
            HomeScore = 3,
            AwayScore = 2
        };

        // Act & Assert
        var act = async () => await _sut.EnterScoreAsync(tournament.Id, match.Id, request, user.Id);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*tournament*InProgress*");
    }

    [Fact]
    public async Task EnterScoreAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var unauthorizedUser = await CreateTestUser("unauthorized@example.com");
        var tournament = await CreateTestTournament(creator.Id);
        await AddTournamentAdmin(tournament.Id, creator.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id);

        var request = new EnterScoreRequest
        {
            HomeScore = 3,
            AwayScore = 2
        };

        // Act & Assert
        var act = async () => await _sut.EnterScoreAsync(tournament.Id, match.Id, request, unauthorizedUser.Id);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    #endregion

    #region Bracket Advancement Tests

    [Fact]
    public async Task EnterScoreAsync_AdvancesWinnerToNextMatch_AsHomeTeam()
    {
        // Arrange - Match with even index (0) should advance winner to next match's home team
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");

        // Create semifinal match (next match for this quarterfinal)
        var semifinal = await CreateTestMatch(tournament.Id, null, null, round: 2, matchNumber: 1, bracketPosition: "SF1");

        // Create quarterfinal match with nextMatch reference
        var quarterfinal = await CreateTestMatch(
            tournament.Id,
            teamA.Id,
            teamB.Id,
            round: 1,
            matchNumber: 1,
            bracketPosition: "QF1",
            nextMatchId: semifinal.Id);

        var request = new EnterScoreRequest
        {
            HomeScore = 5,
            AwayScore = 2
        };

        // Act
        var result = await _sut.EnterScoreAsync(tournament.Id, quarterfinal.Id, request, user.Id);

        // Assert
        result.Should().NotBeNull();

        // Verify the semifinal match has been updated with the winner
        var updatedSemifinal = await _context.TournamentMatches.FindAsync(semifinal.Id);
        updatedSemifinal.Should().NotBeNull();
        updatedSemifinal!.HomeTeamId.Should().Be(teamA.Id);
    }

    [Fact]
    public async Task EnterScoreAsync_AdvancesWinnerToNextMatch_AsAwayTeam()
    {
        // Arrange - Match with odd index (1) should advance winner to next match's away team
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamC = await CreateTestTeam(tournament.Id, "Team C");
        var teamD = await CreateTestTeam(tournament.Id, "Team D");

        // Create semifinal match (next match for this quarterfinal)
        var semifinal = await CreateTestMatch(tournament.Id, null, null, round: 2, matchNumber: 1, bracketPosition: "SF1");

        // Create quarterfinal match with nextMatch reference (odd match number)
        var quarterfinal = await CreateTestMatch(
            tournament.Id,
            teamC.Id,
            teamD.Id,
            round: 1,
            matchNumber: 2,
            bracketPosition: "QF2",
            nextMatchId: semifinal.Id);

        var request = new EnterScoreRequest
        {
            HomeScore = 3,
            AwayScore = 6
        };

        // Act
        var result = await _sut.EnterScoreAsync(tournament.Id, quarterfinal.Id, request, user.Id);

        // Assert
        result.Should().NotBeNull();

        // Verify the semifinal match has been updated with the winner
        var updatedSemifinal = await _context.TournamentMatches.FindAsync(semifinal.Id);
        updatedSemifinal.Should().NotBeNull();
        updatedSemifinal!.AwayTeamId.Should().Be(teamD.Id);
    }

    [Fact]
    public async Task EnterScoreAsync_FinalMatch_DoesNotAdvance()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");

        // Create final match with no nextMatch
        var finalMatch = await CreateTestMatch(
            tournament.Id,
            teamA.Id,
            teamB.Id,
            round: 3,
            matchNumber: 1,
            bracketPosition: "Final",
            nextMatchId: null);

        var request = new EnterScoreRequest
        {
            HomeScore = 4,
            AwayScore = 3
        };

        // Act
        var result = await _sut.EnterScoreAsync(tournament.Id, finalMatch.Id, request, user.Id);

        // Assert
        result.Should().NotBeNull();
        result.NextMatchId.Should().BeNull();
    }

    [Fact]
    public async Task EnterScoreAsync_FinalMatch_SetsWinnerTeamStatusToWinner()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");

        // Create final match with no nextMatch
        var finalMatch = await CreateTestMatch(
            tournament.Id,
            teamA.Id,
            teamB.Id,
            round: 3,
            matchNumber: 1,
            bracketPosition: "Final",
            nextMatchId: null);

        var request = new EnterScoreRequest
        {
            HomeScore = 5,
            AwayScore = 2
        };

        // Act
        await _sut.EnterScoreAsync(tournament.Id, finalMatch.Id, request, user.Id);

        // Assert
        var updatedTeamA = await _context.TournamentTeams.FindAsync(teamA.Id);
        updatedTeamA.Should().NotBeNull();
        updatedTeamA!.Status.Should().Be("Winner");
    }

    #endregion

    #region Team Statistics Tests

    [Fact]
    public async Task EnterScoreAsync_UpdatesWinnerTeamStats_WinsAndGoals()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id);

        var request = new EnterScoreRequest
        {
            HomeScore = 6,
            AwayScore = 3
        };

        // Act
        await _sut.EnterScoreAsync(tournament.Id, match.Id, request, user.Id);

        // Assert
        var updatedTeamA = await _context.TournamentTeams.FindAsync(teamA.Id);
        updatedTeamA.Should().NotBeNull();
        updatedTeamA!.Wins.Should().Be(1);
        updatedTeamA.Losses.Should().Be(0);
        updatedTeamA.GoalsFor.Should().Be(6);
        updatedTeamA.GoalsAgainst.Should().Be(3);
    }

    [Fact]
    public async Task EnterScoreAsync_UpdatesLoserTeamStats_LossesAndGoals()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id);

        var request = new EnterScoreRequest
        {
            HomeScore = 6,
            AwayScore = 3
        };

        // Act
        await _sut.EnterScoreAsync(tournament.Id, match.Id, request, user.Id);

        // Assert
        var updatedTeamB = await _context.TournamentTeams.FindAsync(teamB.Id);
        updatedTeamB.Should().NotBeNull();
        updatedTeamB!.Wins.Should().Be(0);
        updatedTeamB.Losses.Should().Be(1);
        updatedTeamB.GoalsFor.Should().Be(3);
        updatedTeamB.GoalsAgainst.Should().Be(6);
    }

    [Fact]
    public async Task EnterScoreAsync_TiedGame_RoundRobin_UpdatesBothTeamsTies()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, format: "RoundRobin");
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id);

        var request = new EnterScoreRequest
        {
            HomeScore = 4,
            AwayScore = 4
        };

        // Act
        await _sut.EnterScoreAsync(tournament.Id, match.Id, request, user.Id);

        // Assert
        var updatedTeamA = await _context.TournamentTeams.FindAsync(teamA.Id);
        updatedTeamA.Should().NotBeNull();
        updatedTeamA!.Wins.Should().Be(0);
        updatedTeamA.Losses.Should().Be(0);
        updatedTeamA.Ties.Should().Be(1);
        updatedTeamA.GoalsFor.Should().Be(4);
        updatedTeamA.GoalsAgainst.Should().Be(4);

        var updatedTeamB = await _context.TournamentTeams.FindAsync(teamB.Id);
        updatedTeamB.Should().NotBeNull();
        updatedTeamB!.Wins.Should().Be(0);
        updatedTeamB.Losses.Should().Be(0);
        updatedTeamB.Ties.Should().Be(1);
        updatedTeamB.GoalsFor.Should().Be(4);
        updatedTeamB.GoalsAgainst.Should().Be(4);
    }

    #endregion

    #region Team Status Tests

    [Fact]
    public async Task EnterScoreAsync_Elimination_SetsLoserStatusToEliminated()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, format: "SingleElimination");
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id);

        var request = new EnterScoreRequest
        {
            HomeScore = 5,
            AwayScore = 2
        };

        // Act
        await _sut.EnterScoreAsync(tournament.Id, match.Id, request, user.Id);

        // Assert
        var updatedTeamB = await _context.TournamentTeams.FindAsync(teamB.Id);
        updatedTeamB.Should().NotBeNull();
        updatedTeamB!.Status.Should().Be("Eliminated");
    }

    [Fact]
    public async Task EnterScoreAsync_RoundRobin_DoesNotEliminateLoser()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, format: "RoundRobin");
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id);

        var request = new EnterScoreRequest
        {
            HomeScore = 5,
            AwayScore = 2
        };

        // Act
        await _sut.EnterScoreAsync(tournament.Id, match.Id, request, user.Id);

        // Assert
        var updatedTeamB = await _context.TournamentTeams.FindAsync(teamB.Id);
        updatedTeamB.Should().NotBeNull();
        updatedTeamB!.Status.Should().Be("Active");
    }

    #endregion

    #region Score Editing Tests

    [Fact]
    public async Task EnterScoreAsync_EditExistingScore_OverwritesValues()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id);

        // Enter initial score
        var initialRequest = new EnterScoreRequest
        {
            HomeScore = 3,
            AwayScore = 2
        };
        await _sut.EnterScoreAsync(tournament.Id, match.Id, initialRequest, user.Id);

        // Edit score
        var editRequest = new EnterScoreRequest
        {
            HomeScore = 2,
            AwayScore = 5
        };

        // Act
        var result = await _sut.EnterScoreAsync(tournament.Id, match.Id, editRequest, user.Id);

        // Assert
        result.Should().NotBeNull();
        result.HomeScore.Should().Be(2);
        result.AwayScore.Should().Be(5);
        result.WinnerTeamId.Should().Be(teamB.Id); // Winner changed
    }

    [Fact]
    public async Task EnterScoreAsync_EditExistingScore_UpdatesTeamStatsCorrectly()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id);

        // Enter initial score: Team A wins 5-2
        var initialRequest = new EnterScoreRequest
        {
            HomeScore = 5,
            AwayScore = 2
        };
        await _sut.EnterScoreAsync(tournament.Id, match.Id, initialRequest, user.Id);

        // Edit score: Team B now wins 2-6
        var editRequest = new EnterScoreRequest
        {
            HomeScore = 2,
            AwayScore = 6
        };

        // Act
        await _sut.EnterScoreAsync(tournament.Id, match.Id, editRequest, user.Id);

        // Assert - Team A stats should be updated
        var updatedTeamA = await _context.TournamentTeams.FindAsync(teamA.Id);
        updatedTeamA.Should().NotBeNull();
        updatedTeamA!.Wins.Should().Be(0); // Was 1, now 0
        updatedTeamA.Losses.Should().Be(1); // Was 0, now 1
        updatedTeamA.GoalsFor.Should().Be(2);
        updatedTeamA.GoalsAgainst.Should().Be(6);

        // Assert - Team B stats should be updated
        var updatedTeamB = await _context.TournamentTeams.FindAsync(teamB.Id);
        updatedTeamB.Should().NotBeNull();
        updatedTeamB!.Wins.Should().Be(1); // Was 0, now 1
        updatedTeamB.Losses.Should().Be(0); // Was 1, now 0
        updatedTeamB.GoalsFor.Should().Be(6);
        updatedTeamB.GoalsAgainst.Should().Be(2);
    }

    #endregion

    #region ForfeitMatchAsync Tests

    [Fact]
    public async Task ForfeitMatchAsync_SetsNonForfeitingTeamAsWinner()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id);

        var request = new ForfeitMatchRequest
        {
            ForfeitingTeamId = teamB.Id,
            Reason = "Team did not show up"
        };

        // Act
        var result = await _sut.ForfeitMatchAsync(tournament.Id, match.Id, request, user.Id);

        // Assert
        result.Should().NotBeNull();
        result.WinnerTeamId.Should().Be(teamA.Id);
        result.WinnerTeamName.Should().Be("Team A");
    }

    [Fact]
    public async Task ForfeitMatchAsync_SetsMatchStatusToForfeit()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id);

        var request = new ForfeitMatchRequest
        {
            ForfeitingTeamId = teamA.Id,
            Reason = "Player injury"
        };

        // Act
        var result = await _sut.ForfeitMatchAsync(tournament.Id, match.Id, request, user.Id);

        // Assert
        result.Status.Should().Be("Forfeit");
        result.ForfeitReason.Should().Be("Player injury");
    }

    [Fact]
    public async Task ForfeitMatchAsync_UpdatesTeamStats()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id);

        var request = new ForfeitMatchRequest
        {
            ForfeitingTeamId = teamB.Id,
            Reason = "Forfeit"
        };

        // Act
        await _sut.ForfeitMatchAsync(tournament.Id, match.Id, request, user.Id);

        // Assert - Winner gets a win
        var updatedTeamA = await _context.TournamentTeams.FindAsync(teamA.Id);
        updatedTeamA!.Wins.Should().Be(1);
        updatedTeamA.Losses.Should().Be(0);

        // Assert - Forfeiting team gets a loss
        var updatedTeamB = await _context.TournamentTeams.FindAsync(teamB.Id);
        updatedTeamB!.Wins.Should().Be(0);
        updatedTeamB.Losses.Should().Be(1);
    }

    [Fact]
    public async Task ForfeitMatchAsync_Elimination_SetsForfeitingTeamToEliminated()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, format: "SingleElimination");
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id);

        var request = new ForfeitMatchRequest
        {
            ForfeitingTeamId = teamB.Id,
            Reason = "Forfeit"
        };

        // Act
        await _sut.ForfeitMatchAsync(tournament.Id, match.Id, request, user.Id);

        // Assert
        var updatedTeamB = await _context.TournamentTeams.FindAsync(teamB.Id);
        updatedTeamB!.Status.Should().Be("Eliminated");
    }

    [Fact]
    public async Task ForfeitMatchAsync_RoundRobin_DoesNotEliminateForfeitingTeam()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, format: "RoundRobin");
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id);

        var request = new ForfeitMatchRequest
        {
            ForfeitingTeamId = teamB.Id,
            Reason = "Forfeit"
        };

        // Act
        await _sut.ForfeitMatchAsync(tournament.Id, match.Id, request, user.Id);

        // Assert
        var updatedTeamB = await _context.TournamentTeams.FindAsync(teamB.Id);
        updatedTeamB!.Status.Should().Be("Active");
    }

    [Fact]
    public async Task ForfeitMatchAsync_AdvancesWinnerToNextMatch()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");

        var semifinal = await CreateTestMatch(tournament.Id, null, null, round: 2, matchNumber: 1);
        var quarterfinal = await CreateTestMatch(
            tournament.Id,
            teamA.Id,
            teamB.Id,
            round: 1,
            matchNumber: 1,
            nextMatchId: semifinal.Id);

        var request = new ForfeitMatchRequest
        {
            ForfeitingTeamId = teamB.Id,
            Reason = "Forfeit"
        };

        // Act
        await _sut.ForfeitMatchAsync(tournament.Id, quarterfinal.Id, request, user.Id);

        // Assert
        var updatedSemifinal = await _context.TournamentMatches.FindAsync(semifinal.Id);
        updatedSemifinal!.HomeTeamId.Should().Be(teamA.Id);
    }

    [Fact]
    public async Task ForfeitMatchAsync_InvalidForfeitingTeam_ThrowsInvalidOperationException()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var teamC = await CreateTestTeam(tournament.Id, "Team C");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id);

        var request = new ForfeitMatchRequest
        {
            ForfeitingTeamId = teamC.Id, // Not a participant in this match
            Reason = "Forfeit"
        };

        // Act & Assert
        var act = async () => await _sut.ForfeitMatchAsync(tournament.Id, match.Id, request, user.Id);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not a participant*");
    }

    [Fact]
    public async Task ForfeitMatchAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var unauthorizedUser = await CreateTestUser("unauthorized@example.com");
        var tournament = await CreateTestTournament(creator.Id);
        await AddTournamentAdmin(tournament.Id, creator.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id);

        var request = new ForfeitMatchRequest
        {
            ForfeitingTeamId = teamB.Id,
            Reason = "Forfeit"
        };

        // Act & Assert
        var act = async () => await _sut.ForfeitMatchAsync(tournament.Id, match.Id, request, unauthorizedUser.Id);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task ForfeitMatchAsync_TBDTeams_ThrowsInvalidOperationException()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id);

        var match = await CreateTestMatch(tournament.Id, null, null);

        var request = new ForfeitMatchRequest
        {
            ForfeitingTeamId = Guid.NewGuid(),
            Reason = "Forfeit"
        };

        // Act & Assert
        var act = async () => await _sut.ForfeitMatchAsync(tournament.Id, match.Id, request, user.Id);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*TBD*");
    }

    [Fact]
    public async Task ForfeitMatchAsync_NonInProgressTournament_ThrowsInvalidOperationException()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, status: "Draft");
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id);

        var request = new ForfeitMatchRequest
        {
            ForfeitingTeamId = teamB.Id,
            Reason = "Forfeit"
        };

        // Act & Assert
        var act = async () => await _sut.ForfeitMatchAsync(tournament.Id, match.Id, request, user.Id);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*tournament*InProgress*");
    }

    [Fact]
    public async Task ForfeitMatchAsync_FinalMatch_SetsWinnerTeamStatusToWinner()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");

        var finalMatch = await CreateTestMatch(
            tournament.Id,
            teamA.Id,
            teamB.Id,
            round: 3,
            matchNumber: 1,
            bracketPosition: "Final",
            nextMatchId: null);

        var request = new ForfeitMatchRequest
        {
            ForfeitingTeamId = teamB.Id,
            Reason = "Forfeit in finals"
        };

        // Act
        await _sut.ForfeitMatchAsync(tournament.Id, finalMatch.Id, request, user.Id);

        // Assert
        var updatedTeamA = await _context.TournamentTeams.FindAsync(teamA.Id);
        updatedTeamA!.Status.Should().Be("Winner");
    }

    #endregion

    #region Points Calculation Tests

    [Fact]
    public async Task EnterScoreAsync_WinningTeam_UpdatesPointsCorrectly()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id); // Default: PointsWin=3
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id);

        var request = new EnterScoreRequest
        {
            HomeScore = 5,
            AwayScore = 2
        };

        // Act
        await _sut.EnterScoreAsync(tournament.Id, match.Id, request, user.Id);

        // Assert
        var updatedTeamA = await _context.TournamentTeams.FindAsync(teamA.Id);
        updatedTeamA.Should().NotBeNull();
        updatedTeamA!.Points.Should().Be(3, "winning team should receive PointsWin (default 3)");
    }

    [Fact]
    public async Task EnterScoreAsync_LosingTeam_UpdatesPointsCorrectly()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id); // Default: PointsLoss=0
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id);

        var request = new EnterScoreRequest
        {
            HomeScore = 5,
            AwayScore = 2
        };

        // Act
        await _sut.EnterScoreAsync(tournament.Id, match.Id, request, user.Id);

        // Assert
        var updatedTeamB = await _context.TournamentTeams.FindAsync(teamB.Id);
        updatedTeamB.Should().NotBeNull();
        updatedTeamB!.Points.Should().Be(0, "losing team should receive PointsLoss (default 0)");
    }

    [Fact]
    public async Task EnterScoreAsync_TiedGame_RoundRobin_UpdatesPointsForBothTeams()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, format: "RoundRobin"); // Default: PointsTie=1
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id);

        var request = new EnterScoreRequest
        {
            HomeScore = 4,
            AwayScore = 4
        };

        // Act
        await _sut.EnterScoreAsync(tournament.Id, match.Id, request, user.Id);

        // Assert
        var updatedTeamA = await _context.TournamentTeams.FindAsync(teamA.Id);
        updatedTeamA.Should().NotBeNull();
        updatedTeamA!.Points.Should().Be(1, "tied team should receive PointsTie (default 1)");

        var updatedTeamB = await _context.TournamentTeams.FindAsync(teamB.Id);
        updatedTeamB.Should().NotBeNull();
        updatedTeamB!.Points.Should().Be(1, "tied team should receive PointsTie (default 1)");
    }

    [Fact]
    public async Task EnterScoreAsync_CustomPointValues_UsesTournamentSettings()
    {
        // Arrange
        var user = await CreateTestUser();
        // Custom points: Win=2, Tie=1, Loss=0
        var tournament = await CreateTestTournament(user.Id, pointsWin: 2, pointsTie: 1, pointsLoss: 0);
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id);

        var request = new EnterScoreRequest
        {
            HomeScore = 3,
            AwayScore = 1
        };

        // Act
        await _sut.EnterScoreAsync(tournament.Id, match.Id, request, user.Id);

        // Assert
        var updatedTeamA = await _context.TournamentTeams.FindAsync(teamA.Id);
        updatedTeamA.Should().NotBeNull();
        updatedTeamA!.Points.Should().Be(2, "winning team should receive custom PointsWin (2)");

        var updatedTeamB = await _context.TournamentTeams.FindAsync(teamB.Id);
        updatedTeamB.Should().NotBeNull();
        updatedTeamB!.Points.Should().Be(0, "losing team should receive custom PointsLoss (0)");
    }

    [Fact]
    public async Task EnterScoreAsync_EditScore_RecalculatesPointsCorrectly()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id); // Default: PointsWin=3, PointsLoss=0
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id);

        // Enter initial score: Team A wins 5-2
        var initialRequest = new EnterScoreRequest
        {
            HomeScore = 5,
            AwayScore = 2
        };
        await _sut.EnterScoreAsync(tournament.Id, match.Id, initialRequest, user.Id);

        // Edit score: Team B now wins 2-6
        var editRequest = new EnterScoreRequest
        {
            HomeScore = 2,
            AwayScore = 6
        };

        // Act
        await _sut.EnterScoreAsync(tournament.Id, match.Id, editRequest, user.Id);

        // Assert - Team A should have points reversed from 3 to 0
        var updatedTeamA = await _context.TournamentTeams.FindAsync(teamA.Id);
        updatedTeamA.Should().NotBeNull();
        updatedTeamA!.Points.Should().Be(0, "team A should have old win points (3) reversed and new loss points (0) applied");

        // Assert - Team B should have points reversed from 0 to 3
        var updatedTeamB = await _context.TournamentTeams.FindAsync(teamB.Id);
        updatedTeamB.Should().NotBeNull();
        updatedTeamB!.Points.Should().Be(3, "team B should have old loss points (0) reversed and new win points (3) applied");
    }

    [Fact]
    public async Task EnterScoreAsync_MultipleGames_AccumulatesPoints()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, format: "RoundRobin"); // Default: PointsWin=3
        await AddTournamentAdmin(tournament.Id, user.Id);

        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var teamC = await CreateTestTeam(tournament.Id, "Team C");

        // Match 1: Team A beats Team B
        var match1 = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id, matchNumber: 1);
        await _sut.EnterScoreAsync(tournament.Id, match1.Id, new EnterScoreRequest { HomeScore = 5, AwayScore = 2 }, user.Id);

        // Match 2: Team A beats Team C
        var match2 = await CreateTestMatch(tournament.Id, teamA.Id, teamC.Id, matchNumber: 2);
        await _sut.EnterScoreAsync(tournament.Id, match2.Id, new EnterScoreRequest { HomeScore = 4, AwayScore = 1 }, user.Id);

        // Act - Verify Team A's accumulated points
        var updatedTeamA = await _context.TournamentTeams.FindAsync(teamA.Id);

        // Assert
        updatedTeamA.Should().NotBeNull();
        updatedTeamA!.Points.Should().Be(6, "team A should have 2 wins * 3 points = 6 total points");
    }

    #endregion
}
