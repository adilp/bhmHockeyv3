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
/// Tests for TournamentTeamMemberService - TDD approach for TRN-011.
/// Tests written FIRST before implementation.
///
/// These tests ensure:
/// - Players can be added to teams with Pending status
/// - Team capacity limits are enforced (count Accepted members only)
/// - Players cannot be on multiple teams simultaneously (Pending or Accepted)
/// - Players with Declined status can join other teams
/// - Admin permissions are enforced for add/remove operations
/// - Players can respond to invitations (Accept/Decline)
/// - Accepting creates TournamentRegistration
/// - Soft delete with LeftAt timestamp
/// </summary>
public class TournamentTeamMemberServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<ITournamentService> _mockTournamentService;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly ITournamentTeamMemberService _sut;

    public TournamentTeamMemberServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new AppDbContext(options);
        _mockTournamentService = new Mock<ITournamentService>();
        _mockNotificationService = new Mock<INotificationService>();

        _sut = new TournamentTeamMemberService(
            _context,
            _mockTournamentService.Object,
            _mockNotificationService.Object);
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
        int maxPlayersPerTeam = 10)
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
            MaxTeams = 4,
            MinPlayersPerTeam = 5,
            MaxPlayersPerTeam = maxPlayersPerTeam,
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

    private async Task<TournamentTeamMember> CreateTestTeamMember(
        Guid teamId,
        Guid userId,
        string status = "Pending",
        string? position = null,
        DateTime? leftAt = null)
    {
        var member = new TournamentTeamMember
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            UserId = userId,
            Role = "Player",
            Status = status,
            Position = position,
            JoinedAt = DateTime.UtcNow,
            LeftAt = leftAt
        };

        _context.TournamentTeamMembers.Add(member);
        await _context.SaveChangesAsync();
        return member;
    }

    #endregion

    #region AddPlayerAsync Tests

    [Fact]
    public async Task AddPlayerAsync_CreatesTeamMemberWithPendingStatus()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var player = await CreateTestUser("player@example.com");
        var tournament = await CreateTestTournament(admin.Id, "Open");
        var team = await CreateTestTeam(tournament.Id, "Team 1");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, admin.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.AddPlayerAsync(tournament.Id, team.Id, player.Id, admin.Id);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(player.Id);
        result.TeamId.Should().Be(team.Id);
        result.Status.Should().Be("Pending");
        result.Role.Should().Be("Player");
        result.JoinedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.RespondedAt.Should().BeNull();
        result.LeftAt.Should().BeNull();
        result.UserFirstName.Should().Be("Test");
        result.UserLastName.Should().Be("User");

        // Verify in database
        var member = await _context.TournamentTeamMembers
            .FirstOrDefaultAsync(tm => tm.TeamId == team.Id && tm.UserId == player.Id);
        member.Should().NotBeNull();
        member!.Status.Should().Be("Pending");
        member.JoinedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AddPlayerAsync_FailsWhenTeamIsFull()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var tournament = await CreateTestTournament(admin.Id, "Open", maxPlayersPerTeam: 3);
        var team = await CreateTestTeam(tournament.Id, "Team 1");

        // Add 3 players with Accepted status (team is full)
        for (int i = 1; i <= 3; i++)
        {
            var existingPlayer = await CreateTestUser($"existing{i}@example.com");
            await CreateTestTeamMember(team.Id, existingPlayer.Id, "Accepted");
        }

        // Add 1 player with Pending status (should NOT count toward capacity)
        var pendingPlayer = await CreateTestUser("pending@example.com");
        await CreateTestTeamMember(team.Id, pendingPlayer.Id, "Pending");

        var newPlayer = await CreateTestUser("new@example.com");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, admin.Id))
            .ReturnsAsync(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.AddPlayerAsync(tournament.Id, team.Id, newPlayer.Id, admin.Id));

        exception.Message.Should().Contain("Team is full");
        exception.Message.Should().Contain("MaxPlayersPerTeam");
    }

    [Fact]
    public async Task AddPlayerAsync_FailsWhenPlayerAlreadyOnAnotherTeam_Pending()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var player = await CreateTestUser("player@example.com");
        var tournament = await CreateTestTournament(admin.Id, "Open");
        var team1 = await CreateTestTeam(tournament.Id, "Team 1");
        var team2 = await CreateTestTeam(tournament.Id, "Team 2");

        // Player already has Pending status on team1
        await CreateTestTeamMember(team1.Id, player.Id, "Pending");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, admin.Id))
            .ReturnsAsync(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.AddPlayerAsync(tournament.Id, team2.Id, player.Id, admin.Id));

        exception.Message.Should().Contain("already on another team");
        exception.Message.Should().Contain("Pending");
    }

    [Fact]
    public async Task AddPlayerAsync_FailsWhenPlayerAlreadyOnAnotherTeam_Accepted()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var player = await CreateTestUser("player@example.com");
        var tournament = await CreateTestTournament(admin.Id, "Open");
        var team1 = await CreateTestTeam(tournament.Id, "Team 1");
        var team2 = await CreateTestTeam(tournament.Id, "Team 2");

        // Player already has Accepted status on team1
        await CreateTestTeamMember(team1.Id, player.Id, "Accepted");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, admin.Id))
            .ReturnsAsync(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.AddPlayerAsync(tournament.Id, team2.Id, player.Id, admin.Id));

        exception.Message.Should().Contain("already on another team");
        exception.Message.Should().Contain("Accepted");
    }

    [Fact]
    public async Task AddPlayerAsync_SucceedsWhenPlayerDeclinedAnotherTeam()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var player = await CreateTestUser("player@example.com");
        var tournament = await CreateTestTournament(admin.Id, "Open");
        var team1 = await CreateTestTeam(tournament.Id, "Team 1");
        var team2 = await CreateTestTeam(tournament.Id, "Team 2");

        // Player has Declined status on team1 - should not block
        await CreateTestTeamMember(team1.Id, player.Id, "Declined");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, admin.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.AddPlayerAsync(tournament.Id, team2.Id, player.Id, admin.Id);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(player.Id);
        result.TeamId.Should().Be(team2.Id);
        result.Status.Should().Be("Pending");

        // Verify in database
        var member = await _context.TournamentTeamMembers
            .FirstOrDefaultAsync(tm => tm.TeamId == team2.Id && tm.UserId == player.Id);
        member.Should().NotBeNull();
        member!.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task AddPlayerAsync_FailsWhenNotAdmin()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var nonAdmin = await CreateTestUser("nonadmin@example.com");
        var player = await CreateTestUser("player@example.com");
        var tournament = await CreateTestTournament(admin.Id, "Open");
        var team = await CreateTestTeam(tournament.Id, "Team 1");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, nonAdmin.Id))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.AddPlayerAsync(tournament.Id, team.Id, player.Id, nonAdmin.Id));

        // Verify no team member created
        var member = await _context.TournamentTeamMembers
            .FirstOrDefaultAsync(tm => tm.TeamId == team.Id && tm.UserId == player.Id);
        member.Should().BeNull();
    }

    #endregion

    #region RemovePlayerAsync Tests

    [Fact]
    public async Task RemovePlayerAsync_SetsLeftAt()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var player = await CreateTestUser("player@example.com");
        var tournament = await CreateTestTournament(admin.Id, "Open");
        var team = await CreateTestTeam(tournament.Id, "Team 1");
        var member = await CreateTestTeamMember(team.Id, player.Id, "Accepted");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, admin.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.RemovePlayerAsync(tournament.Id, team.Id, player.Id, admin.Id);

        // Assert
        result.Should().BeTrue();

        // Verify LeftAt is set (soft delete)
        var updated = await _context.TournamentTeamMembers
            .FirstOrDefaultAsync(tm => tm.Id == member.Id);
        updated.Should().NotBeNull();
        updated!.LeftAt.Should().NotBeNull();
        updated.LeftAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task RemovePlayerAsync_FailsWhenNotAdmin()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var nonAdmin = await CreateTestUser("nonadmin@example.com");
        var player = await CreateTestUser("player@example.com");
        var tournament = await CreateTestTournament(admin.Id, "Open");
        var team = await CreateTestTeam(tournament.Id, "Team 1");
        var member = await CreateTestTeamMember(team.Id, player.Id, "Accepted");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, nonAdmin.Id))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.RemovePlayerAsync(tournament.Id, team.Id, player.Id, nonAdmin.Id));

        // Verify LeftAt still null
        var unchanged = await _context.TournamentTeamMembers
            .FirstOrDefaultAsync(tm => tm.Id == member.Id);
        unchanged.Should().NotBeNull();
        unchanged!.LeftAt.Should().BeNull();
    }

    #endregion

    #region RespondAsync Tests

    [Fact]
    public async Task RespondAsync_AcceptSetsStatusAndCreatesRegistration()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var player = await CreateTestUser("player@example.com");
        var tournament = await CreateTestTournament(admin.Id, "Open");
        var team = await CreateTestTeam(tournament.Id, "Team 1");
        var member = await CreateTestTeamMember(team.Id, player.Id, "Pending");

        // Act
        var result = await _sut.RespondAsync(
            tournament.Id,
            team.Id,
            player.Id,
            accept: true,
            position: "Skater",
            customResponses: "{\"jerseySize\":\"L\"}");

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("Accepted");
        result.Position.Should().Be("Skater");
        result.RespondedAt.Should().NotBeNull();
        result.RespondedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify in database
        var updated = await _context.TournamentTeamMembers
            .FirstOrDefaultAsync(tm => tm.Id == member.Id);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be("Accepted");
        updated.Position.Should().Be("Skater");
        updated.RespondedAt.Should().NotBeNull();

        // Verify TournamentRegistration created
        var registration = await _context.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.TournamentId == tournament.Id && r.UserId == player.Id);
        registration.Should().NotBeNull();
        registration!.Status.Should().Be("Assigned");
        registration.AssignedTeamId.Should().Be(team.Id);
        registration.Position.Should().Be("Skater");
        registration.CustomResponses.Should().Be("{\"jerseySize\":\"L\"}");
    }

    [Fact]
    public async Task RespondAsync_DeclineSetsStatusOnly()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var player = await CreateTestUser("player@example.com");
        var tournament = await CreateTestTournament(admin.Id, "Open");
        var team = await CreateTestTeam(tournament.Id, "Team 1");
        var member = await CreateTestTeamMember(team.Id, player.Id, "Pending");

        // Act
        var result = await _sut.RespondAsync(
            tournament.Id,
            team.Id,
            player.Id,
            accept: false,
            position: null,
            customResponses: null);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("Declined");
        result.Position.Should().BeNull();
        result.RespondedAt.Should().NotBeNull();
        result.RespondedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify in database
        var updated = await _context.TournamentTeamMembers
            .FirstOrDefaultAsync(tm => tm.Id == member.Id);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be("Declined");
        updated.RespondedAt.Should().NotBeNull();

        // Verify NO TournamentRegistration created
        var registration = await _context.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.TournamentId == tournament.Id && r.UserId == player.Id);
        registration.Should().BeNull();
    }

    #endregion

    #region GetTeamMembersAsync Tests

    [Fact]
    public async Task GetTeamMembersAsync_ReturnsAllMembersWithStatus()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var player1 = await CreateTestUser("player1@example.com");
        var player2 = await CreateTestUser("player2@example.com");
        var player3 = await CreateTestUser("player3@example.com");
        var tournament = await CreateTestTournament(admin.Id, "Open");
        var team = await CreateTestTeam(tournament.Id, "Team 1");

        // Create members with different statuses
        await CreateTestTeamMember(team.Id, player1.Id, "Accepted", "Goalie");
        await CreateTestTeamMember(team.Id, player2.Id, "Pending");
        await CreateTestTeamMember(team.Id, player3.Id, "Declined");

        // Act
        var result = await _sut.GetTeamMembersAsync(tournament.Id, team.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(3);

        var acceptedMember = result.FirstOrDefault(m => m.UserId == player1.Id);
        acceptedMember.Should().NotBeNull();
        acceptedMember!.Status.Should().Be("Accepted");
        acceptedMember.Position.Should().Be("Goalie");

        var pendingMember = result.FirstOrDefault(m => m.UserId == player2.Id);
        pendingMember.Should().NotBeNull();
        pendingMember!.Status.Should().Be("Pending");
        pendingMember.Position.Should().BeNull();

        var declinedMember = result.FirstOrDefault(m => m.UserId == player3.Id);
        declinedMember.Should().NotBeNull();
        declinedMember!.Status.Should().Be("Declined");
    }

    [Fact]
    public async Task GetTeamMembersAsync_ExcludesRemovedMembers()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var player1 = await CreateTestUser("player1@example.com");
        var player2 = await CreateTestUser("player2@example.com");
        var tournament = await CreateTestTournament(admin.Id, "Open");
        var team = await CreateTestTeam(tournament.Id, "Team 1");

        // Create one active member and one removed member (LeftAt set)
        await CreateTestTeamMember(team.Id, player1.Id, "Accepted");
        await CreateTestTeamMember(team.Id, player2.Id, "Accepted", leftAt: DateTime.UtcNow.AddDays(-1));

        // Act
        var result = await _sut.GetTeamMembersAsync(tournament.Id, team.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
        result[0].UserId.Should().Be(player1.Id);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task AddPlayerAsync_FailsWhenTeamNotFound()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var player = await CreateTestUser("player@example.com");
        var tournament = await CreateTestTournament(admin.Id, "Open");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, admin.Id))
            .ReturnsAsync(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.AddPlayerAsync(tournament.Id, Guid.NewGuid(), player.Id, admin.Id));

        exception.Message.Should().Contain("Team not found");
    }

    [Fact]
    public async Task AddPlayerAsync_FailsWhenUserNotFound()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var tournament = await CreateTestTournament(admin.Id, "Open");
        var team = await CreateTestTeam(tournament.Id, "Team 1");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, admin.Id))
            .ReturnsAsync(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.AddPlayerAsync(tournament.Id, team.Id, Guid.NewGuid(), admin.Id));

        exception.Message.Should().Contain("User not found");
    }

    [Fact]
    public async Task RemovePlayerAsync_ReturnsFalseWhenMemberNotFound()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var player = await CreateTestUser("player@example.com");
        var tournament = await CreateTestTournament(admin.Id, "Open");
        var team = await CreateTestTeam(tournament.Id, "Team 1");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, admin.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.RemovePlayerAsync(tournament.Id, team.Id, player.Id, admin.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RespondAsync_FailsWhenMemberNotFound()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var player = await CreateTestUser("player@example.com");
        var tournament = await CreateTestTournament(admin.Id, "Open");
        var team = await CreateTestTeam(tournament.Id, "Team 1");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RespondAsync(tournament.Id, team.Id, player.Id, true, "Skater", null));

        exception.Message.Should().Contain("Team member invitation not found");
    }

    [Fact]
    public async Task RespondAsync_FailsWhenAlreadyResponded()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var player = await CreateTestUser("player@example.com");
        var tournament = await CreateTestTournament(admin.Id, "Open");
        var team = await CreateTestTeam(tournament.Id, "Team 1");
        var member = await CreateTestTeamMember(team.Id, player.Id, "Accepted");

        // Set RespondedAt to simulate already responded
        member.RespondedAt = DateTime.UtcNow.AddHours(-1);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RespondAsync(tournament.Id, team.Id, player.Id, true, "Skater", null));

        exception.Message.Should().Contain("already responded");
    }

    [Fact]
    public async Task GetTeamMembersAsync_ReturnsEmptyListWhenNoMembers()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var tournament = await CreateTestTournament(admin.Id, "Open");
        var team = await CreateTestTeam(tournament.Id, "Team 1");

        // Act
        var result = await _sut.GetTeamMembersAsync(tournament.Id, team.Id);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    #endregion
}
