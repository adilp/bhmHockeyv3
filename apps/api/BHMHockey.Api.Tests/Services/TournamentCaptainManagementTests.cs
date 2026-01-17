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
/// Tests for Tournament Captain Management - TDD approach for TRN-012.
/// Tests written FIRST before implementation.
///
/// These tests ensure:
/// - Captains can add/remove players from their team
/// - Captains can update team name and delete team
/// - Captains can transfer captaincy to another accepted member
/// - Non-captains/non-admins are unauthorized for captain operations
/// - Captain cannot remove themselves
/// - Removal after deadline respects AllowSubstitutions flag
/// - Transfer updates both member roles and Team.CaptainUserId
/// - Audit logs are created for captain transfers
/// </summary>
public class TournamentCaptainManagementTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<ITournamentService> _mockTournamentService;
    private readonly Mock<ITournamentTeamService> _mockTournamentTeamService;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly ITournamentTeamMemberService _sut;

    public TournamentCaptainManagementTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new AppDbContext(options);
        _mockTournamentService = new Mock<ITournamentService>();
        _mockTournamentTeamService = new Mock<ITournamentTeamService>();
        _mockNotificationService = new Mock<INotificationService>();

        _sut = new TournamentTeamMemberService(
            _context,
            _mockTournamentService.Object,
            _mockTournamentTeamService.Object,
            _mockNotificationService.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Helper Methods

    private async Task<User> CreateTestUser(
        string email = "test@example.com",
        string firstName = "Test",
        string lastName = "User")
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = "hashed_password",
            FirstName = firstName,
            LastName = lastName,
            Positions = new Dictionary<string, string> { { "skater", "Silver" } },
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
        string teamFormation = "PreFormed",
        int maxPlayersPerTeam = 10,
        bool allowSubstitutions = true,
        DateTime? registrationDeadline = null)
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
            RegistrationDeadline = registrationDeadline ?? DateTime.UtcNow.AddDays(25),
            MaxTeams = 4,
            MinPlayersPerTeam = 5,
            MaxPlayersPerTeam = maxPlayersPerTeam,
            AllowMultiTeam = false,
            AllowSubstitutions = allowSubstitutions,
            EntryFee = 50,
            FeeType = "PerTeam",
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
        string name = "Team Alpha",
        Guid? captainUserId = null)
    {
        var team = new TournamentTeam
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            Name = name,
            Status = "Registered",
            CaptainUserId = captainUserId,
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
        string role = "Player",
        string status = "Accepted",
        string? position = null)
    {
        var member = new TournamentTeamMember
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            UserId = userId,
            Role = role,
            Status = status,
            Position = position,
            JoinedAt = DateTime.UtcNow,
            RespondedAt = status == "Accepted" ? DateTime.UtcNow : null
        };

        _context.TournamentTeamMembers.Add(member);
        await _context.SaveChangesAsync();
        return member;
    }

    /// <summary>
    /// Creates a team with a captain and a player member
    /// </summary>
    private async Task<(Tournament tournament, TournamentTeam team, User captain, User player)> SetupTeamWithCaptainAsync()
    {
        var admin = await CreateTestUser("admin@example.com", "Admin", "User");
        var captain = await CreateTestUser("captain@example.com", "Captain", "User");
        var player = await CreateTestUser("player@example.com", "Player", "User");

        var tournament = await CreateTestTournament(admin.Id, "Open", "PreFormed");
        var team = await CreateTestTeam(tournament.Id, "Team Alpha", captain.Id);

        // Add captain as Captain role
        await CreateTestTeamMember(team.Id, captain.Id, "Captain", "Accepted");

        // Add player as Player role
        await CreateTestTeamMember(team.Id, player.Id, "Player", "Accepted");

        return (tournament, team, captain, player);
    }

    #endregion

    #region Captain Permission Tests - Add Player

    [Fact]
    public async Task AddPlayer_AsCaptain_ShouldSucceed()
    {
        // Arrange
        var (tournament, team, captain, existingPlayer) = await SetupTeamWithCaptainAsync();
        var newPlayer = await CreateTestUser("newplayer@example.com", "New", "Player");

        // Mock: Captain can manage the team (checked by CanUserManageTeamAsync)
        _mockTournamentTeamService.Setup(ts => ts.CanUserManageTeamAsync(tournament.Id, team.Id, captain.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.AddPlayerAsync(tournament.Id, team.Id, newPlayer.Id, captain.Id);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(newPlayer.Id);
        result.TeamId.Should().Be(team.Id);
        result.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task AddPlayer_AsNonCaptain_ShouldThrowUnauthorized()
    {
        // Arrange
        var (tournament, team, captain, existingPlayer) = await SetupTeamWithCaptainAsync();
        var nonCaptain = await CreateTestUser("noncaptain@example.com", "Non", "Captain");
        var newPlayer = await CreateTestUser("newplayer@example.com", "New", "Player");

        // Mock: Non-captain cannot manage the team
        _mockTournamentTeamService.Setup(ts => ts.CanUserManageTeamAsync(tournament.Id, team.Id, nonCaptain.Id))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.AddPlayerAsync(tournament.Id, team.Id, newPlayer.Id, nonCaptain.Id));
    }

    #endregion

    #region Captain Permission Tests - Remove Player

    [Fact]
    public async Task RemovePlayer_AsCaptain_ShouldSucceed()
    {
        // Arrange
        var (tournament, team, captain, player) = await SetupTeamWithCaptainAsync();

        // Mock: Captain can manage the team
        _mockTournamentTeamService.Setup(ts => ts.CanUserManageTeamAsync(tournament.Id, team.Id, captain.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.RemovePlayerAsync(tournament.Id, team.Id, player.Id, captain.Id);

        // Assert
        result.Should().BeTrue();

        // Verify LeftAt is set
        var member = await _context.TournamentTeamMembers
            .FirstOrDefaultAsync(m => m.TeamId == team.Id && m.UserId == player.Id);
        member.Should().NotBeNull();
        member!.LeftAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RemovePlayer_CaptainCannotRemoveSelf_ShouldThrowInvalidOperation()
    {
        // Arrange
        var (tournament, team, captain, player) = await SetupTeamWithCaptainAsync();

        // Mock: Captain can manage the team
        _mockTournamentTeamService.Setup(ts => ts.CanUserManageTeamAsync(tournament.Id, team.Id, captain.Id))
            .ReturnsAsync(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RemovePlayerAsync(tournament.Id, team.Id, captain.Id, captain.Id));

        exception.Message.Should().Contain("captain");
        exception.Message.Should().Contain("cannot remove");
    }

    [Fact]
    public async Task RemovePlayer_AfterDeadline_WithoutSubstitutions_ShouldThrowInvalidOperation()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com", "Admin", "User");
        var captain = await CreateTestUser("captain@example.com", "Captain", "User");
        var player = await CreateTestUser("player@example.com", "Player", "User");

        // Create tournament with registration deadline in the PAST and substitutions DISABLED
        var tournament = await CreateTestTournament(
            admin.Id,
            "Open",
            "PreFormed",
            maxPlayersPerTeam: 10,
            allowSubstitutions: false,
            registrationDeadline: DateTime.UtcNow.AddDays(-1));

        var team = await CreateTestTeam(tournament.Id, "Team Alpha", captain.Id);
        await CreateTestTeamMember(team.Id, captain.Id, "Captain", "Accepted");
        await CreateTestTeamMember(team.Id, player.Id, "Player", "Accepted");

        _mockTournamentTeamService.Setup(ts => ts.CanUserManageTeamAsync(tournament.Id, team.Id, captain.Id))
            .ReturnsAsync(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RemovePlayerAsync(tournament.Id, team.Id, player.Id, captain.Id));

        exception.Message.Should().Contain("deadline");
        exception.Message.Should().Contain("substitutions");
    }

    [Fact]
    public async Task RemovePlayer_AfterDeadline_WithSubstitutions_ShouldSucceed()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com", "Admin", "User");
        var captain = await CreateTestUser("captain@example.com", "Captain", "User");
        var player = await CreateTestUser("player@example.com", "Player", "User");

        // Create tournament with registration deadline in the PAST but substitutions ENABLED
        var tournament = await CreateTestTournament(
            admin.Id,
            "Open",
            "PreFormed",
            maxPlayersPerTeam: 10,
            allowSubstitutions: true,
            registrationDeadline: DateTime.UtcNow.AddDays(-1));

        var team = await CreateTestTeam(tournament.Id, "Team Alpha", captain.Id);
        await CreateTestTeamMember(team.Id, captain.Id, "Captain", "Accepted");
        await CreateTestTeamMember(team.Id, player.Id, "Player", "Accepted");

        _mockTournamentTeamService.Setup(ts => ts.CanUserManageTeamAsync(tournament.Id, team.Id, captain.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.RemovePlayerAsync(tournament.Id, team.Id, player.Id, captain.Id);

        // Assert
        result.Should().BeTrue();

        // Verify LeftAt is set
        var member = await _context.TournamentTeamMembers
            .FirstOrDefaultAsync(m => m.TeamId == team.Id && m.UserId == player.Id);
        member!.LeftAt.Should().NotBeNull();
    }

    #endregion

    #region Transfer Captain Tests

    [Fact]
    public async Task TransferCaptain_ToAcceptedMember_ShouldSucceed()
    {
        // Arrange
        var (tournament, team, captain, player) = await SetupTeamWithCaptainAsync();

        // Mock: Captain can manage the team
        _mockTournamentTeamService.Setup(ts => ts.CanUserManageTeamAsync(tournament.Id, team.Id, captain.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.TransferCaptainAsync(tournament.Id, team.Id, player.Id, captain.Id);

        // Assert
        result.Should().NotBeNull();
        result.NewCaptainUserId.Should().Be(player.Id);
        result.OldCaptainUserId.Should().Be(captain.Id);
    }

    [Fact]
    public async Task TransferCaptain_ToPendingMember_ShouldThrowInvalidOperation()
    {
        // Arrange
        var (tournament, team, captain, acceptedPlayer) = await SetupTeamWithCaptainAsync();
        var pendingPlayer = await CreateTestUser("pending@example.com", "Pending", "Player");
        await CreateTestTeamMember(team.Id, pendingPlayer.Id, "Player", "Pending");

        _mockTournamentTeamService.Setup(ts => ts.CanUserManageTeamAsync(tournament.Id, team.Id, captain.Id))
            .ReturnsAsync(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.TransferCaptainAsync(tournament.Id, team.Id, pendingPlayer.Id, captain.Id));

        exception.Message.Should().Contain("accepted");
    }

    [Fact]
    public async Task TransferCaptain_ToNonMember_ShouldThrowInvalidOperation()
    {
        // Arrange
        var (tournament, team, captain, player) = await SetupTeamWithCaptainAsync();
        var nonMember = await CreateTestUser("nonmember@example.com", "Non", "Member");

        _mockTournamentTeamService.Setup(ts => ts.CanUserManageTeamAsync(tournament.Id, team.Id, captain.Id))
            .ReturnsAsync(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.TransferCaptainAsync(tournament.Id, team.Id, nonMember.Id, captain.Id));

        exception.Message.Should().Contain("team member");
    }

    [Fact]
    public async Task TransferCaptain_AsNonCaptain_ShouldThrowUnauthorized()
    {
        // Arrange
        var (tournament, team, captain, player) = await SetupTeamWithCaptainAsync();
        var nonCaptain = await CreateTestUser("noncaptain@example.com", "Non", "Captain");

        _mockTournamentTeamService.Setup(ts => ts.CanUserManageTeamAsync(tournament.Id, team.Id, nonCaptain.Id))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.TransferCaptainAsync(tournament.Id, team.Id, player.Id, nonCaptain.Id));
    }

    [Fact]
    public async Task TransferCaptain_UpdatesRoles_ShouldUpdateBothMemberRoles()
    {
        // Arrange
        var (tournament, team, captain, player) = await SetupTeamWithCaptainAsync();

        _mockTournamentTeamService.Setup(ts => ts.CanUserManageTeamAsync(tournament.Id, team.Id, captain.Id))
            .ReturnsAsync(true);

        // Act
        await _sut.TransferCaptainAsync(tournament.Id, team.Id, player.Id, captain.Id);

        // Assert - verify database changes
        var oldCaptainMember = await _context.TournamentTeamMembers
            .FirstOrDefaultAsync(m => m.TeamId == team.Id && m.UserId == captain.Id);
        oldCaptainMember.Should().NotBeNull();
        oldCaptainMember!.Role.Should().Be("Player");

        var newCaptainMember = await _context.TournamentTeamMembers
            .FirstOrDefaultAsync(m => m.TeamId == team.Id && m.UserId == player.Id);
        newCaptainMember.Should().NotBeNull();
        newCaptainMember!.Role.Should().Be("Captain");
    }

    [Fact]
    public async Task TransferCaptain_UpdatesTeamCaptainUserId_ShouldUpdateTeam()
    {
        // Arrange
        var (tournament, team, captain, player) = await SetupTeamWithCaptainAsync();

        _mockTournamentTeamService.Setup(ts => ts.CanUserManageTeamAsync(tournament.Id, team.Id, captain.Id))
            .ReturnsAsync(true);

        // Act
        await _sut.TransferCaptainAsync(tournament.Id, team.Id, player.Id, captain.Id);

        // Assert - verify team CaptainUserId is updated
        var updatedTeam = await _context.TournamentTeams
            .FirstOrDefaultAsync(t => t.Id == team.Id);
        updatedTeam.Should().NotBeNull();
        updatedTeam!.CaptainUserId.Should().Be(player.Id);
    }

    #endregion

    #region Team Update/Delete by Captain Tests

    [Fact]
    public async Task UpdateTeamName_AsCaptain_ShouldSucceed()
    {
        // Arrange
        var (tournament, team, captain, player) = await SetupTeamWithCaptainAsync();

        _mockTournamentTeamService.Setup(ts => ts.CanUserManageTeamAsync(tournament.Id, team.Id, captain.Id))
            .ReturnsAsync(true);

        var updateRequest = new UpdateTournamentTeamRequest { Name = "New Team Name" };

        // Note: This will require a new method or extending existing UpdateAsync to accept captain permission
        var teamService = new TournamentTeamService(_context, _mockTournamentService.Object);

        // Act
        var result = await teamService.UpdateAsync(tournament.Id, team.Id, updateRequest, captain.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("New Team Name");
    }

    [Fact]
    public async Task DeleteTeam_AsCaptain_ShouldSucceed()
    {
        // Arrange
        var (tournament, team, captain, player) = await SetupTeamWithCaptainAsync();

        _mockTournamentTeamService.Setup(ts => ts.CanUserManageTeamAsync(tournament.Id, team.Id, captain.Id))
            .ReturnsAsync(true);

        var teamService = new TournamentTeamService(_context, _mockTournamentService.Object);

        // Act
        var result = await teamService.DeleteAsync(tournament.Id, team.Id, captain.Id);

        // Assert
        result.Should().BeTrue();

        // Verify team is deleted
        var deletedTeam = await _context.TournamentTeams
            .FirstOrDefaultAsync(t => t.Id == team.Id);
        deletedTeam.Should().BeNull();
    }

    #endregion

    #region Audit Log Tests

    [Fact]
    public async Task TransferCaptain_ShouldCreateAuditLog()
    {
        // Arrange
        var (tournament, team, captain, player) = await SetupTeamWithCaptainAsync();

        _mockTournamentTeamService.Setup(ts => ts.CanUserManageTeamAsync(tournament.Id, team.Id, captain.Id))
            .ReturnsAsync(true);

        // Act
        await _sut.TransferCaptainAsync(tournament.Id, team.Id, player.Id, captain.Id);

        // Assert - verify audit log created
        var auditLog = await _context.TournamentAuditLogs
            .FirstOrDefaultAsync(al =>
                al.TournamentId == tournament.Id &&
                al.Action == "TransferCaptain" &&
                al.EntityType == "Team" &&
                al.EntityId == team.Id);

        auditLog.Should().NotBeNull();
        auditLog!.UserId.Should().Be(captain.Id);
        auditLog.OldValue.Should().Contain(captain.Id.ToString());
        auditLog.NewValue.Should().Contain(player.Id.ToString());
        auditLog.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task AddPlayer_AsCaptain_WithFullTeam_ShouldFail()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com", "Admin", "User");
        var captain = await CreateTestUser("captain@example.com", "Captain", "User");
        var tournament = await CreateTestTournament(admin.Id, "Open", "PreFormed", maxPlayersPerTeam: 2);
        var team = await CreateTestTeam(tournament.Id, "Team Alpha", captain.Id);

        await CreateTestTeamMember(team.Id, captain.Id, "Captain", "Accepted");
        var player1 = await CreateTestUser("player1@example.com", "Player", "One");
        await CreateTestTeamMember(team.Id, player1.Id, "Player", "Accepted");

        var newPlayer = await CreateTestUser("newplayer@example.com", "New", "Player");

        _mockTournamentTeamService.Setup(ts => ts.CanUserManageTeamAsync(tournament.Id, team.Id, captain.Id))
            .ReturnsAsync(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.AddPlayerAsync(tournament.Id, team.Id, newPlayer.Id, captain.Id));

        exception.Message.Should().Contain("Team is full");
    }

    [Fact]
    public async Task TransferCaptain_ToSelf_ShouldThrowInvalidOperation()
    {
        // Arrange
        var (tournament, team, captain, player) = await SetupTeamWithCaptainAsync();

        _mockTournamentTeamService.Setup(ts => ts.CanUserManageTeamAsync(tournament.Id, team.Id, captain.Id))
            .ReturnsAsync(true);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.TransferCaptainAsync(tournament.Id, team.Id, captain.Id, captain.Id));

        exception.Message.Should().Contain("yourself");
    }

    [Fact]
    public async Task RemovePlayer_AsNonCaptain_ShouldThrowUnauthorized()
    {
        // Arrange
        var (tournament, team, captain, player) = await SetupTeamWithCaptainAsync();
        var nonCaptain = await CreateTestUser("noncaptain@example.com", "Non", "Captain");

        _mockTournamentTeamService.Setup(ts => ts.CanUserManageTeamAsync(tournament.Id, team.Id, nonCaptain.Id))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.RemovePlayerAsync(tournament.Id, team.Id, player.Id, nonCaptain.Id));
    }

    #endregion
}
