using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// Tests for TournamentService.GetUserTournamentsAsync - TDD approach for TRN-026.
/// Tests written FIRST before implementation.
/// Tests are expected to FAIL until GetUserTournamentsAsync method is implemented.
/// </summary>
public class TournamentHistoryServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly OrganizationAdminService _orgAdminService;
    private readonly TournamentAuthorizationService _authService;
    private readonly TournamentService _sut;

    public TournamentHistoryServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _authService = new TournamentAuthorizationService(_context);
        _orgAdminService = new OrganizationAdminService(_context);
        _sut = new TournamentService(_context, _orgAdminService, _authService);
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

    private async Task<Organization> CreateTestOrganization(Guid creatorId, string name = "Test Org")
    {
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = "Test Description",
            Location = "Boston",
            SkillLevels = new List<string> { "Gold" },
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
        string status = "Open",
        string name = "Test Tournament",
        Guid? organizationId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        DateTime? completedAt = null)
    {
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CreatorId = creatorId,
            Name = name,
            Format = "SingleElimination",
            TeamFormation = "OrganizerAssigned",
            Status = status,
            StartDate = startDate ?? DateTime.UtcNow.AddDays(30),
            EndDate = endDate ?? DateTime.UtcNow.AddDays(32),
            RegistrationDeadline = DateTime.UtcNow.AddDays(25),
            MaxTeams = 8,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CompletedAt = completedAt
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

    private async Task<TournamentTeam> CreateTestTeam(
        Guid tournamentId,
        string name = "Test Team",
        string status = "Active",
        int? finalPlacement = null)
    {
        var team = new TournamentTeam
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            Name = name,
            Status = status,
            FinalPlacement = finalPlacement,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TournamentTeams.Add(team);
        await _context.SaveChangesAsync();
        return team;
    }

    private async Task<TournamentTeamMember> AddUserToTeam(
        Guid teamId,
        Guid userId,
        string role = "Player",
        string status = "Accepted",
        DateTime? leftAt = null)
    {
        var member = new TournamentTeamMember
        {
            Id = Guid.NewGuid(),
            TeamId = teamId,
            UserId = userId,
            Role = role,
            Status = status,
            JoinedAt = DateTime.UtcNow,
            LeftAt = leftAt
        };

        _context.TournamentTeamMembers.Add(member);
        await _context.SaveChangesAsync();
        return member;
    }

    private async Task<TournamentAdmin> AddUserAsAdmin(
        Guid tournamentId,
        Guid userId,
        string role = "Admin",
        DateTime? removedAt = null)
    {
        var admin = new TournamentAdmin
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            UserId = userId,
            Role = role,
            AddedAt = DateTime.UtcNow,
            RemovedAt = removedAt
        };

        _context.TournamentAdmins.Add(admin);
        await _context.SaveChangesAsync();
        return admin;
    }

    private async Task<TournamentRegistration> CreateTestRegistration(
        Guid tournamentId,
        Guid userId,
        string status = "Registered")
    {
        var registration = new TournamentRegistration
        {
            Id = Guid.NewGuid(),
            TournamentId = tournamentId,
            UserId = userId,
            Status = status,
            RegisteredAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TournamentRegistrations.Add(registration);
        await _context.SaveChangesAsync();
        return registration;
    }

    #endregion

    #region GetUserTournamentsAsync Tests

    [Fact]
    public async Task GetUserTournaments_UserWithNoTournaments_ReturnsEmptyLists()
    {
        // Arrange
        var user = await CreateTestUser();

        // Act
        var result = await _sut.GetUserTournamentsAsync(user.Id, user.Id, null);

        // Assert
        result.Should().NotBeNull();
        result.Active.Should().BeEmpty();
        result.Past.Should().BeEmpty();
        result.Organizing.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserTournaments_UserWithActiveTournament_ReturnsInActiveSection()
    {
        // Arrange
        var user = await CreateTestUser();
        var creator = await CreateTestUser("creator@example.com");
        var tournament = await CreateTestTournament(
            creator.Id,
            status: "Open",
            name: "Active Tournament",
            startDate: DateTime.UtcNow.AddDays(10),
            endDate: DateTime.UtcNow.AddDays(12)
        );
        var team = await CreateTestTeam(tournament.Id);
        await AddUserToTeam(team.Id, user.Id);

        // Act
        var result = await _sut.GetUserTournamentsAsync(user.Id, user.Id, null);

        // Assert
        result.Should().NotBeNull();
        result.Active.Should().HaveCount(1);
        result.Active[0].Id.Should().Be(tournament.Id);
        result.Active[0].Name.Should().Be("Active Tournament");
        result.Active[0].Status.Should().Be("Open");
        result.Active[0].TeamName.Should().NotBeNull();
        result.Past.Should().BeEmpty();
        result.Organizing.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserTournaments_UserWithInProgressTournament_ReturnsInActiveSection()
    {
        // Arrange
        var user = await CreateTestUser();
        var creator = await CreateTestUser("creator@example.com");
        var tournament = await CreateTestTournament(
            creator.Id,
            status: "InProgress",
            name: "Ongoing Tournament"
        );
        var team = await CreateTestTeam(tournament.Id);
        await AddUserToTeam(team.Id, user.Id);

        // Act
        var result = await _sut.GetUserTournamentsAsync(user.Id, user.Id, null);

        // Assert
        result.Should().NotBeNull();
        result.Active.Should().HaveCount(1);
        result.Active[0].Status.Should().Be("InProgress");
        result.Past.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserTournaments_UserWithPastTournament_ReturnsInPastSection()
    {
        // Arrange
        var user = await CreateTestUser();
        var creator = await CreateTestUser("creator@example.com");
        var tournament = await CreateTestTournament(
            creator.Id,
            status: "Completed",
            name: "Past Tournament",
            startDate: DateTime.UtcNow.AddDays(-30),
            endDate: DateTime.UtcNow.AddDays(-28),
            completedAt: DateTime.UtcNow.AddDays(-28)
        );
        var team = await CreateTestTeam(tournament.Id, finalPlacement: 2);
        await AddUserToTeam(team.Id, user.Id);

        // Act
        var result = await _sut.GetUserTournamentsAsync(user.Id, user.Id, null);

        // Assert
        result.Should().NotBeNull();
        result.Past.Should().HaveCount(1);
        result.Past[0].Id.Should().Be(tournament.Id);
        result.Past[0].Status.Should().Be("Completed");
        result.Past[0].FinalPlacement.Should().Be(2);
        result.Active.Should().BeEmpty();
        result.Organizing.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserTournaments_UserIsAdmin_ReturnsInOrganizingSection()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, status: "Open", name: "My Tournament");

        // Act
        var result = await _sut.GetUserTournamentsAsync(user.Id, user.Id, null);

        // Assert
        result.Should().NotBeNull();
        result.Organizing.Should().HaveCount(1);
        result.Organizing[0].Id.Should().Be(tournament.Id);
        result.Organizing[0].Name.Should().Be("My Tournament");
        result.Organizing[0].UserRole.Should().Be("Owner");
        // Admin role takes precedence, so should not appear in Active
        result.Active.Should().BeEmpty();
        result.Past.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserTournaments_FilterByWon_OnlyReturnsWinningTournaments()
    {
        // Arrange
        var user = await CreateTestUser();
        var creator = await CreateTestUser("creator@example.com");

        // Tournament where user won (placement 1)
        var wonTournament = await CreateTestTournament(
            creator.Id,
            status: "Completed",
            name: "Won Tournament"
        );
        var wonTeam = await CreateTestTeam(wonTournament.Id, finalPlacement: 1);
        await AddUserToTeam(wonTeam.Id, user.Id);

        // Tournament where user lost (placement 2)
        var lostTournament = await CreateTestTournament(
            creator.Id,
            status: "Completed",
            name: "Lost Tournament"
        );
        var lostTeam = await CreateTestTeam(lostTournament.Id, finalPlacement: 2);
        await AddUserToTeam(lostTeam.Id, user.Id);

        var filter = new UserTournamentsFilterDto { Filter = "won" };

        // Act
        var result = await _sut.GetUserTournamentsAsync(user.Id, user.Id, filter);

        // Assert
        result.Should().NotBeNull();
        result.Past.Should().HaveCount(1);
        result.Past[0].Id.Should().Be(wonTournament.Id);
        result.Past[0].FinalPlacement.Should().Be(1);
    }

    [Fact]
    public async Task GetUserTournaments_FilterByYear_ReturnsCorrectYear()
    {
        // Arrange
        var user = await CreateTestUser();
        var creator = await CreateTestUser("creator@example.com");

        // Tournament in 2025
        var tournament2025 = await CreateTestTournament(
            creator.Id,
            status: "Completed",
            name: "2025 Tournament",
            startDate: new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            endDate: new DateTime(2025, 6, 3, 0, 0, 0, DateTimeKind.Utc)
        );
        var team2025 = await CreateTestTeam(tournament2025.Id);
        await AddUserToTeam(team2025.Id, user.Id);

        // Tournament in 2026
        var tournament2026 = await CreateTestTournament(
            creator.Id,
            status: "Completed",
            name: "2026 Tournament",
            startDate: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            endDate: new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc)
        );
        var team2026 = await CreateTestTeam(tournament2026.Id);
        await AddUserToTeam(team2026.Id, user.Id);

        var filter = new UserTournamentsFilterDto { Year = 2025 };

        // Act
        var result = await _sut.GetUserTournamentsAsync(user.Id, user.Id, filter);

        // Assert
        result.Should().NotBeNull();
        result.Past.Should().HaveCount(1);
        result.Past[0].Id.Should().Be(tournament2025.Id);
        result.Past[0].StartDate.Year.Should().Be(2025);
    }

    [Fact]
    public async Task GetUserTournaments_UserLeftTeamMidTournament_StillInHistory()
    {
        // Arrange
        var user = await CreateTestUser();
        var creator = await CreateTestUser("creator@example.com");
        var tournament = await CreateTestTournament(
            creator.Id,
            status: "Completed",
            name: "Tournament Left Mid-Game"
        );
        var team = await CreateTestTeam(tournament.Id, finalPlacement: 3);
        await AddUserToTeam(team.Id, user.Id, leftAt: DateTime.UtcNow.AddDays(-15));

        // Act
        var result = await _sut.GetUserTournamentsAsync(user.Id, user.Id, null);

        // Assert
        result.Should().NotBeNull();
        result.Past.Should().HaveCount(1);
        result.Past[0].Id.Should().Be(tournament.Id);
        result.Past[0].TeamName.Should().NotBeNull();
        result.Past[0].FinalPlacement.Should().Be(3);
    }

    [Fact]
    public async Task GetUserTournaments_PublicView_OnlyReturnsPast()
    {
        // Arrange
        var targetUser = await CreateTestUser("target@example.com");
        var currentUser = await CreateTestUser("viewer@example.com");
        var creator = await CreateTestUser("creator@example.com");

        // Active tournament
        var activeTournament = await CreateTestTournament(
            creator.Id,
            status: "Open",
            name: "Active Tournament"
        );
        var activeTeam = await CreateTestTeam(activeTournament.Id);
        await AddUserToTeam(activeTeam.Id, targetUser.Id);

        // Past tournament
        var pastTournament = await CreateTestTournament(
            creator.Id,
            status: "Completed",
            name: "Past Tournament"
        );
        var pastTeam = await CreateTestTeam(pastTournament.Id);
        await AddUserToTeam(pastTeam.Id, targetUser.Id);

        // Organizing tournament
        var organizingTournament = await CreateTestTournament(
            targetUser.Id,
            status: "Open",
            name: "Organizing Tournament"
        );

        // Act - currentUser viewing targetUser's profile
        var result = await _sut.GetUserTournamentsAsync(targetUser.Id, currentUser.Id, null);

        // Assert
        result.Should().NotBeNull();
        result.Past.Should().HaveCount(1);
        result.Past[0].Id.Should().Be(pastTournament.Id);
        result.Active.Should().BeEmpty("active tournaments should be private");
        result.Organizing.Should().BeEmpty("organizing tournaments should be private");
    }

    [Fact]
    public async Task GetUserTournaments_UserIsBothPlayerAndAdmin_AppearsInBothSections()
    {
        // Arrange
        var user = await CreateTestUser();
        var creator = await CreateTestUser("creator@example.com");

        // Tournament where user is both admin and player
        var tournament = await CreateTestTournament(
            creator.Id,
            status: "Open",
            name: "Dual Role Tournament"
        );
        await AddUserAsAdmin(tournament.Id, user.Id, role: "Admin");
        var team = await CreateTestTeam(tournament.Id);
        await AddUserToTeam(team.Id, user.Id);

        // Act
        var result = await _sut.GetUserTournamentsAsync(user.Id, user.Id, null);

        // Assert
        result.Should().NotBeNull();
        result.Organizing.Should().HaveCount(1, "user is an admin");
        result.Organizing[0].Id.Should().Be(tournament.Id);
        result.Organizing[0].UserRole.Should().Be("Admin");
        // When user is admin, admin role takes precedence
        result.Active.Should().BeEmpty("admin role takes precedence over player role");
    }

    [Fact]
    public async Task GetUserTournaments_ActiveOrderedByStartDateAsc_PastOrderedByEndDateDesc()
    {
        // Arrange
        var user = await CreateTestUser();
        var creator = await CreateTestUser("creator@example.com");

        // Create active tournaments with different start dates
        var activeTournament1 = await CreateTestTournament(
            creator.Id,
            status: "Open",
            name: "Active 1",
            startDate: DateTime.UtcNow.AddDays(30),
            endDate: DateTime.UtcNow.AddDays(32)
        );
        var activeTeam1 = await CreateTestTeam(activeTournament1.Id);
        await AddUserToTeam(activeTeam1.Id, user.Id);

        var activeTournament2 = await CreateTestTournament(
            creator.Id,
            status: "Open",
            name: "Active 2",
            startDate: DateTime.UtcNow.AddDays(10),
            endDate: DateTime.UtcNow.AddDays(12)
        );
        var activeTeam2 = await CreateTestTeam(activeTournament2.Id);
        await AddUserToTeam(activeTeam2.Id, user.Id);

        // Create past tournaments with different end dates
        var pastTournament1 = await CreateTestTournament(
            creator.Id,
            status: "Completed",
            name: "Past 1",
            startDate: DateTime.UtcNow.AddDays(-30),
            endDate: DateTime.UtcNow.AddDays(-28)
        );
        var pastTeam1 = await CreateTestTeam(pastTournament1.Id);
        await AddUserToTeam(pastTeam1.Id, user.Id);

        var pastTournament2 = await CreateTestTournament(
            creator.Id,
            status: "Completed",
            name: "Past 2",
            startDate: DateTime.UtcNow.AddDays(-10),
            endDate: DateTime.UtcNow.AddDays(-8)
        );
        var pastTeam2 = await CreateTestTeam(pastTournament2.Id);
        await AddUserToTeam(pastTeam2.Id, user.Id);

        // Act
        var result = await _sut.GetUserTournamentsAsync(user.Id, user.Id, null);

        // Assert
        result.Should().NotBeNull();

        // Active tournaments ordered by start date ascending (soonest first)
        result.Active.Should().HaveCount(2);
        result.Active[0].Name.Should().Be("Active 2", "it starts sooner");
        result.Active[1].Name.Should().Be("Active 1", "it starts later");

        // Past tournaments ordered by end date descending (most recent first)
        result.Past.Should().HaveCount(2);
        result.Past[0].Name.Should().Be("Past 2", "it ended more recently");
        result.Past[1].Name.Should().Be("Past 1", "it ended earlier");
    }

    [Fact]
    public async Task GetUserTournaments_DraftTournaments_NotIncludedUnlessAdmin()
    {
        // Arrange
        var user = await CreateTestUser();
        var creator = await CreateTestUser("creator@example.com");

        // Draft tournament where user is registered but not admin
        var draftTournament = await CreateTestTournament(
            creator.Id,
            status: "Draft",
            name: "Draft Tournament"
        );
        await CreateTestRegistration(draftTournament.Id, user.Id, status: "Registered");

        // Act
        var result = await _sut.GetUserTournamentsAsync(user.Id, user.Id, null);

        // Assert
        result.Should().NotBeNull();
        result.Active.Should().BeEmpty("draft tournaments should not appear for non-admins");
        result.Past.Should().BeEmpty();
        result.Organizing.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserTournaments_RemovedAdmin_DoesNotAppearInOrganizing()
    {
        // Arrange
        var user = await CreateTestUser();
        var creator = await CreateTestUser("creator@example.com");
        var tournament = await CreateTestTournament(
            creator.Id,
            status: "Open",
            name: "Tournament"
        );

        // Add user as admin but then remove them
        await AddUserAsAdmin(tournament.Id, user.Id, role: "Admin", removedAt: DateTime.UtcNow.AddDays(-5));

        // Act
        var result = await _sut.GetUserTournamentsAsync(user.Id, user.Id, null);

        // Assert
        result.Should().NotBeNull();
        result.Organizing.Should().BeEmpty("removed admins should not appear");
    }

    [Fact]
    public async Task GetUserTournaments_WithOrganizationContext_IncludesOrganizationInfo()
    {
        // Arrange
        var user = await CreateTestUser();
        var creator = await CreateTestUser("creator@example.com");
        var org = await CreateTestOrganization(creator.Id, "Boston Hockey");

        var tournament = await CreateTestTournament(
            creator.Id,
            status: "Completed",
            name: "Org Tournament",
            organizationId: org.Id
        );
        var team = await CreateTestTeam(tournament.Id);
        await AddUserToTeam(team.Id, user.Id);

        // Act
        var result = await _sut.GetUserTournamentsAsync(user.Id, user.Id, null);

        // Assert
        result.Should().NotBeNull();
        result.Past.Should().HaveCount(1);
        result.Past[0].OrganizationId.Should().NotBeNull();
        result.Past[0].OrganizationName.Should().Be("Boston Hockey");
    }

    [Fact]
    public async Task GetUserTournaments_PostponedTournament_NotIncluded()
    {
        // Arrange
        var user = await CreateTestUser();
        var creator = await CreateTestUser("creator@example.com");
        var tournament = await CreateTestTournament(
            creator.Id,
            status: "Postponed",
            name: "Postponed Tournament"
        );
        var team = await CreateTestTeam(tournament.Id);
        await AddUserToTeam(team.Id, user.Id);

        // Act
        var result = await _sut.GetUserTournamentsAsync(user.Id, user.Id, null);

        // Assert
        result.Should().NotBeNull();
        result.Active.Should().BeEmpty("postponed tournaments should not appear");
        result.Past.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserTournaments_CancelledTournament_NotIncluded()
    {
        // Arrange
        var user = await CreateTestUser();
        var creator = await CreateTestUser("creator@example.com");
        var tournament = await CreateTestTournament(
            creator.Id,
            status: "Cancelled",
            name: "Cancelled Tournament"
        );
        var team = await CreateTestTeam(tournament.Id);
        await AddUserToTeam(team.Id, user.Id);

        // Act
        var result = await _sut.GetUserTournamentsAsync(user.Id, user.Id, null);

        // Assert
        result.Should().NotBeNull();
        result.Active.Should().BeEmpty("cancelled tournaments should not appear");
        result.Past.Should().BeEmpty();
    }

    #endregion
}
