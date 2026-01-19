using BHMHockey.Api.Data;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// TDD tests for TournamentAuthorizationService (TRN-028).
/// Tests role hierarchy, organization admin fallback, and all permission checks.
/// </summary>
public class TournamentAuthorizationServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly TournamentAuthorizationService _sut;

    public TournamentAuthorizationServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _sut = new TournamentAuthorizationService(_context);
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
            FirstName = "Test",
            LastName = "User",
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
        await _context.SaveChangesAsync();
        return org;
    }

    private async Task<Tournament> CreateTestTournament(Guid creatorId, Guid? organizationId = null, string name = "Test Tournament")
    {
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            CreatorId = creatorId,
            Name = name,
            Format = "SingleElimination",
            TeamFormation = "OrganizerAssigned",
            Status = "Draft",
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

    private async Task AddTournamentAdmin(Guid tournamentId, Guid userId, string role)
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

    private async Task AddOrganizationAdmin(Guid organizationId, Guid userId)
    {
        var admin = new OrganizationAdmin
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            UserId = userId,
            AddedAt = DateTime.UtcNow
        };

        _context.OrganizationAdmins.Add(admin);
        await _context.SaveChangesAsync();
    }

    #endregion

    #region Role Hierarchy Tests - IsOwnerAsync

    [Fact]
    public async Task IsOwnerAsync_WithOwnerRole_ReturnsTrue()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Owner");

        // Act
        var result = await _sut.IsOwnerAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsOwnerAsync_WithAdminRole_ReturnsFalse()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Admin");

        // Act
        var result = await _sut.IsOwnerAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsOwnerAsync_WithScorekeeperRole_ReturnsFalse()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Scorekeeper");

        // Act
        var result = await _sut.IsOwnerAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsOwnerAsync_WithNoRole_ReturnsFalse()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var nonAdmin = await CreateTestUser("nonadmin@example.com");
        var tournament = await CreateTestTournament(creator.Id);
        await AddTournamentAdmin(tournament.Id, creator.Id, "Owner");

        // Act
        var result = await _sut.IsOwnerAsync(tournament.Id, nonAdmin.Id);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Role Hierarchy Tests - IsAdminAsync

    [Fact]
    public async Task IsAdminAsync_WithOwnerRole_ReturnsTrue()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Owner");

        // Act
        var result = await _sut.IsAdminAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAdminAsync_WithAdminRole_ReturnsTrue()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Admin");

        // Act
        var result = await _sut.IsAdminAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsAdminAsync_WithScorekeeperRole_ReturnsFalse()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Scorekeeper");

        // Act
        var result = await _sut.IsAdminAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAdminAsync_WithNoRole_ReturnsFalse()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var nonAdmin = await CreateTestUser("nonadmin@example.com");
        var tournament = await CreateTestTournament(creator.Id);
        await AddTournamentAdmin(tournament.Id, creator.Id, "Owner");

        // Act
        var result = await _sut.IsAdminAsync(tournament.Id, nonAdmin.Id);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Role Hierarchy Tests - IsScorekeeperAsync

    [Fact]
    public async Task IsScorekeeperAsync_WithOwnerRole_ReturnsTrue()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Owner");

        // Act
        var result = await _sut.IsScorekeeperAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsScorekeeperAsync_WithAdminRole_ReturnsTrue()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Admin");

        // Act
        var result = await _sut.IsScorekeeperAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsScorekeeperAsync_WithScorekeeperRole_ReturnsTrue()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Scorekeeper");

        // Act
        var result = await _sut.IsScorekeeperAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsScorekeeperAsync_WithNoRole_ReturnsFalse()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var nonAdmin = await CreateTestUser("nonadmin@example.com");
        var tournament = await CreateTestTournament(creator.Id);
        await AddTournamentAdmin(tournament.Id, creator.Id, "Owner");

        // Act
        var result = await _sut.IsScorekeeperAsync(tournament.Id, nonAdmin.Id);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Organization Admin Fallback Tests

    [Fact]
    public async Task IsOwnerAsync_OrgAdminOnOrgTournament_ReturnsTrue()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var orgAdmin = await CreateTestUser("orgadmin@example.com");
        var org = await CreateTestOrganization(creator.Id);
        await AddOrganizationAdmin(org.Id, orgAdmin.Id);
        var tournament = await CreateTestTournament(creator.Id, org.Id);
        await AddTournamentAdmin(tournament.Id, creator.Id, "Owner");

        // Act
        var result = await _sut.IsOwnerAsync(tournament.Id, orgAdmin.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsOwnerAsync_OrgAdminOnStandaloneTournament_ReturnsFalse()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var orgAdmin = await CreateTestUser("orgadmin@example.com");
        var org = await CreateTestOrganization(creator.Id);
        await AddOrganizationAdmin(org.Id, orgAdmin.Id);
        var tournament = await CreateTestTournament(creator.Id, organizationId: null); // Standalone
        await AddTournamentAdmin(tournament.Id, creator.Id, "Owner");

        // Act
        var result = await _sut.IsOwnerAsync(tournament.Id, orgAdmin.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsOwnerAsync_NonOrgAdminOnOrgTournament_ReturnsFalse()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var nonOrgAdmin = await CreateTestUser("nonorgadmin@example.com");
        var org = await CreateTestOrganization(creator.Id);
        var tournament = await CreateTestTournament(creator.Id, org.Id);
        await AddTournamentAdmin(tournament.Id, creator.Id, "Owner");

        // Act
        var result = await _sut.IsOwnerAsync(tournament.Id, nonOrgAdmin.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsAdminAsync_OrgAdminOnOrgTournament_ReturnsTrue()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var orgAdmin = await CreateTestUser("orgadmin@example.com");
        var org = await CreateTestOrganization(creator.Id);
        await AddOrganizationAdmin(org.Id, orgAdmin.Id);
        var tournament = await CreateTestTournament(creator.Id, org.Id);
        await AddTournamentAdmin(tournament.Id, creator.Id, "Owner");

        // Act
        var result = await _sut.IsAdminAsync(tournament.Id, orgAdmin.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsScorekeeperAsync_OrgAdminOnOrgTournament_ReturnsTrue()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var orgAdmin = await CreateTestUser("orgadmin@example.com");
        var org = await CreateTestOrganization(creator.Id);
        await AddOrganizationAdmin(org.Id, orgAdmin.Id);
        var tournament = await CreateTestTournament(creator.Id, org.Id);
        await AddTournamentAdmin(tournament.Id, creator.Id, "Owner");

        // Act
        var result = await _sut.IsScorekeeperAsync(tournament.Id, orgAdmin.Id);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region GetRoleAsync Tests

    [Fact]
    public async Task GetRoleAsync_WithOwnerRole_ReturnsOwner()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Owner");

        // Act
        var result = await _sut.GetRoleAsync(tournament.Id, user.Id);

        // Assert
        result.Should().Be("Owner");
    }

    [Fact]
    public async Task GetRoleAsync_WithAdminRole_ReturnsAdmin()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Admin");

        // Act
        var result = await _sut.GetRoleAsync(tournament.Id, user.Id);

        // Assert
        result.Should().Be("Admin");
    }

    [Fact]
    public async Task GetRoleAsync_WithScorekeeperRole_ReturnsScorekeeper()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Scorekeeper");

        // Act
        var result = await _sut.GetRoleAsync(tournament.Id, user.Id);

        // Assert
        result.Should().Be("Scorekeeper");
    }

    [Fact]
    public async Task GetRoleAsync_WithNoRole_ReturnsNull()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var nonAdmin = await CreateTestUser("nonadmin@example.com");
        var tournament = await CreateTestTournament(creator.Id);
        await AddTournamentAdmin(tournament.Id, creator.Id, "Owner");

        // Act
        var result = await _sut.GetRoleAsync(tournament.Id, nonAdmin.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRoleAsync_OrgAdminOnOrgTournament_ReturnsOwner()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var orgAdmin = await CreateTestUser("orgadmin@example.com");
        var org = await CreateTestOrganization(creator.Id);
        await AddOrganizationAdmin(org.Id, orgAdmin.Id);
        var tournament = await CreateTestTournament(creator.Id, org.Id);
        await AddTournamentAdmin(tournament.Id, creator.Id, "Owner");

        // Act
        var result = await _sut.GetRoleAsync(tournament.Id, orgAdmin.Id);

        // Assert
        result.Should().Be("Owner");
    }

    [Fact]
    public async Task GetRoleAsync_OrgAdminOnStandaloneTournament_ReturnsNull()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var orgAdmin = await CreateTestUser("orgadmin@example.com");
        var org = await CreateTestOrganization(creator.Id);
        await AddOrganizationAdmin(org.Id, orgAdmin.Id);
        var tournament = await CreateTestTournament(creator.Id, organizationId: null); // Standalone
        await AddTournamentAdmin(tournament.Id, creator.Id, "Owner");

        // Act
        var result = await _sut.GetRoleAsync(tournament.Id, orgAdmin.Id);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Permission Check Tests - CanManageAdminsAsync

    [Fact]
    public async Task CanManageAdminsAsync_WithOwnerRole_ReturnsTrue()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Owner");

        // Act
        var result = await _sut.CanManageAdminsAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanManageAdminsAsync_WithAdminRole_ReturnsFalse()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Admin");

        // Act
        var result = await _sut.CanManageAdminsAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanManageAdminsAsync_WithScorekeeperRole_ReturnsFalse()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Scorekeeper");

        // Act
        var result = await _sut.CanManageAdminsAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanManageAdminsAsync_OrgAdminOnOrgTournament_ReturnsTrue()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var orgAdmin = await CreateTestUser("orgadmin@example.com");
        var org = await CreateTestOrganization(creator.Id);
        await AddOrganizationAdmin(org.Id, orgAdmin.Id);
        var tournament = await CreateTestTournament(creator.Id, org.Id);
        await AddTournamentAdmin(tournament.Id, creator.Id, "Owner");

        // Act
        var result = await _sut.CanManageAdminsAsync(tournament.Id, orgAdmin.Id);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Permission Check Tests - CanManageTeamsAsync

    [Fact]
    public async Task CanManageTeamsAsync_WithOwnerRole_ReturnsTrue()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Owner");

        // Act
        var result = await _sut.CanManageTeamsAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanManageTeamsAsync_WithAdminRole_ReturnsTrue()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Admin");

        // Act
        var result = await _sut.CanManageTeamsAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanManageTeamsAsync_WithScorekeeperRole_ReturnsFalse()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Scorekeeper");

        // Act
        var result = await _sut.CanManageTeamsAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Permission Check Tests - CanManageRegistrationsAsync

    [Fact]
    public async Task CanManageRegistrationsAsync_WithOwnerRole_ReturnsTrue()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Owner");

        // Act
        var result = await _sut.CanManageRegistrationsAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanManageRegistrationsAsync_WithAdminRole_ReturnsTrue()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Admin");

        // Act
        var result = await _sut.CanManageRegistrationsAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanManageRegistrationsAsync_WithScorekeeperRole_ReturnsFalse()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Scorekeeper");

        // Act
        var result = await _sut.CanManageRegistrationsAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Permission Check Tests - CanManageScheduleAsync

    [Fact]
    public async Task CanManageScheduleAsync_WithOwnerRole_ReturnsTrue()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Owner");

        // Act
        var result = await _sut.CanManageScheduleAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanManageScheduleAsync_WithAdminRole_ReturnsTrue()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Admin");

        // Act
        var result = await _sut.CanManageScheduleAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanManageScheduleAsync_WithScorekeeperRole_ReturnsFalse()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Scorekeeper");

        // Act
        var result = await _sut.CanManageScheduleAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Permission Check Tests - CanEnterScoresAsync

    [Fact]
    public async Task CanEnterScoresAsync_WithOwnerRole_ReturnsTrue()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Owner");

        // Act
        var result = await _sut.CanEnterScoresAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanEnterScoresAsync_WithAdminRole_ReturnsTrue()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Admin");

        // Act
        var result = await _sut.CanEnterScoresAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanEnterScoresAsync_WithScorekeeperRole_ReturnsTrue()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Scorekeeper");

        // Act
        var result = await _sut.CanEnterScoresAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanEnterScoresAsync_WithNoRole_ReturnsFalse()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var nonAdmin = await CreateTestUser("nonadmin@example.com");
        var tournament = await CreateTestTournament(creator.Id);
        await AddTournamentAdmin(tournament.Id, creator.Id, "Owner");

        // Act
        var result = await _sut.CanEnterScoresAsync(tournament.Id, nonAdmin.Id);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Permission Check Tests - CanDeleteTournamentAsync

    [Fact]
    public async Task CanDeleteTournamentAsync_WithOwnerRole_ReturnsTrue()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Owner");

        // Act
        var result = await _sut.CanDeleteTournamentAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanDeleteTournamentAsync_WithAdminRole_ReturnsFalse()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Admin");

        // Act
        var result = await _sut.CanDeleteTournamentAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanDeleteTournamentAsync_WithScorekeeperRole_ReturnsFalse()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Scorekeeper");

        // Act
        var result = await _sut.CanDeleteTournamentAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanDeleteTournamentAsync_OrgAdminOnOrgTournament_ReturnsTrue()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var orgAdmin = await CreateTestUser("orgadmin@example.com");
        var org = await CreateTestOrganization(creator.Id);
        await AddOrganizationAdmin(org.Id, orgAdmin.Id);
        var tournament = await CreateTestTournament(creator.Id, org.Id);
        await AddTournamentAdmin(tournament.Id, creator.Id, "Owner");

        // Act
        var result = await _sut.CanDeleteTournamentAsync(tournament.Id, orgAdmin.Id);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Permission Check Tests - CanTransferOwnershipAsync

    [Fact]
    public async Task CanTransferOwnershipAsync_WithOwnerRole_ReturnsTrue()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Owner");

        // Act
        var result = await _sut.CanTransferOwnershipAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanTransferOwnershipAsync_WithAdminRole_ReturnsFalse()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Admin");

        // Act
        var result = await _sut.CanTransferOwnershipAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanTransferOwnershipAsync_WithScorekeeperRole_ReturnsFalse()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        await AddTournamentAdmin(tournament.Id, user.Id, "Scorekeeper");

        // Act
        var result = await _sut.CanTransferOwnershipAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanTransferOwnershipAsync_OrgAdminOnOrgTournament_ReturnsTrue()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var orgAdmin = await CreateTestUser("orgadmin@example.com");
        var org = await CreateTestOrganization(creator.Id);
        await AddOrganizationAdmin(org.Id, orgAdmin.Id);
        var tournament = await CreateTestTournament(creator.Id, org.Id);
        await AddTournamentAdmin(tournament.Id, creator.Id, "Owner");

        // Act
        var result = await _sut.CanTransferOwnershipAsync(tournament.Id, orgAdmin.Id);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public async Task IsOwnerAsync_NonExistentTournament_ReturnsFalse()
    {
        // Arrange
        var user = await CreateTestUser();

        // Act
        var result = await _sut.IsOwnerAsync(Guid.NewGuid(), user.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetRoleAsync_NonExistentTournament_ReturnsNull()
    {
        // Arrange
        var user = await CreateTestUser();

        // Act
        var result = await _sut.GetRoleAsync(Guid.NewGuid(), user.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task CanEnterScoresAsync_OrgAdminOnOrgTournament_ReturnsTrue()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var orgAdmin = await CreateTestUser("orgadmin@example.com");
        var org = await CreateTestOrganization(creator.Id);
        await AddOrganizationAdmin(org.Id, orgAdmin.Id);
        var tournament = await CreateTestTournament(creator.Id, org.Id);
        await AddTournamentAdmin(tournament.Id, creator.Id, "Owner");

        // Act
        var result = await _sut.CanEnterScoresAsync(tournament.Id, orgAdmin.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsOwnerAsync_UserWithRemovedAtSet_ReturnsFalse()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id);
        var admin = new TournamentAdmin
        {
            Id = Guid.NewGuid(),
            TournamentId = tournament.Id,
            UserId = user.Id,
            Role = "Owner",
            AddedAt = DateTime.UtcNow,
            RemovedAt = DateTime.UtcNow.AddDays(-1) // Soft deleted
        };
        _context.TournamentAdmins.Add(admin);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.IsOwnerAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
