using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// Tests for TournamentMatchService - TDD approach for TRN-003.
/// Tests written FIRST before implementation.
/// Read-only operations only for this ticket.
/// </summary>
public class TournamentMatchServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly TournamentMatchService _sut;

    public TournamentMatchServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _sut = new TournamentMatchService(_context);
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

    private async Task<Tournament> CreateTestTournament(Guid creatorId, string status = "InProgress")
    {
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            CreatorId = creatorId,
            Name = "Test Tournament",
            Format = "SingleElimination",
            TeamFormation = "OrganizerAssigned",
            Status = status,
            StartDate = DateTime.UtcNow.AddDays(30),
            EndDate = DateTime.UtcNow.AddDays(32),
            RegistrationDeadline = DateTime.UtcNow.AddDays(25),
            MaxTeams = 8,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Tournaments.Add(tournament);
        await _context.SaveChangesAsync();
        return tournament;
    }

    private async Task<TournamentTeam> CreateTestTeam(Guid tournamentId, string name)
    {
        var team = new TournamentTeam
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            Name = name,
            Status = "Active",
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
        string? bracketPosition = null)
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
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TournamentMatches.Add(match);
        await _context.SaveChangesAsync();
        return match;
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ReturnsAllMatchesForTournament()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var teamC = await CreateTestTeam(tournament.Id, "Team C");
        var teamD = await CreateTestTeam(tournament.Id, "Team D");

        await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id, 1, 1, "Scheduled", "QF1");
        await CreateTestMatch(tournament.Id, teamC.Id, teamD.Id, 1, 2, "Scheduled", "QF2");

        // Act
        var result = await _sut.GetAllAsync(tournament.Id);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyListForTournamentWithNoMatches()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);

        // Act
        var result = await _sut.GetAllAsync(tournament.Id);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_DoesNotReturnMatchesFromOtherTournaments()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament1 = await CreateTestTournament(user.Id);
        var tournament2 = await CreateTestTournament(user.Id);
        var teamA = await CreateTestTeam(tournament1.Id, "Team A");
        var teamB = await CreateTestTeam(tournament1.Id, "Team B");

        await CreateTestMatch(tournament1.Id, teamA.Id, teamB.Id, 1, 1);
        await CreateTestMatch(tournament2.Id, null, null, 1, 1);

        // Act
        var result = await _sut.GetAllAsync(tournament1.Id);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAllAsync_OrdersByRoundThenMatchNumber()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);

        await CreateTestMatch(tournament.Id, null, null, 2, 1, "Scheduled", "SF1");
        await CreateTestMatch(tournament.Id, null, null, 1, 2, "Scheduled", "QF2");
        await CreateTestMatch(tournament.Id, null, null, 1, 1, "Scheduled", "QF1");

        // Act
        var result = await _sut.GetAllAsync(tournament.Id);

        // Assert
        result.Should().HaveCount(3);
        result[0].BracketPosition.Should().Be("QF1");
        result[1].BracketPosition.Should().Be("QF2");
        result[2].BracketPosition.Should().Be("SF1");
    }

    [Fact]
    public async Task GetAllAsync_IncludesTeamNames()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        var teamA = await CreateTestTeam(tournament.Id, "Team Alpha");
        var teamB = await CreateTestTeam(tournament.Id, "Team Beta");

        await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id, 1, 1);

        // Act
        var result = await _sut.GetAllAsync(tournament.Id);

        // Assert
        result.Should().HaveCount(1);
        result[0].HomeTeamName.Should().Be("Team Alpha");
        result[0].AwayTeamName.Should().Be("Team Beta");
    }

    [Fact]
    public async Task GetAllAsync_HandlesNullTeams()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);

        await CreateTestMatch(tournament.Id, null, null, 1, 1, "Scheduled", "TBD");

        // Act
        var result = await _sut.GetAllAsync(tournament.Id);

        // Assert
        result.Should().HaveCount(1);
        result[0].HomeTeamId.Should().BeNull();
        result[0].AwayTeamId.Should().BeNull();
        result[0].HomeTeamName.Should().BeNull();
        result[0].AwayTeamName.Should().BeNull();
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsMatch()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");
        var match = await CreateTestMatch(tournament.Id, teamA.Id, teamB.Id, 1, 1, "Scheduled", "QF1");

        // Act
        var result = await _sut.GetByIdAsync(tournament.Id, match.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(match.Id);
        result.BracketPosition.Should().Be("QF1");
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidMatchId_ReturnsNull()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);

        // Act
        var result = await _sut.GetByIdAsync(tournament.Id, Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WithWrongTournamentId_ReturnsNull()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament1 = await CreateTestTournament(user.Id);
        var tournament2 = await CreateTestTournament(user.Id);
        var match = await CreateTestMatch(tournament1.Id, null, null, 1, 1);

        // Act
        var result = await _sut.GetByIdAsync(tournament2.Id, match.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_IncludesAllFields()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        var teamA = await CreateTestTeam(tournament.Id, "Team A");
        var teamB = await CreateTestTeam(tournament.Id, "Team B");

        var match = new TournamentMatch
        {
            Id = Guid.NewGuid(),
            TournamentId = tournament.Id,
            HomeTeamId = teamA.Id,
            AwayTeamId = teamB.Id,
            Round = 2,
            MatchNumber = 1,
            BracketPosition = "SF1",
            Status = "Completed",
            HomeScore = 3,
            AwayScore = 2,
            WinnerTeamId = teamA.Id,
            ScheduledTime = DateTime.UtcNow.AddDays(1),
            Venue = "Ice Arena",
            IsBye = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TournamentMatches.Add(match);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByIdAsync(tournament.Id, match.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Round.Should().Be(2);
        result.MatchNumber.Should().Be(1);
        result.BracketPosition.Should().Be("SF1");
        result.Status.Should().Be("Completed");
        result.HomeScore.Should().Be(3);
        result.AwayScore.Should().Be(2);
        result.WinnerTeamId.Should().Be(teamA.Id);
        result.Venue.Should().Be("Ice Arena");
        result.IsBye.Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_IncludesWinnerTeamName()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        var teamA = await CreateTestTeam(tournament.Id, "Team Alpha");
        var teamB = await CreateTestTeam(tournament.Id, "Team Beta");

        var match = new TournamentMatch
        {
            Id = Guid.NewGuid(),
            TournamentId = tournament.Id,
            HomeTeamId = teamA.Id,
            AwayTeamId = teamB.Id,
            Round = 1,
            MatchNumber = 1,
            Status = "Completed",
            HomeScore = 5,
            AwayScore = 2,
            WinnerTeamId = teamA.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TournamentMatches.Add(match);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByIdAsync(tournament.Id, match.Id);

        // Assert
        result.Should().NotBeNull();
        result!.WinnerTeamName.Should().Be("Team Alpha");
    }

    [Fact]
    public async Task GetByIdAsync_HandlesByeMatch()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        var teamA = await CreateTestTeam(tournament.Id, "Team A");

        var match = new TournamentMatch
        {
            Id = Guid.NewGuid(),
            TournamentId = tournament.Id,
            HomeTeamId = teamA.Id,
            AwayTeamId = null,
            Round = 1,
            MatchNumber = 1,
            Status = "Bye",
            IsBye = true,
            WinnerTeamId = teamA.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TournamentMatches.Add(match);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByIdAsync(tournament.Id, match.Id);

        // Assert
        result.Should().NotBeNull();
        result!.IsBye.Should().BeTrue();
        result.Status.Should().Be("Bye");
        result.AwayTeamId.Should().BeNull();
    }

    #endregion
}
