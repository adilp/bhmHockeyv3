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
/// Tests for TournamentRegistrationService - TDD approach for TRN-007.
/// Tests written FIRST before implementation.
///
/// These tests ensure:
/// - Registration rules are enforced (tournament status, deadlines, duplicate prevention)
/// - Custom responses are saved correctly
/// - Waiver acknowledgment is tracked
/// - Edit/withdrawal permissions are correct
/// - Admin access controls are enforced
/// </summary>
public class TournamentRegistrationServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<ITournamentService> _mockTournamentService;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly TournamentRegistrationService _sut;
    private readonly TournamentAuthorizationService _authService;

    public TournamentRegistrationServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new AppDbContext(options);
        _authService = new TournamentAuthorizationService(_context);
        _mockTournamentService = new Mock<ITournamentService>();
        _mockNotificationService = new Mock<INotificationService>();

        _sut = new TournamentRegistrationService(
            _context,
            _mockTournamentService.Object,
            _mockNotificationService.Object,
            _authService);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Helper Methods

    private async Task<User> CreateTestUser(string email = "test@example.com", string? pushToken = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = "hashed_password",
            FirstName = "Test",
            LastName = "User",
            Positions = new Dictionary<string, string> { { "skater", "Silver" } },
            Role = "Player",
            IsActive = true,
            PushToken = pushToken,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<Organization> CreateTestOrganization(Guid creatorId, string name = "Test Org")
    {
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "Test Description",
            Location = "Boston",
            SkillLevels = new List<string> { "Gold", "Silver" },
            CreatorId = creatorId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Organizations.Add(org);

        // Add creator as org admin
        var admin = new OrganizationAdmin
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            UserId = creatorId,
            AddedAt = DateTime.UtcNow,
            AddedByUserId = null
        };
        _context.OrganizationAdmins.Add(admin);

        await _context.SaveChangesAsync();
        return org;
    }

    private async Task<Tournament> CreateTestTournament(
        Guid creatorId,
        Guid? organizationId = null,
        string status = "Open",
        string name = "Test Tournament",
        DateTime? registrationDeadline = null,
        string? customQuestions = null,
        string? waiverUrl = null,
        decimal entryFee = 50)
    {
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CreatorId = creatorId,
            Name = name,
            Description = "Test Tournament Description",
            Format = "SingleElimination",
            TeamFormation = "OrganizerAssigned",
            Status = status,
            StartDate = DateTime.UtcNow.AddDays(30),
            EndDate = DateTime.UtcNow.AddDays(32),
            RegistrationDeadline = registrationDeadline ?? DateTime.UtcNow.AddDays(25),
            MaxTeams = 8,
            MinPlayersPerTeam = 5,
            MaxPlayersPerTeam = 10,
            AllowMultiTeam = false,
            AllowSubstitutions = true,
            EntryFee = entryFee,
            FeeType = "PerPlayer",
            CustomQuestions = customQuestions,
            WaiverUrl = waiverUrl,
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

    private async Task<TournamentRegistration> CreateTestRegistration(
        Guid tournamentId,
        Guid userId,
        string status = "Registered",
        string? position = "Skater",
        string? customResponses = null,
        string? waiverStatus = null)
    {
        var registration = new TournamentRegistration
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            UserId = userId,
            Status = status,
            Position = position,
            CustomResponses = customResponses,
            WaiverStatus = waiverStatus,
            RegisteredAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TournamentRegistrations.Add(registration);
        await _context.SaveChangesAsync();
        return registration;
    }

    private async Task AddTournamentAdmin(Guid tournamentId, Guid userId, string role = "Admin")
    {
        var admin = new TournamentAdmin
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            UserId = userId,
            Role = role,
            AddedAt = DateTime.UtcNow
        };
        _context.TournamentAdmins.Add(admin);
        await _context.SaveChangesAsync();
    }

    #endregion

    #region RegisterAsync Tests

    [Fact]
    public async Task RegisterAsync_OpenTournament_ReturnsRegistered()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Open");

        var request = new CreateTournamentRegistrationRequest
        {
            Position = "Skater"
        };

        // Act
        var result = await _sut.RegisterAsync(tournament.Id, request, user.Id);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("Registered");

        // Verify in database
        var registration = await _context.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.TournamentId == tournament.Id && r.UserId == user.Id);
        registration.Should().NotBeNull();
        registration!.Status.Should().Be("Registered");
    }

    [Fact]
    public async Task RegisterAsync_ClosedTournament_ThrowsInvalidOperation()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "RegistrationClosed");

        var request = new CreateTournamentRegistrationRequest
        {
            Position = "Skater"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RegisterAsync(tournament.Id, request, user.Id));

        exception.Message.Should().Contain("registration");
    }

    [Fact]
    public async Task RegisterAsync_DraftTournament_ThrowsInvalidOperation()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Draft");

        var request = new CreateTournamentRegistrationRequest
        {
            Position = "Skater"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RegisterAsync(tournament.Id, request, user.Id));

        exception.Message.Should().Contain("registration");
    }

    [Fact]
    public async Task RegisterAsync_AfterDeadline_ThrowsInvalidOperation()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(
            creator.Id,
            status: "Open",
            registrationDeadline: DateTime.UtcNow.AddDays(-1)); // Deadline passed

        var request = new CreateTournamentRegistrationRequest
        {
            Position = "Skater"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RegisterAsync(tournament.Id, request, user.Id));

        exception.Message.Should().Contain("deadline");
    }

    [Fact]
    public async Task RegisterAsync_DuplicateRegistration_ThrowsInvalidOperation()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Open");

        // Create existing registration
        await CreateTestRegistration(tournament.Id, user.Id, "Registered");

        var request = new CreateTournamentRegistrationRequest
        {
            Position = "Skater"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RegisterAsync(tournament.Id, request, user.Id));

        exception.Message.Should().Contain("Already registered");

        // Verify no duplicate was created
        var registrationCount = await _context.TournamentRegistrations
            .CountAsync(r => r.TournamentId == tournament.Id && r.UserId == user.Id);
        registrationCount.Should().Be(1);
    }

    [Fact]
    public async Task RegisterAsync_WithCustomResponses_SavesResponses()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");

        var customQuestions = "[{\"id\":\"q1\",\"question\":\"T-Shirt Size\",\"type\":\"select\",\"options\":[\"S\",\"M\",\"L\",\"XL\"]}]";
        var tournament = await CreateTestTournament(
            creator.Id,
            status: "Open",
            customQuestions: customQuestions);

        var customResponses = "{\"q1\":\"L\"}";
        var request = new CreateTournamentRegistrationRequest
        {
            Position = "Skater",
            CustomResponses = customResponses
        };

        // Act
        var result = await _sut.RegisterAsync(tournament.Id, request, user.Id);

        // Assert
        result.Should().NotBeNull();

        // Verify in database
        var registration = await _context.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.TournamentId == tournament.Id && r.UserId == user.Id);
        registration.Should().NotBeNull();
        registration!.CustomResponses.Should().Be(customResponses);
    }

    [Fact]
    public async Task RegisterAsync_WithWaiver_SetsWaiverStatus()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(
            creator.Id,
            status: "Open",
            waiverUrl: "https://example.com/waiver");

        var request = new CreateTournamentRegistrationRequest
        {
            Position = "Skater",
            WaiverAccepted = true
        };

        // Act
        var result = await _sut.RegisterAsync(tournament.Id, request, user.Id);

        // Assert
        result.Should().NotBeNull();

        // Verify in database - WaiverStatus should be "Signed"
        var registration = await _context.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.TournamentId == tournament.Id && r.UserId == user.Id);
        registration.Should().NotBeNull();
        registration!.WaiverStatus.Should().Be("Signed");
    }

    [Fact]
    public async Task RegisterAsync_NonExistentTournament_ThrowsInvalidOperation()
    {
        // Arrange
        var user = await CreateTestUser("user@example.com");
        var request = new CreateTournamentRegistrationRequest
        {
            Position = "Skater"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RegisterAsync(Guid.NewGuid(), request, user.Id));

        exception.Message.Should().Contain("not found");
    }

    [Fact]
    public async Task RegisterAsync_CancelledTournament_ThrowsInvalidOperation()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Cancelled");

        var request = new CreateTournamentRegistrationRequest
        {
            Position = "Skater"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RegisterAsync(tournament.Id, request, user.Id));

        exception.Message.Should().Contain("registration");
    }

    [Fact]
    public async Task RegisterAsync_AfterPreviousCancellation_AllowsReRegistration()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Open");

        // Create and cancel a registration
        await CreateTestRegistration(tournament.Id, user.Id, "Cancelled");

        var request = new CreateTournamentRegistrationRequest
        {
            Position = "Skater"
        };

        // Act
        var result = await _sut.RegisterAsync(tournament.Id, request, user.Id);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("Registered");

        // Verify reactivated (not duplicated)
        var registrations = await _context.TournamentRegistrations
            .Where(r => r.TournamentId == tournament.Id && r.UserId == user.Id)
            .ToListAsync();
        registrations.Should().HaveCount(1);
        registrations.First().Status.Should().Be("Registered");
    }

    #endregion

    #region GetMyRegistrationAsync Tests

    [Fact]
    public async Task GetMyRegistrationAsync_WhenRegistered_ReturnsRegistration()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Open");
        await CreateTestRegistration(tournament.Id, user.Id, "Registered");

        // Act
        var result = await _sut.GetMyRegistrationAsync(tournament.Id, user.Id);

        // Assert
        result.Should().NotBeNull();
        result!.User.Id.Should().Be(user.Id);
        result.TournamentId.Should().Be(tournament.Id);
        result.Status.Should().Be("Registered");
    }

    [Fact]
    public async Task GetMyRegistrationAsync_WhenNotRegistered_ReturnsNull()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Open");

        // Act
        var result = await _sut.GetMyRegistrationAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetMyRegistrationAsync_WhenCancelled_ReturnsNull()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Open");
        await CreateTestRegistration(tournament.Id, user.Id, "Cancelled");

        // Act
        var result = await _sut.GetMyRegistrationAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_BeforeDeadline_Succeeds()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(
            creator.Id,
            status: "Open",
            registrationDeadline: DateTime.UtcNow.AddDays(10));

        await CreateTestRegistration(tournament.Id, user.Id, "Registered");

        var updateRequest = new UpdateTournamentRegistrationRequest
        {
            Position = "Goalie"
        };

        // Act
        var result = await _sut.UpdateAsync(tournament.Id, updateRequest, user.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Position.Should().Be("Goalie");
    }

    [Fact]
    public async Task UpdateAsync_AfterDeadline_ThrowsInvalidOperation()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(
            creator.Id,
            status: "Open",
            registrationDeadline: DateTime.UtcNow.AddDays(-1)); // Deadline passed

        await CreateTestRegistration(tournament.Id, user.Id, "Registered");

        var updateRequest = new UpdateTournamentRegistrationRequest
        {
            Position = "Goalie"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateAsync(tournament.Id, updateRequest, user.Id));

        exception.Message.Should().Contain("deadline");
    }

    [Fact]
    public async Task UpdateAsync_NotOwner_ThrowsUnauthorized()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var registeredUser = await CreateTestUser("registered@example.com");
        var otherUser = await CreateTestUser("other@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Open");

        await CreateTestRegistration(tournament.Id, registeredUser.Id, "Registered", position: "Skater");

        var updateRequest = new UpdateTournamentRegistrationRequest
        {
            Position = "Goalie"
        };

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.UpdateAsync(tournament.Id, updateRequest, otherUser.Id));

        // Verify registration was not modified
        var unchanged = await _context.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.TournamentId == tournament.Id && r.UserId == registeredUser.Id);
        unchanged!.Position.Should().Be("Skater");
    }

    [Fact]
    public async Task UpdateAsync_WhenNotRegistered_ReturnsNull()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Open");

        var updateRequest = new UpdateTournamentRegistrationRequest
        {
            Position = "Goalie"
        };

        // Act
        var result = await _sut.UpdateAsync(tournament.Id, updateRequest, user.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithCustomResponses_UpdatesResponses()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Open");

        await CreateTestRegistration(
            tournament.Id,
            user.Id,
            "Registered",
            customResponses: "{\"q1\":\"S\"}");

        var updateRequest = new UpdateTournamentRegistrationRequest
        {
            CustomResponses = "{\"q1\":\"XL\"}"
        };

        // Act
        var result = await _sut.UpdateAsync(tournament.Id, updateRequest, user.Id);

        // Assert
        result.Should().NotBeNull();

        // Verify in database
        var updated = await _context.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.TournamentId == tournament.Id && r.UserId == user.Id);
        updated!.CustomResponses.Should().Be("{\"q1\":\"XL\"}");
    }

    #endregion

    #region WithdrawAsync Tests

    [Fact]
    public async Task WithdrawAsync_SetsStatusCancelled()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Open");
        await CreateTestRegistration(tournament.Id, user.Id, "Registered");

        // Act
        var result = await _sut.WithdrawAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeTrue();

        var registration = await _context.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.TournamentId == tournament.Id && r.UserId == user.Id);
        registration!.Status.Should().Be("Cancelled");
        registration.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public async Task WithdrawAsync_NotOwner_ThrowsUnauthorized()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var registeredUser = await CreateTestUser("registered@example.com");
        var otherUser = await CreateTestUser("other@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Open");

        await CreateTestRegistration(tournament.Id, registeredUser.Id, "Registered");

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.WithdrawAsync(tournament.Id, otherUser.Id));

        // Verify registration was not modified
        var unchanged = await _context.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.TournamentId == tournament.Id && r.UserId == registeredUser.Id);
        unchanged!.Status.Should().Be("Registered");
    }

    [Fact]
    public async Task WithdrawAsync_WhenNotRegistered_ReturnsFalse()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Open");

        // Act
        var result = await _sut.WithdrawAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task WithdrawAsync_AlreadyCancelled_ReturnsFalse()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Open");
        await CreateTestRegistration(tournament.Id, user.Id, "Cancelled");

        // Act
        var result = await _sut.WithdrawAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetAllAsync (Admin) Tests

    [Fact]
    public async Task GetAllAsync_AsAdmin_ReturnsAllRegistrations()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user1 = await CreateTestUser("user1@example.com");
        var user2 = await CreateTestUser("user2@example.com");
        var user3 = await CreateTestUser("user3@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Open");

        await CreateTestRegistration(tournament.Id, user1.Id, "Registered");
        await CreateTestRegistration(tournament.Id, user2.Id, "Registered");
        await CreateTestRegistration(tournament.Id, user3.Id, "Cancelled");

        // Setup mock for tournament admin check
        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.GetAllAsync(tournament.Id, creator.Id);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(r => r.User.Id == user1.Id && r.Status == "Registered");
        result.Should().Contain(r => r.User.Id == user2.Id && r.Status == "Registered");
        result.Should().Contain(r => r.User.Id == user3.Id && r.Status == "Cancelled");
    }

    [Fact]
    public async Task GetAllAsync_AsNonAdmin_ThrowsUnauthorized()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var nonAdmin = await CreateTestUser("nonadmin@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Open");

        // Setup mock for tournament admin check
        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, nonAdmin.Id))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.GetAllAsync(tournament.Id, nonAdmin.Id));
    }

    [Fact]
    public async Task GetAllAsync_AsAdmin_ReturnsEmptyListWhenNoRegistrations()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Open");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.GetAllAsync(tournament.Id, creator.Id);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_AsAdmin_IncludesUserDetails()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("player@example.com");
        user.FirstName = "John";
        user.LastName = "Player";
        await _context.SaveChangesAsync();

        var tournament = await CreateTestTournament(creator.Id, status: "Open");
        await CreateTestRegistration(tournament.Id, user.Id, "Registered");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.GetAllAsync(tournament.Id, creator.Id);

        // Assert
        result.Should().HaveCount(1);
        var registration = result.First();
        registration.User.FirstName.Should().Be("John");
        registration.User.LastName.Should().Be("Player");
        registration.User.Email.Should().Be("player@example.com");
    }

    #endregion

    #region Admin Override Tests

    [Fact]
    public async Task UpdateAsync_AsAdmin_CanUpdateAnyRegistration()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var registeredUser = await CreateTestUser("registered@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Open");

        await CreateTestRegistration(tournament.Id, registeredUser.Id, "Registered", position: "Skater");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        var updateRequest = new UpdateTournamentRegistrationRequest
        {
            Position = "Goalie"
        };

        // Act
        var result = await _sut.UpdateAsync(tournament.Id, updateRequest, creator.Id, registeredUser.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Position.Should().Be("Goalie");
    }

    [Fact]
    public async Task WithdrawAsync_AsAdmin_CanWithdrawAnyRegistration()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var registeredUser = await CreateTestUser("registered@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Open");

        await CreateTestRegistration(tournament.Id, registeredUser.Id, "Registered");

        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.WithdrawAsync(tournament.Id, creator.Id, registeredUser.Id);

        // Assert
        result.Should().BeTrue();

        var registration = await _context.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.TournamentId == tournament.Id && r.UserId == registeredUser.Id);
        registration!.Status.Should().Be("Cancelled");
    }

    #endregion

    #region Payment Tests (TRN-008)

    [Fact]
    public async Task MarkPaymentAsync_WithPendingPayment_TransitionsToMarkedPaid()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Open");

        var registration = await CreateTestRegistration(tournament.Id, user.Id, "Registered");

        // Set initial payment status to Pending
        registration.PaymentStatus = "Pending";
        registration.PaymentMarkedAt = null;
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.MarkPaymentAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeTrue();

        // Verify database update
        var updated = await _context.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.Id == registration.Id);
        updated.Should().NotBeNull();
        updated!.PaymentStatus.Should().Be("MarkedPaid");
        updated.PaymentMarkedAt.Should().NotBeNull();
        updated.PaymentMarkedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task MarkPaymentAsync_WhenAlreadyMarkedPaid_ReturnsFalse()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Open");

        var registration = await CreateTestRegistration(tournament.Id, user.Id, "Registered");

        // Already marked as paid
        registration.PaymentStatus = "MarkedPaid";
        registration.PaymentMarkedAt = DateTime.UtcNow.AddHours(-1);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.MarkPaymentAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task MarkPaymentAsync_WhenAlreadyVerified_ReturnsFalse()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Open");

        var registration = await CreateTestRegistration(tournament.Id, user.Id, "Registered");

        // Already verified by admin
        registration.PaymentStatus = "Verified";
        registration.PaymentVerifiedAt = DateTime.UtcNow.AddHours(-1);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.MarkPaymentAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task MarkPaymentAsync_ForFreeTournament_ReturnsFalse()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");

        // Create free tournament (EntryFee = 0)
        var tournament = await CreateTestTournament(creator.Id, status: "Open");
        tournament.EntryFee = 0;
        await _context.SaveChangesAsync();

        var registration = await CreateTestRegistration(tournament.Id, user.Id, "Registered");

        // Free tournaments should have null PaymentStatus
        registration.PaymentStatus = null;
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.MarkPaymentAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeFalse();

        // Verify PaymentStatus remains null
        var updated = await _context.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.Id == registration.Id);
        updated!.PaymentStatus.Should().BeNull();
    }

    [Fact]
    public async Task MarkPaymentAsync_WhenNotRegistered_ReturnsFalse()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Open");

        // No registration exists for this user

        // Act
        var result = await _sut.MarkPaymentAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task MarkPaymentAsync_WhenRegistrationCancelled_ReturnsFalse()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Open");

        var registration = await CreateTestRegistration(tournament.Id, user.Id, "Cancelled");
        registration.PaymentStatus = "Pending";
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.MarkPaymentAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeFalse();

        // Verify status unchanged
        var unchanged = await _context.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.Id == registration.Id);
        unchanged!.PaymentStatus.Should().Be("Pending");
        unchanged.PaymentMarkedAt.Should().BeNull();
    }

    [Fact]
    public async Task VerifyPaymentAsync_AsAdmin_TransitionsToVerified()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Open");

        var registration = await CreateTestRegistration(tournament.Id, user.Id, "Registered");
        registration.PaymentStatus = "MarkedPaid";
        registration.PaymentMarkedAt = DateTime.UtcNow.AddHours(-1);
        await _context.SaveChangesAsync();

        // Setup admin check
        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.VerifyPaymentAsync(tournament.Id, registration.Id, verified: true, creator.Id);

        // Assert
        result.Should().NotBeNull();
        result!.PaymentStatus.Should().Be("Verified");
        result.PaymentVerifiedAt.Should().NotBeNull();
        result.PaymentVerifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        // Verify database update
        var updated = await _context.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.Id == registration.Id);
        updated!.PaymentStatus.Should().Be("Verified");
        updated.PaymentVerifiedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task VerifyPaymentAsync_AsNonAdmin_ThrowsUnauthorized()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var nonAdmin = await CreateTestUser("nonadmin@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Open");

        var registration = await CreateTestRegistration(tournament.Id, user.Id, "Registered");
        registration.PaymentStatus = "MarkedPaid";
        await _context.SaveChangesAsync();

        // Setup non-admin check
        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, nonAdmin.Id))
            .ReturnsAsync(false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.VerifyPaymentAsync(tournament.Id, registration.Id, verified: true, nonAdmin.Id));

        // Verify no changes made
        var unchanged = await _context.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.Id == registration.Id);
        unchanged!.PaymentStatus.Should().Be("MarkedPaid");
        unchanged.PaymentVerifiedAt.Should().BeNull();
    }

    [Fact]
    public async Task VerifyPaymentAsync_ResetToPending_Succeeds()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Open");

        var registration = await CreateTestRegistration(tournament.Id, user.Id, "Registered");

        // Previously verified
        registration.PaymentStatus = "Verified";
        registration.PaymentVerifiedAt = DateTime.UtcNow.AddHours(-2);
        await _context.SaveChangesAsync();

        // Setup admin check
        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        // Act - Admin resets verification back to pending
        var result = await _sut.VerifyPaymentAsync(tournament.Id, registration.Id, verified: false, creator.Id);

        // Assert
        result.Should().NotBeNull();
        result!.PaymentStatus.Should().Be("Pending");
        result.PaymentVerifiedAt.Should().BeNull();

        // Verify database update
        var updated = await _context.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.Id == registration.Id);
        updated!.PaymentStatus.Should().Be("Pending");
        updated.PaymentVerifiedAt.Should().BeNull();
    }

    [Fact]
    public async Task VerifyPaymentAsync_RegistrationNotFound_ReturnsNull()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Open");

        var nonExistentRegistrationId = Guid.NewGuid();

        // Setup admin check
        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.VerifyPaymentAsync(tournament.Id, nonExistentRegistrationId, verified: true, creator.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task VerifyPaymentAsync_FreeTournament_ReturnsNull()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");

        // Create free tournament (EntryFee = 0)
        var tournament = await CreateTestTournament(creator.Id, status: "Open");
        tournament.EntryFee = 0;
        await _context.SaveChangesAsync();

        var registration = await CreateTestRegistration(tournament.Id, user.Id, "Registered");

        // Free tournaments have null PaymentStatus
        registration.PaymentStatus = null;
        await _context.SaveChangesAsync();

        // Setup admin check
        _mockTournamentService.Setup(ts => ts.CanUserManageTournamentAsync(tournament.Id, creator.Id))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.VerifyPaymentAsync(tournament.Id, registration.Id, verified: true, creator.Id);

        // Assert
        result.Should().BeNull();

        // Verify PaymentStatus remains null
        var unchanged = await _context.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.Id == registration.Id);
        unchanged!.PaymentStatus.Should().BeNull();
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task RegisterAsync_TournamentInProgress_ThrowsInvalidOperation()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "InProgress");

        var request = new CreateTournamentRegistrationRequest
        {
            Position = "Skater"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RegisterAsync(tournament.Id, request, user.Id));

        exception.Message.Should().Contain("registration");
    }

    [Fact]
    public async Task RegisterAsync_CompletedTournament_ThrowsInvalidOperation()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Completed");

        var request = new CreateTournamentRegistrationRequest
        {
            Position = "Skater"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RegisterAsync(tournament.Id, request, user.Id));

        exception.Message.Should().Contain("registration");
    }

    [Fact]
    public async Task RegisterAsync_PostponedTournament_ThrowsInvalidOperation()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Postponed");

        var request = new CreateTournamentRegistrationRequest
        {
            Position = "Skater"
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RegisterAsync(tournament.Id, request, user.Id));

        exception.Message.Should().Contain("registration");
    }

    [Fact]
    public async Task RegisterAsync_SetsTimestamps()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Open");

        var request = new CreateTournamentRegistrationRequest
        {
            Position = "Skater"
        };

        // Act
        var result = await _sut.RegisterAsync(tournament.Id, request, user.Id);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("Registered");

        // Verify timestamps are set in database
        var registration = await _context.TournamentRegistrations
            .FirstOrDefaultAsync(r => r.TournamentId == tournament.Id && r.UserId == user.Id);
        registration!.RegisteredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UpdateAsync_UpdatesTimestamp()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var tournament = await CreateTestTournament(creator.Id, status: "Open");

        var registration = await CreateTestRegistration(tournament.Id, user.Id, "Registered");
        var originalUpdatedAt = registration.UpdatedAt;

        await Task.Delay(10); // Ensure time difference

        var updateRequest = new UpdateTournamentRegistrationRequest
        {
            Position = "Goalie"
        };

        // Act
        var result = await _sut.UpdateAsync(tournament.Id, updateRequest, user.Id);

        // Assert
        result.Should().NotBeNull();
        result!.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    #endregion
}
