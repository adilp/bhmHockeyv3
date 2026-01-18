using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// Tests for BracketGenerationService - TDD approach for TRN-004.
/// Tests written FIRST before implementation.
/// </summary>
public class BracketGenerationServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly BracketGenerationService _sut;
    private readonly ITournamentService _tournamentService;

    public BracketGenerationServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        var orgAdminService = new OrganizationAdminService(_context);
        _tournamentService = new TournamentService(_context, orgAdminService);
        _sut = new BracketGenerationService(_context, _tournamentService);
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

    private async Task<Tournament> CreateTestTournament(Guid creatorId, string status = "Draft")
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

        // Add creator as tournament admin (Owner)
        var admin = new TournamentAdmin
        {
            Id = Guid.NewGuid(),
            TournamentId = tournament.Id,
            UserId = creatorId,
            Role = "Owner",
            AddedAt = DateTime.UtcNow
        };
        _context.TournamentAdmins.Add(admin);

        await _context.SaveChangesAsync();
        return tournament;
    }

    private async Task<TournamentTeam> CreateTestTeam(Guid tournamentId, string name, int? seed = null)
    {
        var team = new TournamentTeam
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            Name = name,
            Status = "Registered",
            Seed = seed,
            Wins = 0,
            Losses = 0,
            Ties = 0,
            Points = 0,
            GoalsFor = 0,
            GoalsAgainst = 0,
            HasBye = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TournamentTeams.Add(team);
        await _context.SaveChangesAsync();
        return team;
    }

    #endregion

    #region GenerateSingleEliminationBracketAsync - Bracket Structure Tests

    [Fact]
    public async Task GenerateBracket_With4Teams_Creates3Matches()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        await CreateTestTeam(tournament.Id, "Team 1", 1);
        await CreateTestTeam(tournament.Id, "Team 2", 2);
        await CreateTestTeam(tournament.Id, "Team 3", 3);
        await CreateTestTeam(tournament.Id, "Team 4", 4);

        // Act
        var result = await _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id);

        // Assert
        result.Should().HaveCount(3);
        result.Count(m => m.Round == 1).Should().Be(2, "4 teams should have 2 semifinals");
        result.Count(m => m.Round == 2).Should().Be(1, "should have 1 final");
        result.Should().AllSatisfy(m => m.IsBye.Should().BeFalse());
    }

    [Fact]
    public async Task GenerateBracket_With8Teams_Creates7Matches()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        for (int i = 1; i <= 8; i++)
        {
            await CreateTestTeam(tournament.Id, $"Team {i}", i);
        }

        // Act
        var result = await _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id);

        // Assert
        result.Should().HaveCount(7);
        result.Count(m => m.Round == 1).Should().Be(4, "8 teams should have 4 quarterfinals");
        result.Count(m => m.Round == 2).Should().Be(2, "should have 2 semifinals");
        result.Count(m => m.Round == 3).Should().Be(1, "should have 1 final");
        result.Should().AllSatisfy(m => m.IsBye.Should().BeFalse());
    }

    [Fact]
    public async Task GenerateBracket_With5Teams_Creates4MatchesWithByes()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        await CreateTestTeam(tournament.Id, "Team 1", 1);
        await CreateTestTeam(tournament.Id, "Team 2", 2);
        await CreateTestTeam(tournament.Id, "Team 3", 3);
        await CreateTestTeam(tournament.Id, "Team 4", 4);
        await CreateTestTeam(tournament.Id, "Team 5", 5);

        // Act
        var result = await _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id);

        // Assert
        result.Should().HaveCount(7, "5 teams in 8-slot bracket = 7 matches");

        result.Count(m => m.Round == 1).Should().Be(4, "Round 1: 3 byes + 1 real match");
        result.Count(m => m.Round == 2).Should().Be(2, "Round 2: 2 semifinals");
        result.Count(m => m.Round == 3).Should().Be(1, "Round 3: 1 final");

        var byeMatches = result.Where(m => m.IsBye).ToList();
        byeMatches.Should().HaveCount(3, "3 bye matches for seeds 1, 2, 3");
        byeMatches.Should().AllSatisfy(m => m.Round.Should().Be(1));

        var realMatches = result.Where(m => !m.IsBye).ToList();
        realMatches.Count(m => m.Round == 1).Should().Be(1, "1 real match in round 1: seed 4 vs 5");

        // Bye matches should be in Round 1, marked as Completed
        byeMatches.Should().AllSatisfy(m =>
        {
            m.Round.Should().Be(1);
            m.Status.Should().Be("Completed");
            m.WinnerTeamId.Should().NotBeNull("bye match should have winner set");
        });
    }

    [Fact]
    public async Task GenerateBracket_With6Teams_Creates5MatchesWithByes()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        for (int i = 1; i <= 6; i++)
        {
            await CreateTestTeam(tournament.Id, $"Team {i}", i);
        }

        // Act
        var result = await _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id);

        // Assert
        result.Should().HaveCount(7, "6 teams in 8-slot bracket = 7 matches");

        result.Count(m => m.Round == 1).Should().Be(4, "Round 1: 2 byes + 2 real matches");
        result.Count(m => m.Round == 2).Should().Be(2, "Round 2: 2 semifinals");
        result.Count(m => m.Round == 3).Should().Be(1, "Round 3: 1 final");

        var byeMatches = result.Where(m => m.IsBye).ToList();
        byeMatches.Should().HaveCount(2, "2 bye matches for seeds 1 and 2");
        byeMatches.Should().AllSatisfy(m => m.Round.Should().Be(1));

        var round1RealMatches = result.Where(m => m.Round == 1 && !m.IsBye).ToList();
        round1RealMatches.Should().HaveCount(2, "2 real matches in round 1");
    }

    [Fact]
    public async Task GenerateBracket_With2Teams_Creates1Match()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        await CreateTestTeam(tournament.Id, "Team 1", 1);
        await CreateTestTeam(tournament.Id, "Team 2", 2);

        // Act
        var result = await _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id);

        // Assert
        result.Should().HaveCount(1, "2 teams should have just the final");
        result[0].Round.Should().Be(1);
        result[0].IsBye.Should().BeFalse();
        result[0].HomeTeamId.Should().NotBeNull();
        result[0].AwayTeamId.Should().NotBeNull();
    }

    #endregion

    #region GenerateSingleEliminationBracketAsync - Seeding Tests

    [Fact]
    public async Task GenerateBracket_With8Teams_UsesCorrectSeeding()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        var teams = new List<TournamentTeam>();
        for (int i = 1; i <= 8; i++)
        {
            teams.Add(await CreateTestTeam(tournament.Id, $"Team {i}", i));
        }

        // Act
        var result = await _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id);

        // Assert - Standard single elimination seeding: 1v8, 4v5, 3v6, 2v7
        var qf = result.Where(m => m.Round == 1).OrderBy(m => m.MatchNumber).ToList();

        // Match 1: Seed 1 vs Seed 8
        var match1Teams = new[] { qf[0].HomeTeamId, qf[0].AwayTeamId };
        match1Teams.Should().Contain(teams[0].Id).And.Contain(teams[7].Id);

        // Match 2: Seed 4 vs Seed 5
        var match2Teams = new[] { qf[1].HomeTeamId, qf[1].AwayTeamId };
        match2Teams.Should().Contain(teams[3].Id).And.Contain(teams[4].Id);

        // Match 3: Seed 3 vs Seed 6
        var match3Teams = new[] { qf[2].HomeTeamId, qf[2].AwayTeamId };
        match3Teams.Should().Contain(teams[2].Id).And.Contain(teams[5].Id);

        // Match 4: Seed 2 vs Seed 7
        var match4Teams = new[] { qf[3].HomeTeamId, qf[3].AwayTeamId };
        match4Teams.Should().Contain(teams[1].Id).And.Contain(teams[6].Id);
    }

    [Fact]
    public async Task GenerateBracket_With8Teams_Seeds1And2InOppositeHalves()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        var teams = new List<TournamentTeam>();
        for (int i = 1; i <= 8; i++)
        {
            teams.Add(await CreateTestTeam(tournament.Id, $"Team {i}", i));
        }

        // Act
        var result = await _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id);

        // Assert - Seeds 1 and 2 should only meet in the final
        var semifinals = result.Where(m => m.Round == 2).OrderBy(m => m.MatchNumber).ToList();

        // Find which SF has seed 1's QF winner
        var qfMatches = result.Where(m => m.Round == 1).ToList();
        var seed1QF = qfMatches.First(m => m.HomeTeamId == teams[0].Id || m.AwayTeamId == teams[0].Id);
        var seed2QF = qfMatches.First(m => m.HomeTeamId == teams[1].Id || m.AwayTeamId == teams[1].Id);

        // Seed 1 and 2's QF matches should feed different SFs
        var sf1 = semifinals[0];
        var sf2 = semifinals[1];

        var seed1FeedsToSf1 = seed1QF.NextMatchId == sf1.Id;
        var seed2FeedsToSf1 = seed2QF.NextMatchId == sf1.Id;

        seed1FeedsToSf1.Should().NotBe(seed2FeedsToSf1,
            "Seeds 1 and 2 should be in opposite halves of the bracket");
    }

    [Fact]
    public async Task GenerateBracket_With4Teams_UsesCorrectSeeding()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        var teams = new List<TournamentTeam>();
        for (int i = 1; i <= 4; i++)
        {
            teams.Add(await CreateTestTeam(tournament.Id, $"Team {i}", i));
        }

        // Act
        var result = await _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id);

        // Assert - Standard 4-team seeding: 1v4, 2v3
        var semis = result.Where(m => m.Round == 1).OrderBy(m => m.MatchNumber).ToList();

        // SF1: Seed 1 vs Seed 4
        var sf1Teams = new[] { semis[0].HomeTeamId, semis[0].AwayTeamId };
        sf1Teams.Should().Contain(teams[0].Id).And.Contain(teams[3].Id);

        // SF2: Seed 2 vs Seed 3
        var sf2Teams = new[] { semis[1].HomeTeamId, semis[1].AwayTeamId };
        sf2Teams.Should().Contain(teams[1].Id).And.Contain(teams[2].Id);
    }

    [Fact]
    public async Task GenerateBracket_WithByeRound_TopSeedsGetByes()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        var teams = new List<TournamentTeam>();
        for (int i = 1; i <= 5; i++)
        {
            teams.Add(await CreateTestTeam(tournament.Id, $"Team {i}", i));
        }

        // Act
        var result = await _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id);

        // Assert
        var byeMatches = result.Where(m => m.IsBye).ToList();

        // Top 3 seeds (1, 2, 3) should get byes
        var teamIdsWithByes = byeMatches
            .Select(m => m.WinnerTeamId)
            .Where(id => id.HasValue)
            .ToList();

        teamIdsWithByes.Should().Contain(teams[0].Id, "Seed 1 should get a bye");
        teamIdsWithByes.Should().Contain(teams[1].Id, "Seed 2 should get a bye");
        teamIdsWithByes.Should().Contain(teams[2].Id, "Seed 3 should get a bye");

        // Seeds 4 and 5 should play each other
        var realMatch = result.First(m => !m.IsBye);
        var playingTeams = new[] { realMatch.HomeTeamId, realMatch.AwayTeamId };
        playingTeams.Should().Contain(teams[3].Id).And.Contain(teams[4].Id);
    }

    #endregion

    #region GenerateSingleEliminationBracketAsync - NextMatchId Linking Tests

    [Fact]
    public async Task GenerateBracket_With8Teams_LinksMatchesCorrectly()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        for (int i = 1; i <= 8; i++)
        {
            await CreateTestTeam(tournament.Id, $"Team {i}", i);
        }

        // Act
        var result = await _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id);

        // Assert
        var qf = result.Where(m => m.Round == 1).OrderBy(m => m.MatchNumber).ToList();
        var sf = result.Where(m => m.Round == 2).OrderBy(m => m.MatchNumber).ToList();
        var final = result.Single(m => m.Round == 3);

        // QF1 and QF2 should feed SF1
        qf[0].NextMatchId.Should().Be(sf[0].Id);
        qf[1].NextMatchId.Should().Be(sf[0].Id);

        // QF3 and QF4 should feed SF2
        qf[2].NextMatchId.Should().Be(sf[1].Id);
        qf[3].NextMatchId.Should().Be(sf[1].Id);

        // Both SFs should feed the Final
        sf[0].NextMatchId.Should().Be(final.Id);
        sf[1].NextMatchId.Should().Be(final.Id);

        // Final should have no next match
        final.NextMatchId.Should().BeNull();
    }

    [Fact]
    public async Task GenerateBracket_With4Teams_LinksMatchesCorrectly()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        for (int i = 1; i <= 4; i++)
        {
            await CreateTestTeam(tournament.Id, $"Team {i}", i);
        }

        // Act
        var result = await _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id);

        // Assert
        var semis = result.Where(m => m.Round == 1).OrderBy(m => m.MatchNumber).ToList();
        var final = result.Single(m => m.Round == 2);

        semis[0].NextMatchId.Should().Be(final.Id);
        semis[1].NextMatchId.Should().Be(final.Id);
        final.NextMatchId.Should().BeNull();
    }

    [Fact]
    public async Task GenerateBracket_WithByes_LinksCorrectlyToNextRound()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        for (int i = 1; i <= 6; i++)
        {
            await CreateTestTeam(tournament.Id, $"Team {i}", i);
        }

        // Act
        var result = await _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id);

        // Assert
        var byeMatches = result.Where(m => m.IsBye).ToList();
        var round1RealMatches = result.Where(m => m.Round == 1 && !m.IsBye).ToList();
        var round2Matches = result.Where(m => m.Round == 2).ToList();

        // All Round 1 matches (byes and real) should link to Round 2
        byeMatches.Should().AllSatisfy(m => m.NextMatchId.Should().NotBeNull());
        round1RealMatches.Should().AllSatisfy(m => m.NextMatchId.Should().NotBeNull());

        // All NextMatchId values should point to valid Round 2 matches
        var allRound1 = result.Where(m => m.Round == 1).ToList();
        var round2Ids = round2Matches.Select(m => m.Id).ToList();
        allRound1.Should().AllSatisfy(m => round2Ids.Should().Contain(m.NextMatchId!.Value));
    }

    #endregion

    #region GenerateSingleEliminationBracketAsync - BracketPosition Label Tests

    [Fact]
    public async Task GenerateBracket_With8Teams_HasCorrectBracketPositions()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        for (int i = 1; i <= 8; i++)
        {
            await CreateTestTeam(tournament.Id, $"Team {i}", i);
        }

        // Act
        var result = await _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id);

        // Assert
        var qf = result.Where(m => m.Round == 1).OrderBy(m => m.MatchNumber).ToList();
        qf[0].BracketPosition.Should().Be("QF1");
        qf[1].BracketPosition.Should().Be("QF2");
        qf[2].BracketPosition.Should().Be("QF3");
        qf[3].BracketPosition.Should().Be("QF4");

        var sf = result.Where(m => m.Round == 2).OrderBy(m => m.MatchNumber).ToList();
        sf[0].BracketPosition.Should().Be("SF1");
        sf[1].BracketPosition.Should().Be("SF2");

        var final = result.Single(m => m.Round == 3);
        final.BracketPosition.Should().Be("Final");
    }

    [Fact]
    public async Task GenerateBracket_With4Teams_HasCorrectBracketPositions()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        for (int i = 1; i <= 4; i++)
        {
            await CreateTestTeam(tournament.Id, $"Team {i}", i);
        }

        // Act
        var result = await _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id);

        // Assert
        var semis = result.Where(m => m.Round == 1).OrderBy(m => m.MatchNumber).ToList();
        semis[0].BracketPosition.Should().Be("SF1");
        semis[1].BracketPosition.Should().Be("SF2");

        var final = result.Single(m => m.Round == 2);
        final.BracketPosition.Should().Be("Final");
    }

    [Fact]
    public async Task GenerateBracket_With3Teams_HasGenericBracketPositions()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        for (int i = 1; i <= 3; i++)
        {
            await CreateTestTeam(tournament.Id, $"Team {i}", i);
        }

        // Act
        var result = await _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id);

        // Assert
        result.Should().HaveCount(3, "3 teams in 4-slot bracket = 3 matches");

        result.Count(m => m.Round == 1).Should().Be(2, "Round 1: 1 bye + 1 real match");
        result.Count(m => m.Round == 2).Should().Be(1, "Round 2: 1 final");

        var round1Matches = result.Where(m => m.Round == 1).OrderBy(m => m.MatchNumber).ToList();
        round1Matches[0].BracketPosition.Should().Be("R1-M1");
        round1Matches[1].BracketPosition.Should().Be("R1-M2");

        var final = result.Single(m => m.Round == 2);
        final.BracketPosition.Should().Be("Final");
    }

    #endregion

    #region GenerateSingleEliminationBracketAsync - Status and Validation Tests

    [Fact]
    public async Task GenerateBracket_InDraftStatus_Succeeds()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        await CreateTestTeam(tournament.Id, "Team 1", 1);
        await CreateTestTeam(tournament.Id, "Team 2", 2);

        // Act
        var result = await _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id);

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateBracket_InOpenStatus_Succeeds()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Open");
        await CreateTestTeam(tournament.Id, "Team 1", 1);
        await CreateTestTeam(tournament.Id, "Team 2", 2);

        // Act
        var result = await _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id);

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateBracket_InRegistrationClosedStatus_Succeeds()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "RegistrationClosed");
        await CreateTestTeam(tournament.Id, "Team 1", 1);
        await CreateTestTeam(tournament.Id, "Team 2", 2);

        // Act
        var result = await _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id);

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateBracket_WithFewerThan2Teams_ThrowsException()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        await CreateTestTeam(tournament.Id, "Team 1", 1);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id));
    }

    [Fact]
    public async Task GenerateBracket_WithZeroTeams_ThrowsException()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id));
    }

    [Fact]
    public async Task GenerateBracket_WhenBracketAlreadyExists_ThrowsException()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        await CreateTestTeam(tournament.Id, "Team 1", 1);
        await CreateTestTeam(tournament.Id, "Team 2", 2);

        // Create an existing match manually to simulate bracket already exists
        var existingMatch = new TournamentMatch
        {
            Id = Guid.NewGuid(),
            TournamentId = tournament.Id,
            Round = 1,
            MatchNumber = 1,
            Status = "Scheduled",
            IsBye = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.TournamentMatches.Add(existingMatch);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id));
    }

    [Fact]
    public async Task GenerateBracket_NonExistentTournament_ThrowsException()
    {
        // Arrange
        var user = await CreateTestUser();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.GenerateSingleEliminationBracketAsync(Guid.NewGuid(), user.Id));
    }

    #endregion

    #region GenerateSingleEliminationBracketAsync - Authorization Tests

    [Fact]
    public async Task GenerateBracket_ByTournamentAdmin_Succeeds()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var tournament = await CreateTestTournament(creator.Id, "Draft");
        await CreateTestTeam(tournament.Id, "Team 1", 1);
        await CreateTestTeam(tournament.Id, "Team 2", 2);

        // Act
        var result = await _sut.GenerateSingleEliminationBracketAsync(tournament.Id, creator.Id);

        // Assert
        result.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateBracket_ByNonAdmin_ThrowsUnauthorized()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var nonAdmin = await CreateTestUser("nonadmin@example.com");
        var tournament = await CreateTestTournament(creator.Id, "Draft");
        await CreateTestTeam(tournament.Id, "Team 1", 1);
        await CreateTestTeam(tournament.Id, "Team 2", 2);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.GenerateSingleEliminationBracketAsync(tournament.Id, nonAdmin.Id));
    }

    #endregion

    #region GenerateSingleEliminationBracketAsync - Match Status Tests

    [Fact]
    public async Task GenerateBracket_ByeMatches_HaveCompletedStatus()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        for (int i = 1; i <= 5; i++)
        {
            await CreateTestTeam(tournament.Id, $"Team {i}", i);
        }

        // Act
        var result = await _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id);

        // Assert
        var byeMatches = result.Where(m => m.IsBye).ToList();
        byeMatches.Should().AllSatisfy(m => m.Status.Should().Be("Completed"));
    }

    [Fact]
    public async Task GenerateBracket_RealMatches_HaveScheduledStatus()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        for (int i = 1; i <= 4; i++)
        {
            await CreateTestTeam(tournament.Id, $"Team {i}", i);
        }

        // Act
        var result = await _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id);

        // Assert
        var realMatches = result.Where(m => !m.IsBye).ToList();
        realMatches.Should().AllSatisfy(m => m.Status.Should().Be("Scheduled"));
    }

    [Fact]
    public async Task GenerateBracket_ByeMatches_HaveWinnerSet()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        var teams = new List<TournamentTeam>();
        for (int i = 1; i <= 5; i++)
        {
            teams.Add(await CreateTestTeam(tournament.Id, $"Team {i}", i));
        }

        // Act
        var result = await _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id);

        // Assert
        var byeMatches = result.Where(m => m.IsBye).ToList();
        byeMatches.Should().AllSatisfy(m =>
        {
            m.WinnerTeamId.Should().NotBeNull("bye matches should have a winner");
        });

        // Winners should be top 3 seeds
        var winnerIds = byeMatches.Select(m => m.WinnerTeamId).ToList();
        winnerIds.Should().Contain(teams[0].Id);
        winnerIds.Should().Contain(teams[1].Id);
        winnerIds.Should().Contain(teams[2].Id);
    }

    #endregion

    #region GenerateSingleEliminationBracketAsync - Round and Match Number Tests

    [Fact]
    public async Task GenerateBracket_AssignsRoundNumbersCorrectly()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        for (int i = 1; i <= 8; i++)
        {
            await CreateTestTeam(tournament.Id, $"Team {i}", i);
        }

        // Act
        var result = await _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id);

        // Assert
        result.Should().AllSatisfy(m => m.Round.Should().BeGreaterThan(0));

        var maxRound = result.Max(m => m.Round);
        maxRound.Should().Be(3, "8 teams should have 3 rounds (QF, SF, Final)");

        // Each round should have expected number of matches
        result.Count(m => m.Round == 1).Should().Be(4);
        result.Count(m => m.Round == 2).Should().Be(2);
        result.Count(m => m.Round == 3).Should().Be(1);
    }

    [Fact]
    public async Task GenerateBracket_AssignsMatchNumbersSequentially()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        for (int i = 1; i <= 8; i++)
        {
            await CreateTestTeam(tournament.Id, $"Team {i}", i);
        }

        // Act
        var result = await _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id);

        // Assert
        foreach (var round in result.GroupBy(m => m.Round))
        {
            var matchNumbers = round.OrderBy(m => m.MatchNumber).Select(m => m.MatchNumber).ToList();
            matchNumbers.Should().BeInAscendingOrder();
            matchNumbers.First().Should().Be(1, "match numbers should start at 1");
            matchNumbers.Should().OnlyHaveUniqueItems("match numbers within a round should be unique");
        }
    }

    #endregion

    #region ClearBracketAsync Tests

    [Fact]
    public async Task ClearBracket_RemovesAllMatches()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        await CreateTestTeam(tournament.Id, "Team 1", 1);
        await CreateTestTeam(tournament.Id, "Team 2", 2);
        await CreateTestTeam(tournament.Id, "Team 3", 3);
        await CreateTestTeam(tournament.Id, "Team 4", 4);

        await _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id);

        var matchesBeforeClear = await _context.TournamentMatches
            .Where(m => m.TournamentId == tournament.Id)
            .ToListAsync();
        matchesBeforeClear.Should().NotBeEmpty();

        // Act
        await _sut.ClearBracketAsync(tournament.Id, user.Id);

        // Assert
        var matchesAfterClear = await _context.TournamentMatches
            .Where(m => m.TournamentId == tournament.Id)
            .ToListAsync();
        matchesAfterClear.Should().BeEmpty();
    }

    [Fact]
    public async Task ClearBracket_WhenNoMatches_DoesNotThrow()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        await CreateTestTeam(tournament.Id, "Team 1", 1);
        await CreateTestTeam(tournament.Id, "Team 2", 2);

        // Act & Assert
        await _sut.ClearBracketAsync(tournament.Id, user.Id);
        // Should not throw
    }

    [Fact]
    public async Task ClearBracket_ByNonAdmin_ThrowsUnauthorized()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var nonAdmin = await CreateTestUser("nonadmin@example.com");
        var tournament = await CreateTestTournament(creator.Id, "Draft");
        await CreateTestTeam(tournament.Id, "Team 1", 1);
        await CreateTestTeam(tournament.Id, "Team 2", 2);

        await _sut.GenerateSingleEliminationBracketAsync(tournament.Id, creator.Id);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.ClearBracketAsync(tournament.Id, nonAdmin.Id));
    }

    [Fact]
    public async Task ClearBracket_DoesNotRemoveTeams()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        await CreateTestTeam(tournament.Id, "Team 1", 1);
        await CreateTestTeam(tournament.Id, "Team 2", 2);
        await CreateTestTeam(tournament.Id, "Team 3", 3);
        await CreateTestTeam(tournament.Id, "Team 4", 4);

        await _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id);

        var teamsBeforeClear = await _context.TournamentTeams
            .Where(t => t.TournamentId == tournament.Id)
            .ToListAsync();

        // Act
        await _sut.ClearBracketAsync(tournament.Id, user.Id);

        // Assert
        var teamsAfterClear = await _context.TournamentTeams
            .Where(t => t.TournamentId == tournament.Id)
            .ToListAsync();
        teamsAfterClear.Should().HaveCount(teamsBeforeClear.Count);
    }

    #endregion

    #region GenerateSingleEliminationBracketAsync - Edge Cases

    [Fact]
    public async Task GenerateBracket_WithTeamsNotSeeded_ThrowsException()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        await CreateTestTeam(tournament.Id, "Team 1", null); // No seed
        await CreateTestTeam(tournament.Id, "Team 2", null); // No seed

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id));
    }

    [Fact]
    public async Task GenerateBracket_WithDuplicateSeeds_ThrowsException()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        await CreateTestTeam(tournament.Id, "Team 1", 1);
        await CreateTestTeam(tournament.Id, "Team 2", 1); // Duplicate seed

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id));
    }

    [Fact]
    public async Task GenerateBracket_WithGapInSeeds_ThrowsException()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        await CreateTestTeam(tournament.Id, "Team 1", 1);
        await CreateTestTeam(tournament.Id, "Team 2", 2);
        await CreateTestTeam(tournament.Id, "Team 3", 4); // Gap: missing seed 3

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id));
    }

    [Fact]
    public async Task GenerateBracket_AssignsUniqueIds()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        for (int i = 1; i <= 4; i++)
        {
            await CreateTestTeam(tournament.Id, $"Team {i}", i);
        }

        // Act
        var result = await _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id);

        // Assert
        var matchIds = result.Select(m => m.Id).ToList();
        matchIds.Should().OnlyHaveUniqueItems("each match should have a unique ID");
    }

    [Fact]
    public async Task GenerateBracket_SetsCreatedAndUpdatedTimestamps()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        await CreateTestTeam(tournament.Id, "Team 1", 1);
        await CreateTestTeam(tournament.Id, "Team 2", 2);

        var beforeGeneration = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var result = await _sut.GenerateSingleEliminationBracketAsync(tournament.Id, user.Id);

        // Assert
        result.Should().AllSatisfy(m =>
        {
            m.CreatedAt.Should().BeAfter(beforeGeneration);
            m.UpdatedAt.Should().BeAfter(beforeGeneration);
        });
    }

    #endregion

    #region Helper Methods for Round Robin Tests

    /// <summary>
    /// Creates a tournament with RoundRobin format for Round Robin tests.
    /// </summary>
    private async Task<Tournament> CreateRoundRobinTournament(Guid creatorId, string status = "Draft")
    {
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            CreatorId = creatorId,
            Name = "Round Robin Tournament",
            Format = "RoundRobin",
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

        // Add creator as tournament admin (Owner)
        var admin = new TournamentAdmin
        {
            Id = Guid.NewGuid(),
            TournamentId = tournament.Id,
            UserId = creatorId,
            Role = "Owner",
            AddedAt = DateTime.UtcNow
        };
        _context.TournamentAdmins.Add(admin);

        await _context.SaveChangesAsync();
        return tournament;
    }

    #endregion

    #region GenerateRoundRobinScheduleAsync - Match Count Tests

    /// <summary>
    /// Test Case 1: 4 teams should generate 6 matches in 3 rounds.
    /// Formula: N*(N-1)/2 = 4*3/2 = 6 matches
    /// Even teams: N-1 = 3 rounds, N/2 = 2 matches per round
    /// </summary>
    [Fact]
    public async Task GenerateRoundRobin_With4Teams_Generates6MatchesIn3Rounds()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateRoundRobinTournament(user.Id, "Draft");
        for (int i = 1; i <= 4; i++)
        {
            await CreateTestTeam(tournament.Id, $"Team {i}", i);
        }

        // Act
        var result = await _sut.GenerateRoundRobinScheduleAsync(tournament.Id, user.Id);

        // Assert
        result.Should().HaveCount(6, "4 teams should generate 4*3/2 = 6 matches");

        var maxRound = result.Max(m => m.Round);
        maxRound.Should().Be(3, "4 teams (even) should have N-1 = 3 rounds");

        // Each round should have N/2 = 2 matches
        for (int round = 1; round <= 3; round++)
        {
            result.Count(m => m.Round == round).Should().Be(2, $"Round {round} should have 2 matches");
        }
    }

    /// <summary>
    /// Test Case 2: 5 teams (odd) should generate 10 matches in 5 rounds with byes.
    /// Formula: N*(N-1)/2 = 5*4/2 = 10 matches
    /// Odd teams: N rounds, some teams get byes each round
    /// </summary>
    [Fact]
    public async Task GenerateRoundRobin_With5Teams_Generates10MatchesIn5Rounds()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateRoundRobinTournament(user.Id, "Draft");
        for (int i = 1; i <= 5; i++)
        {
            await CreateTestTeam(tournament.Id, $"Team {i}", i);
        }

        // Act
        var result = await _sut.GenerateRoundRobinScheduleAsync(tournament.Id, user.Id);

        // Assert
        result.Should().HaveCount(10, "5 teams should generate 5*4/2 = 10 matches");

        var maxRound = result.Max(m => m.Round);
        maxRound.Should().Be(5, "5 teams (odd) should have N = 5 rounds");

        // Each round should have (N-1)/2 = 2 real matches (one team has bye)
        for (int round = 1; round <= 5; round++)
        {
            result.Count(m => m.Round == round).Should().Be(2, $"Round {round} should have 2 matches");
        }
    }

    /// <summary>
    /// Test Case 3: 6 teams should generate 15 matches in 5 rounds.
    /// Formula: N*(N-1)/2 = 6*5/2 = 15 matches
    /// Even teams: N-1 = 5 rounds, N/2 = 3 matches per round
    /// </summary>
    [Fact]
    public async Task GenerateRoundRobin_With6Teams_Generates15MatchesIn5Rounds()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateRoundRobinTournament(user.Id, "Draft");
        for (int i = 1; i <= 6; i++)
        {
            await CreateTestTeam(tournament.Id, $"Team {i}", i);
        }

        // Act
        var result = await _sut.GenerateRoundRobinScheduleAsync(tournament.Id, user.Id);

        // Assert
        result.Should().HaveCount(15, "6 teams should generate 6*5/2 = 15 matches");

        var maxRound = result.Max(m => m.Round);
        maxRound.Should().Be(5, "6 teams (even) should have N-1 = 5 rounds");

        // Each round should have N/2 = 3 matches
        for (int round = 1; round <= 5; round++)
        {
            result.Count(m => m.Round == round).Should().Be(3, $"Round {round} should have 3 matches");
        }
    }

    #endregion

    #region GenerateRoundRobinScheduleAsync - Schedule Validity Tests

    /// <summary>
    /// Test Case 4: Each team plays exactly N-1 games in round robin.
    /// Every team must play against every other team exactly once.
    /// </summary>
    [Fact]
    public async Task GenerateRoundRobin_EachTeamPlaysNMinus1Games()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateRoundRobinTournament(user.Id, "Draft");
        var teams = new List<TournamentTeam>();
        for (int i = 1; i <= 6; i++)
        {
            teams.Add(await CreateTestTeam(tournament.Id, $"Team {i}", i));
        }

        // Act
        var result = await _sut.GenerateRoundRobinScheduleAsync(tournament.Id, user.Id);

        // Assert - Each team should appear in exactly N-1 = 5 matches
        foreach (var team in teams)
        {
            var gamesForTeam = result.Count(m =>
                m.HomeTeamId == team.Id || m.AwayTeamId == team.Id);

            gamesForTeam.Should().Be(5, $"Team {team.Name} should play exactly N-1 = 5 games");
        }
    }

    /// <summary>
    /// Test Case 5: No team plays twice in the same round.
    /// This is critical for scheduling - each team can only be in one match per round.
    /// </summary>
    [Fact]
    public async Task GenerateRoundRobin_NoTeamPlaysTwiceInSameRound()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateRoundRobinTournament(user.Id, "Draft");
        var teams = new List<TournamentTeam>();
        for (int i = 1; i <= 6; i++)
        {
            teams.Add(await CreateTestTeam(tournament.Id, $"Team {i}", i));
        }

        // Act
        var result = await _sut.GenerateRoundRobinScheduleAsync(tournament.Id, user.Id);

        // Assert - In each round, no team should appear more than once
        var rounds = result.GroupBy(m => m.Round);
        foreach (var round in rounds)
        {
            var teamsInRound = round
                .SelectMany(m => new[] { m.HomeTeamId, m.AwayTeamId })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToList();

            teamsInRound.Should().OnlyHaveUniqueItems(
                $"Round {round.Key} should not have any team playing twice");
        }
    }

    /// <summary>
    /// Test Case 6: Every team pair plays exactly once.
    /// This is the fundamental property of round robin - all pairs meet once.
    /// </summary>
    [Fact]
    public async Task GenerateRoundRobin_EachPairPlaysExactlyOnce()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateRoundRobinTournament(user.Id, "Draft");
        var teams = new List<TournamentTeam>();
        for (int i = 1; i <= 6; i++)
        {
            teams.Add(await CreateTestTeam(tournament.Id, $"Team {i}", i));
        }

        // Act
        var result = await _sut.GenerateRoundRobinScheduleAsync(tournament.Id, user.Id);

        // Assert - Every pair of teams should have exactly one match
        for (int i = 0; i < teams.Count; i++)
        {
            for (int j = i + 1; j < teams.Count; j++)
            {
                var teamA = teams[i];
                var teamB = teams[j];

                var matchesBetweenPair = result.Count(m =>
                    (m.HomeTeamId == teamA.Id && m.AwayTeamId == teamB.Id) ||
                    (m.HomeTeamId == teamB.Id && m.AwayTeamId == teamA.Id));

                matchesBetweenPair.Should().Be(1,
                    $"Teams {teamA.Name} and {teamB.Name} should play exactly once");
            }
        }
    }

    #endregion

    #region GenerateRoundRobinScheduleAsync - Home/Away Balance Tests

    /// <summary>
    /// Test Case 7: Home/away should alternate by round for fairness.
    /// Odd rounds swap home/away positions from even rounds.
    /// </summary>
    [Fact]
    public async Task GenerateRoundRobin_AlternatesHomeAwayByRound()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateRoundRobinTournament(user.Id, "Draft");
        var teams = new List<TournamentTeam>();
        for (int i = 1; i <= 4; i++)
        {
            teams.Add(await CreateTestTeam(tournament.Id, $"Team {i}", i));
        }

        // Act
        var result = await _sut.GenerateRoundRobinScheduleAsync(tournament.Id, user.Id);

        // Assert - Each team should have roughly balanced home/away games
        foreach (var team in teams)
        {
            var homeGames = result.Count(m => m.HomeTeamId == team.Id);
            var awayGames = result.Count(m => m.AwayTeamId == team.Id);

            // With N-1 games, difference should be at most 1
            var difference = Math.Abs(homeGames - awayGames);
            difference.Should().BeLessOrEqualTo(1,
                $"Team {team.Name} should have balanced home ({homeGames}) and away ({awayGames}) games");
        }
    }

    #endregion

    #region GenerateRoundRobinScheduleAsync - Bracket Position Tests

    /// <summary>
    /// Test Case 8: BracketPosition should use format "RR-R{round}-M{match}".
    /// This distinguishes round robin matches from elimination matches.
    /// </summary>
    [Fact]
    public async Task GenerateRoundRobin_UseCorrectBracketPositionFormat()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateRoundRobinTournament(user.Id, "Draft");
        for (int i = 1; i <= 4; i++)
        {
            await CreateTestTeam(tournament.Id, $"Team {i}", i);
        }

        // Act
        var result = await _sut.GenerateRoundRobinScheduleAsync(tournament.Id, user.Id);

        // Assert - All matches should have BracketPosition format "RR-R{round}-M{match}"
        foreach (var match in result)
        {
            match.BracketPosition.Should().NotBeNullOrEmpty();
            match.BracketPosition.Should().MatchRegex(@"^RR-R\d+-M\d+$",
                $"Match in round {match.Round} should have format RR-R{{round}}-M{{match}}");

            // Verify the round and match numbers are correct
            var expectedPosition = $"RR-R{match.Round}-M{match.MatchNumber}";
            match.BracketPosition.Should().Be(expectedPosition);
        }
    }

    #endregion

    #region GenerateRoundRobinScheduleAsync - Configuration Tests

    /// <summary>
    /// Test Case 9: All matches should have null ScheduledTime initially.
    /// Times are set later by the tournament admin.
    /// </summary>
    [Fact]
    public async Task GenerateRoundRobin_MatchesHaveNullScheduledTime()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateRoundRobinTournament(user.Id, "Draft");
        for (int i = 1; i <= 4; i++)
        {
            await CreateTestTeam(tournament.Id, $"Team {i}", i);
        }

        // Act
        var result = await _sut.GenerateRoundRobinScheduleAsync(tournament.Id, user.Id);

        // Assert
        result.Should().AllSatisfy(m =>
            m.ScheduledTime.Should().BeNull("matches should not have scheduled time initially"));
    }

    /// <summary>
    /// Test Case 10: All matches should have "Scheduled" status.
    /// Unlike elimination byes, round robin matches are all real games.
    /// </summary>
    [Fact]
    public async Task GenerateRoundRobin_MatchesHaveScheduledStatus()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateRoundRobinTournament(user.Id, "Draft");
        for (int i = 1; i <= 4; i++)
        {
            await CreateTestTeam(tournament.Id, $"Team {i}", i);
        }

        // Act
        var result = await _sut.GenerateRoundRobinScheduleAsync(tournament.Id, user.Id);

        // Assert
        result.Should().AllSatisfy(m =>
            m.Status.Should().Be("Scheduled", "all round robin matches should have Scheduled status"));
    }

    #endregion

    #region GenerateRoundRobinScheduleAsync - Validation Tests

    /// <summary>
    /// Test Case 11: Should throw exception with fewer than 2 teams.
    /// Need at least 2 teams to have a match.
    /// </summary>
    [Fact]
    public async Task GenerateRoundRobin_WithFewerThan2Teams_ThrowsException()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateRoundRobinTournament(user.Id, "Draft");
        await CreateTestTeam(tournament.Id, "Team 1", 1);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.GenerateRoundRobinScheduleAsync(tournament.Id, user.Id));
    }

    /// <summary>
    /// Test Case 12: Non-admin should not be able to generate schedule.
    /// Only tournament admins can generate the schedule.
    /// </summary>
    [Fact]
    public async Task GenerateRoundRobin_ByNonAdmin_ThrowsUnauthorized()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var nonAdmin = await CreateTestUser("nonadmin@example.com");
        var tournament = await CreateRoundRobinTournament(creator.Id, "Draft");
        await CreateTestTeam(tournament.Id, "Team 1", 1);
        await CreateTestTeam(tournament.Id, "Team 2", 2);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.GenerateRoundRobinScheduleAsync(tournament.Id, nonAdmin.Id));
    }

    /// <summary>
    /// Test Case 13: Should throw exception when matches already exist.
    /// Must clear bracket first before regenerating.
    /// </summary>
    [Fact]
    public async Task GenerateRoundRobin_WhenMatchesExist_ThrowsException()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateRoundRobinTournament(user.Id, "Draft");
        await CreateTestTeam(tournament.Id, "Team 1", 1);
        await CreateTestTeam(tournament.Id, "Team 2", 2);

        // Create an existing match manually
        var existingMatch = new TournamentMatch
        {
            Id = Guid.NewGuid(),
            TournamentId = tournament.Id,
            Round = 1,
            MatchNumber = 1,
            Status = "Scheduled",
            IsBye = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.TournamentMatches.Add(existingMatch);
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.GenerateRoundRobinScheduleAsync(tournament.Id, user.Id));
    }

    #endregion

    #region GenerateRoundRobinScheduleAsync - Additional Edge Cases

    /// <summary>
    /// All round robin matches should not be bye matches.
    /// Round robin doesn't have byes in the same way single elimination does.
    /// </summary>
    [Fact]
    public async Task GenerateRoundRobin_AllMatchesAreNotByes()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateRoundRobinTournament(user.Id, "Draft");
        for (int i = 1; i <= 5; i++)
        {
            await CreateTestTeam(tournament.Id, $"Team {i}", i);
        }

        // Act
        var result = await _sut.GenerateRoundRobinScheduleAsync(tournament.Id, user.Id);

        // Assert - No matches should be marked as byes
        result.Should().AllSatisfy(m =>
            m.IsBye.Should().BeFalse("round robin matches should not be bye matches"));
    }

    /// <summary>
    /// All matches should have both home and away teams assigned.
    /// Round robin matches always have two real teams.
    /// </summary>
    [Fact]
    public async Task GenerateRoundRobin_AllMatchesHaveBothTeams()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateRoundRobinTournament(user.Id, "Draft");
        for (int i = 1; i <= 4; i++)
        {
            await CreateTestTeam(tournament.Id, $"Team {i}", i);
        }

        // Act
        var result = await _sut.GenerateRoundRobinScheduleAsync(tournament.Id, user.Id);

        // Assert
        result.Should().AllSatisfy(m =>
        {
            m.HomeTeamId.Should().NotBeNull("all matches should have a home team");
            m.AwayTeamId.Should().NotBeNull("all matches should have an away team");
            m.HomeTeamId!.Value.Should().NotBe(m.AwayTeamId!.Value, "home and away teams should be different");
        });
    }

    /// <summary>
    /// Round robin matches should not have NextMatchId links.
    /// Unlike elimination, there's no bracket progression.
    /// </summary>
    [Fact]
    public async Task GenerateRoundRobin_MatchesHaveNoNextMatchId()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateRoundRobinTournament(user.Id, "Draft");
        for (int i = 1; i <= 4; i++)
        {
            await CreateTestTeam(tournament.Id, $"Team {i}", i);
        }

        // Act
        var result = await _sut.GenerateRoundRobinScheduleAsync(tournament.Id, user.Id);

        // Assert - Round robin matches don't progress to next matches
        result.Should().AllSatisfy(m =>
            m.NextMatchId.Should().BeNull("round robin matches don't have bracket progression"));
    }

    /// <summary>
    /// Match numbers within each round should be sequential starting from 1.
    /// </summary>
    [Fact]
    public async Task GenerateRoundRobin_MatchNumbersAreSequentialWithinRounds()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateRoundRobinTournament(user.Id, "Draft");
        for (int i = 1; i <= 6; i++)
        {
            await CreateTestTeam(tournament.Id, $"Team {i}", i);
        }

        // Act
        var result = await _sut.GenerateRoundRobinScheduleAsync(tournament.Id, user.Id);

        // Assert
        foreach (var round in result.GroupBy(m => m.Round))
        {
            var matchNumbers = round.OrderBy(m => m.MatchNumber)
                .Select(m => m.MatchNumber)
                .ToList();

            matchNumbers.Should().BeInAscendingOrder();
            matchNumbers.First().Should().Be(1, "match numbers should start at 1");
            matchNumbers.Should().OnlyHaveUniqueItems("match numbers within a round should be unique");
        }
    }

    /// <summary>
    /// Each match should have unique ID.
    /// </summary>
    [Fact]
    public async Task GenerateRoundRobin_AssignsUniqueIds()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateRoundRobinTournament(user.Id, "Draft");
        for (int i = 1; i <= 4; i++)
        {
            await CreateTestTeam(tournament.Id, $"Team {i}", i);
        }

        // Act
        var result = await _sut.GenerateRoundRobinScheduleAsync(tournament.Id, user.Id);

        // Assert
        var matchIds = result.Select(m => m.Id).ToList();
        matchIds.Should().OnlyHaveUniqueItems("each match should have a unique ID");
    }

    /// <summary>
    /// Matches should have proper timestamps set.
    /// </summary>
    [Fact]
    public async Task GenerateRoundRobin_SetsCreatedAndUpdatedTimestamps()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateRoundRobinTournament(user.Id, "Draft");
        await CreateTestTeam(tournament.Id, "Team 1", 1);
        await CreateTestTeam(tournament.Id, "Team 2", 2);

        var beforeGeneration = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var result = await _sut.GenerateRoundRobinScheduleAsync(tournament.Id, user.Id);

        // Assert
        result.Should().AllSatisfy(m =>
        {
            m.CreatedAt.Should().BeAfter(beforeGeneration);
            m.UpdatedAt.Should().BeAfter(beforeGeneration);
        });
    }

    /// <summary>
    /// With 2 teams, should generate just 1 match.
    /// Minimum viable round robin.
    /// </summary>
    [Fact]
    public async Task GenerateRoundRobin_With2Teams_Generates1Match()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateRoundRobinTournament(user.Id, "Draft");
        await CreateTestTeam(tournament.Id, "Team 1", 1);
        await CreateTestTeam(tournament.Id, "Team 2", 2);

        // Act
        var result = await _sut.GenerateRoundRobinScheduleAsync(tournament.Id, user.Id);

        // Assert
        result.Should().HaveCount(1, "2 teams should generate 2*1/2 = 1 match");
        result[0].Round.Should().Be(1);
        result[0].HomeTeamId.Should().NotBeNull();
        result[0].AwayTeamId.Should().NotBeNull();
    }

    /// <summary>
    /// With 3 teams (odd), should generate 3 matches in 3 rounds.
    /// Each team plays 2 games, one team sits out each round.
    /// </summary>
    [Fact]
    public async Task GenerateRoundRobin_With3Teams_Generates3MatchesIn3Rounds()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateRoundRobinTournament(user.Id, "Draft");
        for (int i = 1; i <= 3; i++)
        {
            await CreateTestTeam(tournament.Id, $"Team {i}", i);
        }

        // Act
        var result = await _sut.GenerateRoundRobinScheduleAsync(tournament.Id, user.Id);

        // Assert
        result.Should().HaveCount(3, "3 teams should generate 3*2/2 = 3 matches");

        var maxRound = result.Max(m => m.Round);
        maxRound.Should().Be(3, "3 teams (odd) should have N = 3 rounds");

        // Each round should have 1 match (one team has bye)
        for (int round = 1; round <= 3; round++)
        {
            result.Count(m => m.Round == round).Should().Be(1, $"Round {round} should have 1 match");
        }
    }

    /// <summary>
    /// Non-existent tournament should throw exception.
    /// </summary>
    [Fact]
    public async Task GenerateRoundRobin_NonExistentTournament_ThrowsException()
    {
        // Arrange
        var user = await CreateTestUser();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.GenerateRoundRobinScheduleAsync(Guid.NewGuid(), user.Id));
    }

    /// <summary>
    /// Zero teams should throw exception.
    /// </summary>
    [Fact]
    public async Task GenerateRoundRobin_WithZeroTeams_ThrowsException()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateRoundRobinTournament(user.Id, "Draft");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.GenerateRoundRobinScheduleAsync(tournament.Id, user.Id));
    }

    #endregion
}
