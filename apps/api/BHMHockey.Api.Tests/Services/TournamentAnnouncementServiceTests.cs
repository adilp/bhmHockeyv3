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
/// Tests for TournamentAnnouncementService - TDD approach.
/// Tests written FIRST before implementation.
/// Tournament announcements with visibility filtering (All, Captains, Admins, Team-specific).
/// </summary>
public class TournamentAnnouncementServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly TournamentAnnouncementService _sut;
    private readonly ITournamentAuthorizationService _authService;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<ILogger<TournamentAnnouncementService>> _mockLogger;

    public TournamentAnnouncementServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _authService = new TournamentAuthorizationService(_context);
        _mockNotificationService = new Mock<INotificationService>();
        _mockLogger = new Mock<ILogger<TournamentAnnouncementService>>();
        _sut = new TournamentAnnouncementService(_context, _authService, _mockNotificationService.Object, _mockLogger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Helper Methods

    private async Task<User> CreateTestUser(string email = "test@example.com", string firstName = "John", string lastName = "Doe")
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = "hashed_password",
            FirstName = firstName,
            LastName = lastName,
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

    private async Task<TournamentAdmin> AddTournamentAdmin(Guid tournamentId, Guid userId, string role, Guid? addedByUserId = null)
    {
        var admin = new TournamentAdmin
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            UserId = userId,
            Role = role,
            AddedByUserId = addedByUserId,
            AddedAt = DateTime.UtcNow
        };

        _context.TournamentAdmins.Add(admin);
        await _context.SaveChangesAsync();
        return admin;
    }

    private async Task<TournamentTeam> CreateTestTeam(Guid tournamentId, string name, Guid? captainId = null)
    {
        var team = new TournamentTeam
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            Name = name,
            CaptainUserId = captainId,
            Status = "Registered",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TournamentTeams.Add(team);
        await _context.SaveChangesAsync();
        return team;
    }

    private async Task AddTeamMember(Guid teamId, Guid userId, string role = "Player")
    {
        var member = new TournamentTeamMember
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            UserId = userId,
            Role = role,
            JoinedAt = DateTime.UtcNow
        };

        _context.TournamentTeamMembers.Add(member);
        await _context.SaveChangesAsync();
    }

    private async Task<TournamentAnnouncement> CreateTestAnnouncement(
        Guid tournamentId,
        Guid createdByUserId,
        string title,
        string body,
        string target = "All",
        List<Guid>? targetTeamIds = null)
    {
        var announcement = new TournamentAnnouncement
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            CreatedByUserId = createdByUserId,
            Title = title,
            Body = body,
            Target = target,
            TargetTeamIds = targetTeamIds != null ? System.Text.Json.JsonSerializer.Serialize(targetTeamIds) : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TournamentAnnouncements.Add(announcement);
        await _context.SaveChangesAsync();
        return announcement;
    }

    #endregion

    #region GetAnnouncementsAsync Tests

    [Fact]
    public async Task GetAnnouncementsAsync_ReturnsAllNonDeletedAnnouncementsOrderedByCreatedAtDesc()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var tournament = await CreateTestTournament(admin.Id);

        var announcement1 = await CreateTestAnnouncement(tournament.Id, admin.Id, "First", "Body 1");
        await Task.Delay(10); // Ensure different timestamps
        var announcement2 = await CreateTestAnnouncement(tournament.Id, admin.Id, "Second", "Body 2");
        await Task.Delay(10);
        var announcement3 = await CreateTestAnnouncement(tournament.Id, admin.Id, "Third", "Body 3");

        // Act
        var result = await _sut.GetAnnouncementsAsync(tournament.Id, admin.Id);

        // Assert
        result.Should().HaveCount(3);
        result[0].Title.Should().Be("Third"); // Most recent first
        result[1].Title.Should().Be("Second");
        result[2].Title.Should().Be("First");
    }

    [Fact]
    public async Task GetAnnouncementsAsync_ExcludesSoftDeletedAnnouncements()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var tournament = await CreateTestTournament(admin.Id);

        var activeAnnouncement = await CreateTestAnnouncement(tournament.Id, admin.Id, "Active", "Body 1");
        var deletedAnnouncement = await CreateTestAnnouncement(tournament.Id, admin.Id, "Deleted", "Body 2");

        // Soft delete one announcement
        deletedAnnouncement.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetAnnouncementsAsync(tournament.Id, admin.Id);

        // Assert
        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Active");
    }

    [Fact]
    public async Task GetAnnouncementsAsync_AdminSeesAllAnnouncements()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var tournament = await CreateTestTournament(admin.Id);
        var team = await CreateTestTeam(tournament.Id, "Team A");

        await CreateTestAnnouncement(tournament.Id, admin.Id, "All", "Body", "All");
        await CreateTestAnnouncement(tournament.Id, admin.Id, "Captains", "Body", "Captains");
        await CreateTestAnnouncement(tournament.Id, admin.Id, "Admins", "Body", "Admins");
        await CreateTestAnnouncement(tournament.Id, admin.Id, "Team", "Body", "Teams", new List<Guid> { team.Id });

        // Act
        var result = await _sut.GetAnnouncementsAsync(tournament.Id, admin.Id);

        // Assert
        result.Should().HaveCount(4);
        result.Should().Contain(a => a.Target == "All");
        result.Should().Contain(a => a.Target == "Captains");
        result.Should().Contain(a => a.Target == "Admins");
        result.Should().Contain(a => a.Target == "Teams");
    }

    [Fact]
    public async Task GetAnnouncementsAsync_CaptainSeesAllCaptainsAndTeamSpecificAnnouncements()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var captain = await CreateTestUser("captain@example.com");
        var tournament = await CreateTestTournament(admin.Id);

        var captainTeam = await CreateTestTeam(tournament.Id, "Team A", captain.Id);
        var otherTeam = await CreateTestTeam(tournament.Id, "Team B");
        await AddTeamMember(captainTeam.Id, captain.Id, "Captain");

        await CreateTestAnnouncement(tournament.Id, admin.Id, "All", "Body", "All");
        await CreateTestAnnouncement(tournament.Id, admin.Id, "Captains", "Body", "Captains");
        await CreateTestAnnouncement(tournament.Id, admin.Id, "Admins", "Body", "Admins");
        await CreateTestAnnouncement(tournament.Id, admin.Id, "Team A", "Body", "Teams", new List<Guid> { captainTeam.Id });
        await CreateTestAnnouncement(tournament.Id, admin.Id, "Team B", "Body", "Teams", new List<Guid> { otherTeam.Id });

        // Act
        var result = await _sut.GetAnnouncementsAsync(tournament.Id, captain.Id);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(a => a.Target == "All");
        result.Should().Contain(a => a.Target == "Captains");
        result.Should().Contain(a => a.Title == "Team A");
        result.Should().NotContain(a => a.Target == "Admins");
        result.Should().NotContain(a => a.Title == "Team B");
    }

    [Fact]
    public async Task GetAnnouncementsAsync_RegularPlayerSeesAllAndTeamSpecificAnnouncements()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var player = await CreateTestUser("player@example.com");
        var tournament = await CreateTestTournament(admin.Id);

        var playerTeam = await CreateTestTeam(tournament.Id, "Team A");
        var otherTeam = await CreateTestTeam(tournament.Id, "Team B");
        await AddTeamMember(playerTeam.Id, player.Id, "Player");

        await CreateTestAnnouncement(tournament.Id, admin.Id, "All", "Body", "All");
        await CreateTestAnnouncement(tournament.Id, admin.Id, "Captains", "Body", "Captains");
        await CreateTestAnnouncement(tournament.Id, admin.Id, "Admins", "Body", "Admins");
        await CreateTestAnnouncement(tournament.Id, admin.Id, "Team A", "Body", "Teams", new List<Guid> { playerTeam.Id });
        await CreateTestAnnouncement(tournament.Id, admin.Id, "Team B", "Body", "Teams", new List<Guid> { otherTeam.Id });

        // Act
        var result = await _sut.GetAnnouncementsAsync(tournament.Id, player.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(a => a.Target == "All");
        result.Should().Contain(a => a.Title == "Team A");
        result.Should().NotContain(a => a.Target == "Captains");
        result.Should().NotContain(a => a.Target == "Admins");
        result.Should().NotContain(a => a.Title == "Team B");
    }

    [Fact]
    public async Task GetAnnouncementsAsync_ReturnsCreatorDetails()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com", "Admin", "User");
        var tournament = await CreateTestTournament(admin.Id);

        await CreateTestAnnouncement(tournament.Id, admin.Id, "Test", "Body");

        // Act
        var result = await _sut.GetAnnouncementsAsync(tournament.Id, admin.Id);

        // Assert
        result.Should().HaveCount(1);
        result[0].CreatedByFirstName.Should().Be("Admin");
        result[0].CreatedByLastName.Should().Be("User");
    }

    [Fact]
    public async Task GetAnnouncementsAsync_ReturnsEmptyListForNoAnnouncements()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var tournament = await CreateTestTournament(admin.Id);

        // Act
        var result = await _sut.GetAnnouncementsAsync(tournament.Id, admin.Id);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAnnouncementsAsync_PlayerNotOnAnyTeamSeesOnlyAllAnnouncements()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var player = await CreateTestUser("player@example.com");
        var tournament = await CreateTestTournament(admin.Id);

        var team = await CreateTestTeam(tournament.Id, "Team A");

        await CreateTestAnnouncement(tournament.Id, admin.Id, "All", "Body", "All");
        await CreateTestAnnouncement(tournament.Id, admin.Id, "Captains", "Body", "Captains");
        await CreateTestAnnouncement(tournament.Id, admin.Id, "Team A", "Body", "Teams", new List<Guid> { team.Id });

        // Act
        var result = await _sut.GetAnnouncementsAsync(tournament.Id, player.Id);

        // Assert
        result.Should().HaveCount(1);
        result[0].Target.Should().Be("All");
    }

    #endregion

    #region CreateAnnouncementAsync Tests

    [Fact]
    public async Task CreateAnnouncementAsync_AdminCanCreateAnnouncementWithTargetAll()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com", "Admin", "User");
        var tournament = await CreateTestTournament(admin.Id);

        var request = new CreateTournamentAnnouncementRequest(
            Title: "Important Update",
            Body: "Please read this important announcement.",
            Target: "All",
            TargetTeamIds: null
        );

        // Act
        var result = await _sut.CreateAnnouncementAsync(tournament.Id, request, admin.Id);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Important Update");
        result.Body.Should().Be("Please read this important announcement.");
        result.Target.Should().Be("All");
        result.CreatedByUserId.Should().Be(admin.Id);
        result.CreatedByFirstName.Should().Be("Admin");
        result.CreatedByLastName.Should().Be("User");
        result.TargetTeamIds.Should().BeNull();
    }

    [Fact]
    public async Task CreateAnnouncementAsync_AdminCanCreateAnnouncementWithTargetCaptains()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var tournament = await CreateTestTournament(admin.Id);

        var request = new CreateTournamentAnnouncementRequest(
            Title: "Captains Meeting",
            Body: "Captains meeting tomorrow at 5pm.",
            Target: "Captains",
            TargetTeamIds: null
        );

        // Act
        var result = await _sut.CreateAnnouncementAsync(tournament.Id, request, admin.Id);

        // Assert
        result.Should().NotBeNull();
        result.Target.Should().Be("Captains");
    }

    [Fact]
    public async Task CreateAnnouncementAsync_AdminCanCreateAnnouncementWithTargetAdmins()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var tournament = await CreateTestTournament(admin.Id);

        var request = new CreateTournamentAnnouncementRequest(
            Title: "Admin Note",
            Body: "Internal note for admins only.",
            Target: "Admins",
            TargetTeamIds: null
        );

        // Act
        var result = await _sut.CreateAnnouncementAsync(tournament.Id, request, admin.Id);

        // Assert
        result.Should().NotBeNull();
        result.Target.Should().Be("Admins");
    }

    [Fact]
    public async Task CreateAnnouncementAsync_AdminCanCreateAnnouncementTargetingSpecificTeams()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var tournament = await CreateTestTournament(admin.Id);

        var team1 = await CreateTestTeam(tournament.Id, "Team A");
        var team2 = await CreateTestTeam(tournament.Id, "Team B");

        var request = new CreateTournamentAnnouncementRequest(
            Title: "Team Specific",
            Body: "Message for specific teams.",
            Target: "Teams",
            TargetTeamIds: new List<Guid> { team1.Id, team2.Id }
        );

        // Act
        var result = await _sut.CreateAnnouncementAsync(tournament.Id, request, admin.Id);

        // Assert
        result.Should().NotBeNull();
        result.Target.Should().Be("Teams");
        result.TargetTeamIds.Should().HaveCount(2);
        result.TargetTeamIds.Should().Contain(team1.Id);
        result.TargetTeamIds.Should().Contain(team2.Id);
    }

    [Fact]
    public async Task CreateAnnouncementAsync_NonAdminCannotCreateAnnouncement()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var player = await CreateTestUser("player@example.com");
        var tournament = await CreateTestTournament(admin.Id);

        var request = new CreateTournamentAnnouncementRequest(
            Title: "Unauthorized",
            Body: "Should not be created.",
            Target: "All",
            TargetTeamIds: null
        );

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.CreateAnnouncementAsync(tournament.Id, request, player.Id));
    }

    [Fact]
    public async Task CreateAnnouncementAsync_ScorekeeperCanCreateAnnouncement()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var scorekeeper = await CreateTestUser("scorekeeper@example.com");
        var tournament = await CreateTestTournament(owner.Id);
        await AddTournamentAdmin(tournament.Id, scorekeeper.Id, "Scorekeeper", owner.Id);

        var request = new CreateTournamentAnnouncementRequest(
            Title: "Score Update",
            Body: "Scores have been updated.",
            Target: "All",
            TargetTeamIds: null
        );

        // Act
        var result = await _sut.CreateAnnouncementAsync(tournament.Id, request, scorekeeper.Id);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Score Update");
    }

    [Fact]
    public async Task CreateAnnouncementAsync_SetsCreatedAtAndUpdatedAt()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var tournament = await CreateTestTournament(admin.Id);

        var request = new CreateTournamentAnnouncementRequest(
            Title: "Test",
            Body: "Body",
            Target: "All",
            TargetTeamIds: null
        );

        var beforeCreate = DateTime.UtcNow;

        // Act
        var result = await _sut.CreateAnnouncementAsync(tournament.Id, request, admin.Id);

        // Assert
        result.CreatedAt.Should().BeAfter(beforeCreate.AddSeconds(-1));
        result.UpdatedAt.Should().BeAfter(beforeCreate.AddSeconds(-1));
    }

    #endregion

    #region UpdateAnnouncementAsync Tests

    [Fact]
    public async Task UpdateAnnouncementAsync_AdminCanUpdateTitleAndBody()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var tournament = await CreateTestTournament(admin.Id);
        var announcement = await CreateTestAnnouncement(tournament.Id, admin.Id, "Original Title", "Original Body");

        var request = new UpdateTournamentAnnouncementRequest(
            Title: "Updated Title",
            Body: "Updated Body",
            Target: null,
            TargetTeamIds: null
        );

        // Act
        var result = await _sut.UpdateAnnouncementAsync(tournament.Id, announcement.Id, request, admin.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Title.Should().Be("Updated Title");
        result.Body.Should().Be("Updated Body");
    }

    [Fact]
    public async Task UpdateAnnouncementAsync_AdminCanChangeTarget()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var tournament = await CreateTestTournament(admin.Id);
        var announcement = await CreateTestAnnouncement(tournament.Id, admin.Id, "Title", "Body", "All");

        var request = new UpdateTournamentAnnouncementRequest(
            Title: null,
            Body: null,
            Target: "Captains",
            TargetTeamIds: null
        );

        // Act
        var result = await _sut.UpdateAnnouncementAsync(tournament.Id, announcement.Id, request, admin.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Target.Should().Be("Captains");
    }

    [Fact]
    public async Task UpdateAnnouncementAsync_AdminCanUpdateTargetTeamIds()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var tournament = await CreateTestTournament(admin.Id);
        var team1 = await CreateTestTeam(tournament.Id, "Team A");
        var team2 = await CreateTestTeam(tournament.Id, "Team B");
        var team3 = await CreateTestTeam(tournament.Id, "Team C");

        var announcement = await CreateTestAnnouncement(
            tournament.Id, admin.Id, "Title", "Body", "Teams",
            new List<Guid> { team1.Id });

        var request = new UpdateTournamentAnnouncementRequest(
            Title: null,
            Body: null,
            Target: null,
            TargetTeamIds: new List<Guid> { team2.Id, team3.Id }
        );

        // Act
        var result = await _sut.UpdateAnnouncementAsync(tournament.Id, announcement.Id, request, admin.Id);

        // Assert
        result.Should().NotBeNull();
        result!.TargetTeamIds.Should().HaveCount(2);
        result.TargetTeamIds.Should().Contain(team2.Id);
        result.TargetTeamIds.Should().Contain(team3.Id);
        result.TargetTeamIds.Should().NotContain(team1.Id);
    }

    [Fact]
    public async Task UpdateAnnouncementAsync_NonAdminCannotUpdate()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var player = await CreateTestUser("player@example.com");
        var tournament = await CreateTestTournament(admin.Id);
        var announcement = await CreateTestAnnouncement(tournament.Id, admin.Id, "Title", "Body");

        var request = new UpdateTournamentAnnouncementRequest(
            Title: "Hacked Title",
            Body: null,
            Target: null,
            TargetTeamIds: null
        );

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.UpdateAnnouncementAsync(tournament.Id, announcement.Id, request, player.Id));
    }

    [Fact]
    public async Task UpdateAnnouncementAsync_ReturnsNullForNonExistentAnnouncement()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var tournament = await CreateTestTournament(admin.Id);
        var nonExistentId = Guid.NewGuid();

        var request = new UpdateTournamentAnnouncementRequest(
            Title: "Updated",
            Body: null,
            Target: null,
            TargetTeamIds: null
        );

        // Act
        var result = await _sut.UpdateAnnouncementAsync(tournament.Id, nonExistentId, request, admin.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAnnouncementAsync_UpdatesUpdatedAtTimestamp()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var tournament = await CreateTestTournament(admin.Id);
        var announcement = await CreateTestAnnouncement(tournament.Id, admin.Id, "Title", "Body");

        var originalUpdatedAt = announcement.UpdatedAt;
        await Task.Delay(10); // Ensure time difference

        var request = new UpdateTournamentAnnouncementRequest(
            Title: "Updated Title",
            Body: null,
            Target: null,
            TargetTeamIds: null
        );

        // Act
        var result = await _sut.UpdateAnnouncementAsync(tournament.Id, announcement.Id, request, admin.Id);

        // Assert
        result.Should().NotBeNull();
        result!.UpdatedAt.Should().NotBeNull();
        result.UpdatedAt!.Value.Should().BeAfter(originalUpdatedAt!.Value);
    }

    [Fact]
    public async Task UpdateAnnouncementAsync_CannotUpdateDeletedAnnouncement()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var tournament = await CreateTestTournament(admin.Id);
        var announcement = await CreateTestAnnouncement(tournament.Id, admin.Id, "Title", "Body");

        announcement.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var request = new UpdateTournamentAnnouncementRequest(
            Title: "Updated Title",
            Body: null,
            Target: null,
            TargetTeamIds: null
        );

        // Act
        var result = await _sut.UpdateAnnouncementAsync(tournament.Id, announcement.Id, request, admin.Id);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region DeleteAnnouncementAsync Tests

    [Fact]
    public async Task DeleteAnnouncementAsync_AdminCanSoftDeleteAnnouncement()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var tournament = await CreateTestTournament(admin.Id);
        var announcement = await CreateTestAnnouncement(tournament.Id, admin.Id, "Title", "Body");

        // Act
        var result = await _sut.DeleteAnnouncementAsync(tournament.Id, announcement.Id, admin.Id);

        // Assert
        result.Should().BeTrue();

        var deletedAnnouncement = await _context.TournamentAnnouncements
            .FirstOrDefaultAsync(a => a.Id == announcement.Id);
        deletedAnnouncement.Should().NotBeNull();
        deletedAnnouncement!.DeletedAt.Should().NotBeNull();
        deletedAnnouncement.DeletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DeleteAnnouncementAsync_NonAdminCannotDelete()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var player = await CreateTestUser("player@example.com");
        var tournament = await CreateTestTournament(admin.Id);
        var announcement = await CreateTestAnnouncement(tournament.Id, admin.Id, "Title", "Body");

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.DeleteAnnouncementAsync(tournament.Id, announcement.Id, player.Id));
    }

    [Fact]
    public async Task DeleteAnnouncementAsync_ReturnsFalseForNonExistentAnnouncement()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var tournament = await CreateTestTournament(admin.Id);
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await _sut.DeleteAnnouncementAsync(tournament.Id, nonExistentId, admin.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAnnouncementAsync_DeletedAnnouncementsDoNotAppearInGetAnnouncements()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var tournament = await CreateTestTournament(admin.Id);
        var announcement1 = await CreateTestAnnouncement(tournament.Id, admin.Id, "Keep", "Body");
        var announcement2 = await CreateTestAnnouncement(tournament.Id, admin.Id, "Delete", "Body");

        // Act
        await _sut.DeleteAnnouncementAsync(tournament.Id, announcement2.Id, admin.Id);
        var result = await _sut.GetAnnouncementsAsync(tournament.Id, admin.Id);

        // Assert
        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Keep");
    }

    [Fact]
    public async Task DeleteAnnouncementAsync_ScorekeeperCanDeleteAnnouncement()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var scorekeeper = await CreateTestUser("scorekeeper@example.com");
        var tournament = await CreateTestTournament(owner.Id);
        await AddTournamentAdmin(tournament.Id, scorekeeper.Id, "Scorekeeper", owner.Id);

        var announcement = await CreateTestAnnouncement(tournament.Id, scorekeeper.Id, "Title", "Body");

        // Act
        var result = await _sut.DeleteAnnouncementAsync(tournament.Id, announcement.Id, scorekeeper.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAnnouncementAsync_CannotDeleteAlreadyDeletedAnnouncement()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var tournament = await CreateTestTournament(admin.Id);
        var announcement = await CreateTestAnnouncement(tournament.Id, admin.Id, "Title", "Body");

        announcement.DeletedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.DeleteAnnouncementAsync(tournament.Id, announcement.Id, admin.Id);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Edge Cases and Additional Tests

    [Fact]
    public async Task GetAnnouncementsAsync_PlayerOnMultipleTeamsSeesAnnouncementsForAllTheirTeams()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var player = await CreateTestUser("player@example.com");
        var tournament = await CreateTestTournament(admin.Id);

        var team1 = await CreateTestTeam(tournament.Id, "Team A");
        var team2 = await CreateTestTeam(tournament.Id, "Team B");
        var team3 = await CreateTestTeam(tournament.Id, "Team C");

        await AddTeamMember(team1.Id, player.Id, "Player");
        await AddTeamMember(team2.Id, player.Id, "Player");

        await CreateTestAnnouncement(tournament.Id, admin.Id, "Team A Announcement", "Body", "Teams", new List<Guid> { team1.Id });
        await CreateTestAnnouncement(tournament.Id, admin.Id, "Team B Announcement", "Body", "Teams", new List<Guid> { team2.Id });
        await CreateTestAnnouncement(tournament.Id, admin.Id, "Team C Announcement", "Body", "Teams", new List<Guid> { team3.Id });

        // Act
        var result = await _sut.GetAnnouncementsAsync(tournament.Id, player.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(a => a.Title == "Team A Announcement");
        result.Should().Contain(a => a.Title == "Team B Announcement");
        result.Should().NotContain(a => a.Title == "Team C Announcement");
    }

    [Fact]
    public async Task GetAnnouncementsAsync_AnnouncementTargetingMultipleTeamsVisibleToMembersOfAnyTargetedTeam()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var player1 = await CreateTestUser("player1@example.com");
        var player2 = await CreateTestUser("player2@example.com");
        var tournament = await CreateTestTournament(admin.Id);

        var team1 = await CreateTestTeam(tournament.Id, "Team A");
        var team2 = await CreateTestTeam(tournament.Id, "Team B");

        await AddTeamMember(team1.Id, player1.Id, "Player");
        await AddTeamMember(team2.Id, player2.Id, "Player");

        await CreateTestAnnouncement(
            tournament.Id, admin.Id, "Multi-Team", "Body", "Teams",
            new List<Guid> { team1.Id, team2.Id });

        // Act
        var result1 = await _sut.GetAnnouncementsAsync(tournament.Id, player1.Id);
        var result2 = await _sut.GetAnnouncementsAsync(tournament.Id, player2.Id);

        // Assert
        result1.Should().Contain(a => a.Title == "Multi-Team");
        result2.Should().Contain(a => a.Title == "Multi-Team");
    }

    [Fact]
    public async Task CreateAnnouncementAsync_WithNullRequesterIdThrowsException()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var tournament = await CreateTestTournament(admin.Id);

        var request = new CreateTournamentAnnouncementRequest(
            Title: "Test",
            Body: "Body",
            Target: "All",
            TargetTeamIds: null
        );

        // Act & Assert - this tests that null requester is handled
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.CreateAnnouncementAsync(tournament.Id, request, Guid.Empty));
    }

    [Fact]
    public async Task GetAnnouncementsAsync_CaptainWhoIsAlsoTeamMemberSeesCorrectAnnouncements()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var captain = await CreateTestUser("captain@example.com");
        var tournament = await CreateTestTournament(admin.Id);

        var team = await CreateTestTeam(tournament.Id, "Team A", captain.Id);
        await AddTeamMember(team.Id, captain.Id, "Captain");

        await CreateTestAnnouncement(tournament.Id, admin.Id, "All", "Body", "All");
        await CreateTestAnnouncement(tournament.Id, admin.Id, "Captains", "Body", "Captains");
        await CreateTestAnnouncement(tournament.Id, admin.Id, "Team", "Body", "Teams", new List<Guid> { team.Id });
        await CreateTestAnnouncement(tournament.Id, admin.Id, "Admins", "Body", "Admins");

        // Act
        var result = await _sut.GetAnnouncementsAsync(tournament.Id, captain.Id);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(a => a.Target == "All");
        result.Should().Contain(a => a.Target == "Captains");
        result.Should().Contain(a => a.Target == "Teams");
        result.Should().NotContain(a => a.Target == "Admins");
    }

    #endregion
}
