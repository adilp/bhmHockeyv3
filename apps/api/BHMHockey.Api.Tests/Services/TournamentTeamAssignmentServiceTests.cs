using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// Tests for TournamentTeamAssignmentService - TDD approach.
/// Tests written FIRST before implementation.
///
/// These tests ensure:
/// - Players can be assigned to teams with proper validation
/// - Auto-assignment distributes players fairly (goalies first, skill balance)
/// - Team assignments respect tournament status constraints
/// - Admin permissions are enforced
/// - Team member records are created correctly
/// - Registration status transitions correctly (Registered -> Assigned)
/// </summary>
public class TournamentTeamAssignmentServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<ITournamentService> _mockTournamentService;
    private readonly TournamentTeamAssignmentService _sut;
    private readonly TournamentAuthorizationService _authService;

    public TournamentTeamAssignmentServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new AppDbContext(options);
        _authService = new TournamentAuthorizationService(_context);
        _mockTournamentService = new Mock<ITournamentService>();

        _sut = new TournamentTeamAssignmentService(_context, _mockTournamentService.Object, _authService);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Helper Methods

    private async Task<User> CreateTestUser(
        string email = "test@example.com",
        string position = "skater",
        string skillLevel = "Silver")
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = "hashed_password",
            FirstName = "Test",
            LastName = "User",
            Positions = new Dictionary<string, string> { { position, skillLevel } },
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
        string status = "Open",
        string teamFormation = "OrganizerAssigned",
        int maxTeams = 4)
    {
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            CreatorId = creatorId,
            Name = "Test Tournament",
            Description = "Test Tournament Description",
            Format = "SingleElimination",
            TeamFormation = teamFormation,
            Status = status,
            StartDate = DateTime.UtcNow.AddDays(30),
            EndDate = DateTime.UtcNow.AddDays(32),
            RegistrationDeadline = DateTime.UtcNow.AddDays(25),
            MaxTeams = maxTeams,
            MinPlayersPerTeam = 5,
            MaxPlayersPerTeam = 10,
            AllowMultiTeam = false,
            AllowSubstitutions = true,
            EntryFee = 50,
            FeeType = "PerPlayer",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Tournaments.Add(tournament);

        // Add creator as tournament admin
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

    private async Task<TournamentTeam> CreateTestTeam(
        Guid tournamentId,
        string name = "Team Alpha")
    {
        var team = new TournamentTeam
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            Name = name,
            Status = "Registered",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TournamentTeams.Add(team);
        await _context.SaveChangesAsync();
        return team;
    }

    private async Task<TournamentRegistration> CreateTestRegistration(
        Guid tournamentId,
        Guid userId,
        string status = "Registered",
        string position = "Skater")
    {
        var registration = new TournamentRegistration
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            UserId = userId,
            Status = status,
            Position = position,
            RegisteredAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TournamentRegistrations.Add(registration);
        await _context.SaveChangesAsync();
        return registration;
    }

    #endregion

    #region AssignPlayerToTeamAsync Tests

    [Fact]
    public async Task AssignPlayerToTeam_ShouldAssignPlayerAndUpdateStatus()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var player = await CreateTestUser("player@example.com");
        var tournament = await CreateTestTournament(creator.Id, "Open");
        var team = await CreateTestTeam(tournament.Id, "Team 1");
        var registration = await CreateTestRegistration(tournament.Id, player.Id, "Registered");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.AssignPlayerToTeamAsync(tournament.Id, registration.Id, team.Id, creator.Id);

        // Assert
        result.Should().NotBeNull();
        result!.AssignedTeamId.Should().Be(team.Id);
        result.Status.Should().Be("Assigned");

        // Verify in database
        var updated = await _context.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.Id == registration.Id);
        updated!.AssignedTeamId.Should().Be(team.Id);
        updated.Status.Should().Be("Assigned");
    }

    [Fact]
    public async Task AssignPlayerToTeam_ShouldCreateTeamMemberRecord()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var player = await CreateTestUser("player@example.com");
        var tournament = await CreateTestTournament(creator.Id, "Open");
        var team = await CreateTestTeam(tournament.Id, "Team 1");
        var registration = await CreateTestRegistration(tournament.Id, player.Id, "Registered");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.AssignPlayerToTeamAsync(tournament.Id, registration.Id, team.Id, creator.Id);

        // Assert
        result.Should().NotBeNull();

        // Verify team member record created
        var teamMember = await _context.TournamentTeamMembers
            .FirstOrDefaultAsync(tm => tm.TeamId == team.Id && tm.UserId == player.Id);
        teamMember.Should().NotBeNull();
        teamMember!.Role.Should().Be("Player");
        teamMember.JoinedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AssignPlayerToTeam_WhenTournamentInProgress_ShouldThrowInvalidOperation()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var player = await CreateTestUser("player@example.com");
        var tournament = await CreateTestTournament(creator.Id, "InProgress");
        var team = await CreateTestTeam(tournament.Id, "Team 1");
        var registration = await CreateTestRegistration(tournament.Id, player.Id, "Registered");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.AssignPlayerToTeamAsync(tournament.Id, registration.Id, team.Id, creator.Id));

        exception.Message.Should().Contain("Cannot assign players");
    }

    [Fact]
    public async Task AssignPlayerToTeam_WhenUserNotAdmin_ShouldThrowUnauthorized()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var nonAdmin = await CreateTestUser("nonadmin@example.com");
        var player = await CreateTestUser("player@example.com");
        var tournament = await CreateTestTournament(creator.Id, "Open");
        var team = await CreateTestTeam(tournament.Id, "Team 1");
        var registration = await CreateTestRegistration(tournament.Id, player.Id, "Registered");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, nonAdmin.Id))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.AssignPlayerToTeamAsync(tournament.Id, registration.Id, team.Id, nonAdmin.Id));

        // Verify no changes made
        var unchanged = await _context.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.Id == registration.Id);
        unchanged!.AssignedTeamId.Should().BeNull();
        unchanged.Status.Should().Be("Registered");
    }

    [Fact]
    public async Task AssignPlayerToTeam_WhenRegistrationNotFound_ShouldReturnNull()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var tournament = await CreateTestTournament(creator.Id, "Open");
        var team = await CreateTestTeam(tournament.Id, "Team 1");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.AssignPlayerToTeamAsync(tournament.Id, Guid.NewGuid(), team.Id, creator.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task AssignPlayerToTeam_WhenTeamNotFound_ShouldThrowInvalidOperation()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var player = await CreateTestUser("player@example.com");
        var tournament = await CreateTestTournament(creator.Id, "Open");
        var registration = await CreateTestRegistration(tournament.Id, player.Id, "Registered");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.AssignPlayerToTeamAsync(tournament.Id, registration.Id, Guid.NewGuid(), creator.Id));

        exception.Message.Should().Contain("Team not found");
    }

    #endregion

    #region AutoAssignTeamsAsync Tests

    [Fact]
    public async Task AutoAssign_ShouldDistributeGoaliesEvenly()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var tournament = await CreateTestTournament(creator.Id, "Open", maxTeams: 2);
        var team1 = await CreateTestTeam(tournament.Id, "Team 1");
        var team2 = await CreateTestTeam(tournament.Id, "Team 2");

        // Create 2 goalies and 6 skaters
        var goalie1 = await CreateTestUser("goalie1@example.com", "goalie", "Gold");
        var goalie2 = await CreateTestUser("goalie2@example.com", "goalie", "Silver");

        await CreateTestRegistration(tournament.Id, goalie1.Id, "Registered", "Goalie");
        await CreateTestRegistration(tournament.Id, goalie2.Id, "Registered", "Goalie");

        for (int i = 1; i <= 6; i++)
        {
            var skater = await CreateTestUser($"skater{i}@example.com", "skater", "Silver");
            await CreateTestRegistration(tournament.Id, skater.Id, "Registered", "Skater");
        }

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.AutoAssignTeamsAsync(tournament.Id, false, creator.Id);

        // Assert
        result.Should().NotBeNull();
        result.AssignedCount.Should().Be(8);

        // Each team should have exactly 1 goalie
        var team1Members = await _context.TournamentTeamMembers
            .Include(tm => tm.User)
            .Where(tm => tm.TeamId == team1.Id)
            .ToListAsync();

        var team2Members = await _context.TournamentTeamMembers
            .Include(tm => tm.User)
            .Where(tm => tm.TeamId == team2.Id)
            .ToListAsync();

        var team1Goalies = team1Members.Count(tm => tm.User.Positions!.ContainsKey("goalie"));
        var team2Goalies = team2Members.Count(tm => tm.User.Positions!.ContainsKey("goalie"));

        team1Goalies.Should().Be(1);
        team2Goalies.Should().Be(1);
    }

    [Fact]
    public async Task AutoAssign_WithSkillBalance_ShouldSnakeDraftBySkillLevel()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var tournament = await CreateTestTournament(creator.Id, "Open", maxTeams: 2);
        var team1 = await CreateTestTeam(tournament.Id, "Team 1");
        var team2 = await CreateTestTeam(tournament.Id, "Team 2");

        // Create players with different skill levels (no goalies for simplicity)
        var gold1 = await CreateTestUser("gold1@example.com", "skater", "Gold");
        var gold2 = await CreateTestUser("gold2@example.com", "skater", "Gold");
        var silver1 = await CreateTestUser("silver1@example.com", "skater", "Silver");
        var silver2 = await CreateTestUser("silver2@example.com", "skater", "Silver");
        var bronze1 = await CreateTestUser("bronze1@example.com", "skater", "Bronze");
        var bronze2 = await CreateTestUser("bronze2@example.com", "skater", "Bronze");

        await CreateTestRegistration(tournament.Id, gold1.Id, "Registered", "Skater");
        await CreateTestRegistration(tournament.Id, gold2.Id, "Registered", "Skater");
        await CreateTestRegistration(tournament.Id, silver1.Id, "Registered", "Skater");
        await CreateTestRegistration(tournament.Id, silver2.Id, "Registered", "Skater");
        await CreateTestRegistration(tournament.Id, bronze1.Id, "Registered", "Skater");
        await CreateTestRegistration(tournament.Id, bronze2.Id, "Registered", "Skater");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.AutoAssignTeamsAsync(tournament.Id, true, creator.Id);

        // Assert
        result.Should().NotBeNull();
        result.AssignedCount.Should().Be(6);

        // Each team should have players distributed evenly (snake draft)
        var team1Members = await _context.TournamentTeamMembers
            .Include(tm => tm.User)
            .Where(tm => tm.TeamId == team1.Id)
            .ToListAsync();

        var team2Members = await _context.TournamentTeamMembers
            .Include(tm => tm.User)
            .Where(tm => tm.TeamId == team2.Id)
            .ToListAsync();

        // Each team should have 3 players
        team1Members.Should().HaveCount(3);
        team2Members.Should().HaveCount(3);

        // Each team should have mix of skill levels (not all gold on one team)
        var team1SkillLevels = team1Members
            .Select(tm => tm.User.Positions!.Values.First())
            .Distinct()
            .ToList();

        var team2SkillLevels = team2Members
            .Select(tm => tm.User.Positions!.Values.First())
            .Distinct()
            .ToList();

        // Both teams should have variety (at least 2 different skill levels)
        team1SkillLevels.Should().HaveCountGreaterOrEqualTo(2);
        team2SkillLevels.Should().HaveCountGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task AutoAssign_WithoutSkillBalance_ShouldDistributeRandomly()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var tournament = await CreateTestTournament(creator.Id, "Open", maxTeams: 2);
        var team1 = await CreateTestTeam(tournament.Id, "Team 1");
        var team2 = await CreateTestTeam(tournament.Id, "Team 2");

        // Create 8 skaters
        for (int i = 1; i <= 8; i++)
        {
            var skater = await CreateTestUser($"skater{i}@example.com", "skater", "Silver");
            await CreateTestRegistration(tournament.Id, skater.Id, "Registered", "Skater");
        }

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.AutoAssignTeamsAsync(tournament.Id, false, creator.Id);

        // Assert
        result.Should().NotBeNull();
        result.AssignedCount.Should().Be(8);

        // Each team should have 4 players
        var team1Count = await _context.TournamentTeamMembers
            .CountAsync(tm => tm.TeamId == team1.Id);
        var team2Count = await _context.TournamentTeamMembers
            .CountAsync(tm => tm.TeamId == team2.Id);

        team1Count.Should().Be(4);
        team2Count.Should().Be(4);
    }

    [Fact]
    public async Task AutoAssign_ShouldUpdateRegistrationStatusToAssigned()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var tournament = await CreateTestTournament(creator.Id, "Open");
        var team = await CreateTestTeam(tournament.Id, "Team 1");

        var player = await CreateTestUser("player@example.com");
        var registration = await CreateTestRegistration(tournament.Id, player.Id, "Registered");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.AutoAssignTeamsAsync(tournament.Id, false, creator.Id);

        // Assert
        result.Should().NotBeNull();

        // Verify registration status updated
        var updated = await _context.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.Id == registration.Id);
        updated!.Status.Should().Be("Assigned");
        updated.AssignedTeamId.Should().NotBeNull();
    }

    [Fact]
    public async Task AutoAssign_WhenNoUnassignedPlayers_ShouldReturnZeroCount()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var tournament = await CreateTestTournament(creator.Id, "Open");
        var team = await CreateTestTeam(tournament.Id, "Team 1");

        // Create player already assigned
        var player = await CreateTestUser("player@example.com");
        var registration = await CreateTestRegistration(tournament.Id, player.Id, "Assigned");
        registration.AssignedTeamId = team.Id;
        await _context.SaveChangesAsync();

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.AutoAssignTeamsAsync(tournament.Id, false, creator.Id);

        // Assert
        result.Should().NotBeNull();
        result.AssignedCount.Should().Be(0);
    }

    [Fact]
    public async Task AutoAssign_WhenTournamentInProgress_ShouldThrowInvalidOperation()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var tournament = await CreateTestTournament(creator.Id, "InProgress");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.AutoAssignTeamsAsync(tournament.Id, false, creator.Id));

        exception.Message.Should().Contain("Cannot assign players");
    }

    #endregion

    #region BulkCreateTeamsAsync Tests

    [Fact]
    public async Task BulkCreateTeams_ShouldCreateTeamsWithSequentialNames()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var tournament = await CreateTestTournament(creator.Id, "Open");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.BulkCreateTeamsAsync(tournament.Id, 4, "Team", creator.Id);

        // Assert
        result.Should().NotBeNull();
        result.Teams.Should().HaveCount(4);
        result.Teams[0].Name.Should().Be("Team 1");
        result.Teams[1].Name.Should().Be("Team 2");
        result.Teams[2].Name.Should().Be("Team 3");
        result.Teams[3].Name.Should().Be("Team 4");

        // Verify in database
        var teams = await _context.TournamentTeams
            .Where(t => t.TournamentId == tournament.Id)
            .OrderBy(t => t.Name)
            .ToListAsync();

        teams.Should().HaveCount(4);
        teams[0].Name.Should().Be("Team 1");
        teams[0].Status.Should().Be("Registered");
    }

    [Fact]
    public async Task BulkCreateTeams_WhenTournamentInProgress_ShouldThrowInvalidOperation()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var tournament = await CreateTestTournament(creator.Id, "InProgress");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.BulkCreateTeamsAsync(tournament.Id, 4, "Team", creator.Id));

        exception.Message.Should().Contain("Cannot create teams");
    }

    [Fact]
    public async Task BulkCreateTeams_WhenUserNotAdmin_ShouldThrowUnauthorized()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var nonAdmin = await CreateTestUser("nonadmin@example.com");
        var tournament = await CreateTestTournament(creator.Id, "Open");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, nonAdmin.Id))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.BulkCreateTeamsAsync(tournament.Id, 4, "Team", nonAdmin.Id));

        // Verify no teams created
        var teams = await _context.TournamentTeams
            .Where(t => t.TournamentId == tournament.Id)
            .ToListAsync();
        teams.Should().BeEmpty();
    }

    #endregion

    #region RemovePlayerFromTeamAsync Tests

    [Fact]
    public async Task RemovePlayerFromTeam_ShouldDeleteTeamMemberAndClearAssignment()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var player = await CreateTestUser("player@example.com");
        var tournament = await CreateTestTournament(creator.Id, "Open");
        var team = await CreateTestTeam(tournament.Id, "Team 1");
        var registration = await CreateTestRegistration(tournament.Id, player.Id, "Assigned");

        // Manually assign player
        registration.AssignedTeamId = team.Id;
        var teamMember = new TournamentTeamMember
        {
            Id = Guid.NewGuid(),
            TeamId = team.Id,
            UserId = player.Id,
            Role = "Player",
            JoinedAt = DateTime.UtcNow
        };
        _context.TournamentTeamMembers.Add(teamMember);
        await _context.SaveChangesAsync();

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.RemovePlayerFromTeamAsync(tournament.Id, registration.Id, creator.Id);

        // Assert
        result.Should().BeTrue();

        // Verify team member deleted
        var deletedMember = await _context.TournamentTeamMembers
            .FirstOrDefaultAsync(tm => tm.TeamId == team.Id && tm.UserId == player.Id);
        deletedMember.Should().BeNull();

        // Verify registration cleared
        var updated = await _context.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.Id == registration.Id);
        updated!.AssignedTeamId.Should().BeNull();
        updated.Status.Should().Be("Registered");
    }

    [Fact]
    public async Task RemovePlayerFromTeam_WhenNotAssigned_ShouldReturnFalse()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var player = await CreateTestUser("player@example.com");
        var tournament = await CreateTestTournament(creator.Id, "Open");
        var registration = await CreateTestRegistration(tournament.Id, player.Id, "Registered");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.RemovePlayerFromTeamAsync(tournament.Id, registration.Id, creator.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RemovePlayerFromTeam_WhenUserNotAdmin_ShouldThrowUnauthorized()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var nonAdmin = await CreateTestUser("nonadmin@example.com");
        var player = await CreateTestUser("player@example.com");
        var tournament = await CreateTestTournament(creator.Id, "Open");
        var team = await CreateTestTeam(tournament.Id, "Team 1");
        var registration = await CreateTestRegistration(tournament.Id, player.Id, "Assigned");
        registration.AssignedTeamId = team.Id;
        await _context.SaveChangesAsync();

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, nonAdmin.Id))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.RemovePlayerFromTeamAsync(tournament.Id, registration.Id, nonAdmin.Id));

        // Verify no changes
        var unchanged = await _context.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.Id == registration.Id);
        unchanged!.AssignedTeamId.Should().Be(team.Id);
        unchanged.Status.Should().Be("Assigned");
    }

    [Fact]
    public async Task RemovePlayerFromTeam_WhenTournamentInProgress_ShouldThrowInvalidOperation()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var player = await CreateTestUser("player@example.com");
        var tournament = await CreateTestTournament(creator.Id, "InProgress");
        var team = await CreateTestTeam(tournament.Id, "Team 1");
        var registration = await CreateTestRegistration(tournament.Id, player.Id, "Assigned");
        registration.AssignedTeamId = team.Id;
        await _context.SaveChangesAsync();

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RemovePlayerFromTeamAsync(tournament.Id, registration.Id, creator.Id));

        exception.Message.Should().Contain("Cannot remove players");
    }

    [Fact]
    public async Task RemovePlayerFromTeam_WhenRegistrationNotFound_ShouldReturnFalse()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var tournament = await CreateTestTournament(creator.Id, "Open");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.RemovePlayerFromTeamAsync(tournament.Id, Guid.NewGuid(), creator.Id);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Edge Cases and Integration Tests

    [Fact]
    public async Task AutoAssign_WithOddNumberOfPlayers_ShouldDistributeEvenly()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var tournament = await CreateTestTournament(creator.Id, "Open", maxTeams: 3);
        await CreateTestTeam(tournament.Id, "Team 1");
        await CreateTestTeam(tournament.Id, "Team 2");
        await CreateTestTeam(tournament.Id, "Team 3");

        // Create 7 players (not evenly divisible by 3)
        for (int i = 1; i <= 7; i++)
        {
            var player = await CreateTestUser($"player{i}@example.com", "skater", "Silver");
            await CreateTestRegistration(tournament.Id, player.Id, "Registered", "Skater");
        }

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.AutoAssignTeamsAsync(tournament.Id, false, creator.Id);

        // Assert
        result.Should().NotBeNull();
        result.AssignedCount.Should().Be(7);

        // Verify distribution is relatively even (max difference of 1)
        var team1Count = await _context.TournamentTeamMembers.CountAsync(tm => tm.Team.Name == "Team 1");
        var team2Count = await _context.TournamentTeamMembers.CountAsync(tm => tm.Team.Name == "Team 2");
        var team3Count = await _context.TournamentTeamMembers.CountAsync(tm => tm.Team.Name == "Team 3");

        var counts = new[] { team1Count, team2Count, team3Count };
        (counts.Max() - counts.Min()).Should().BeLessOrEqualTo(1);
    }

    [Fact]
    public async Task AutoAssign_WithMoreGoaliesThanTeams_ShouldDistributeExtraGoalies()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var tournament = await CreateTestTournament(creator.Id, "Open", maxTeams: 2);
        await CreateTestTeam(tournament.Id, "Team 1");
        await CreateTestTeam(tournament.Id, "Team 2");

        // Create 3 goalies (more than teams)
        for (int i = 1; i <= 3; i++)
        {
            var goalie = await CreateTestUser($"goalie{i}@example.com", "goalie", "Gold");
            await CreateTestRegistration(tournament.Id, goalie.Id, "Registered", "Goalie");
        }

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.AutoAssignTeamsAsync(tournament.Id, false, creator.Id);

        // Assert
        result.Should().NotBeNull();
        result.AssignedCount.Should().Be(3);

        // One team should have 2 goalies, the other should have 1
        var team1Goalies = await _context.TournamentTeamMembers
            .Include(tm => tm.User)
            .Where(tm => tm.Team.Name == "Team 1" && tm.User.Positions!.ContainsKey("goalie"))
            .CountAsync();

        var team2Goalies = await _context.TournamentTeamMembers
            .Include(tm => tm.User)
            .Where(tm => tm.Team.Name == "Team 2" && tm.User.Positions!.ContainsKey("goalie"))
            .CountAsync();

        (team1Goalies + team2Goalies).Should().Be(3);
        Math.Abs(team1Goalies - team2Goalies).Should().BeLessOrEqualTo(1);
    }

    [Fact]
    public async Task BulkCreateTeams_WithCustomPrefix_ShouldUsePrefix()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var tournament = await CreateTestTournament(creator.Id, "Open");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.BulkCreateTeamsAsync(tournament.Id, 3, "Squad", creator.Id);

        // Assert
        result.Should().NotBeNull();
        result.Teams.Should().HaveCount(3);
        result.Teams[0].Name.Should().Be("Squad 1");
        result.Teams[1].Name.Should().Be("Squad 2");
        result.Teams[2].Name.Should().Be("Squad 3");
    }

    [Fact]
    public async Task AssignPlayerToTeam_WhenAlreadyAssignedToDifferentTeam_ShouldReassign()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var player = await CreateTestUser("player@example.com");
        var tournament = await CreateTestTournament(creator.Id, "Open");
        var team1 = await CreateTestTeam(tournament.Id, "Team 1");
        var team2 = await CreateTestTeam(tournament.Id, "Team 2");
        var registration = await CreateTestRegistration(tournament.Id, player.Id, "Assigned");

        // Initially assigned to team1
        registration.AssignedTeamId = team1.Id;
        var initialMember = new TournamentTeamMember
        {
            Id = Guid.NewGuid(),
            TeamId = team1.Id,
            UserId = player.Id,
            Role = "Player",
            JoinedAt = DateTime.UtcNow
        };
        _context.TournamentTeamMembers.Add(initialMember);
        await _context.SaveChangesAsync();

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        // Act - Reassign to team2
        var result = await _sut.AssignPlayerToTeamAsync(tournament.Id, registration.Id, team2.Id, creator.Id);

        // Assert
        result.Should().NotBeNull();
        result!.AssignedTeamId.Should().Be(team2.Id);

        // Old team member should be deleted
        var oldMember = await _context.TournamentTeamMembers
            .FirstOrDefaultAsync(tm => tm.TeamId == team1.Id && tm.UserId == player.Id);
        oldMember.Should().BeNull();

        // New team member should exist
        var newMember = await _context.TournamentTeamMembers
            .FirstOrDefaultAsync(tm => tm.TeamId == team2.Id && tm.UserId == player.Id);
        newMember.Should().NotBeNull();
    }

    #endregion
}
