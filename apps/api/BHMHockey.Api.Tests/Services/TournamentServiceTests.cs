using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// Tests for TournamentService - TDD approach for TRN-001.
/// Tests written FIRST before implementation.
/// </summary>
public class TournamentServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly OrganizationAdminService _orgAdminService;
    private readonly TournamentService _sut;
    private readonly TournamentAuthorizationService _authService;

    public TournamentServiceTests()
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

    private CreateTournamentRequest CreateValidRequest(Guid? organizationId = null)
    {
        return new CreateTournamentRequest
        {
            OrganizationId = organizationId,
            Name = "Summer Tournament 2026",
            Description = "Annual summer hockey tournament",
            Format = "SingleElimination",
            TeamFormation = "OrganizerAssigned",
            StartDate = DateTime.UtcNow.AddDays(30),
            EndDate = DateTime.UtcNow.AddDays(32),
            RegistrationDeadline = DateTime.UtcNow.AddDays(25),
            MaxTeams = 8,
            MinPlayersPerTeam = 5,
            MaxPlayersPerTeam = 10,
            AllowMultiTeam = false,
            AllowSubstitutions = true,
            EntryFee = 50,
            FeeType = "PerPlayer",
            Venue = "Ice Arena"
        };
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_WithValidData_CreatesTournamentInDraftStatus()
    {
        // Arrange
        var user = await CreateTestUser();
        var org = await CreateTestOrganization(user.Id);
        var request = CreateValidRequest(org.Id);

        // Act
        var result = await _sut.CreateAsync(request, user.Id);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("Draft");
        result.Name.Should().Be("Summer Tournament 2026");
        result.Format.Should().Be("SingleElimination");
        result.TeamFormation.Should().Be("OrganizerAssigned");
    }

    [Fact]
    public async Task CreateAsync_WithValidData_SetsCreatorId()
    {
        // Arrange
        var user = await CreateTestUser();
        var org = await CreateTestOrganization(user.Id);
        var request = CreateValidRequest(org.Id);

        // Act
        var result = await _sut.CreateAsync(request, user.Id);

        // Assert
        result.CreatorId.Should().Be(user.Id);
    }

    [Fact]
    public async Task CreateAsync_WithValidData_CreatesOwnerAdmin()
    {
        // Arrange
        var user = await CreateTestUser();
        var org = await CreateTestOrganization(user.Id);
        var request = CreateValidRequest(org.Id);

        // Act
        var result = await _sut.CreateAsync(request, user.Id);

        // Assert
        var tournamentAdmin = await _context.TournamentAdmins
            .FirstOrDefaultAsync(ta => ta.TournamentId == result.Id && ta.UserId == user.Id);
        tournamentAdmin.Should().NotBeNull();
        tournamentAdmin!.Role.Should().Be("Owner");
    }

    [Fact]
    public async Task CreateAsync_WithoutOrganization_CreatesStandaloneTournament()
    {
        // Arrange
        var user = await CreateTestUser();
        var request = CreateValidRequest(organizationId: null);

        // Act
        var result = await _sut.CreateAsync(request, user.Id);

        // Assert
        result.Should().NotBeNull();
        result.OrganizationId.Should().BeNull();
        result.Status.Should().Be("Draft");
    }

    [Fact]
    public async Task CreateAsync_WithRoundRobinFormat_SetsRoundRobinDefaults()
    {
        // Arrange
        var user = await CreateTestUser();
        var request = CreateValidRequest();
        request.Format = "RoundRobin";
        request.PointsWin = 3;
        request.PointsTie = 1;
        request.PointsLoss = 0;

        // Act
        var result = await _sut.CreateAsync(request, user.Id);

        // Assert
        result.Format.Should().Be("RoundRobin");
        result.PointsWin.Should().Be(3);
        result.PointsTie.Should().Be(1);
        result.PointsLoss.Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_WithInvalidFormat_ThrowsException()
    {
        // Arrange
        var user = await CreateTestUser();
        var request = CreateValidRequest();
        request.Format = "InvalidFormat";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CreateAsync(request, user.Id));
    }

    [Fact]
    public async Task CreateAsync_WithInvalidTeamFormation_ThrowsException()
    {
        // Arrange
        var user = await CreateTestUser();
        var request = CreateValidRequest();
        request.TeamFormation = "InvalidFormation";

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CreateAsync(request, user.Id));
    }

    [Fact]
    public async Task CreateAsync_WithNonExistentOrganization_ThrowsException()
    {
        // Arrange
        var user = await CreateTestUser();
        var request = CreateValidRequest(Guid.NewGuid()); // Non-existent org

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CreateAsync(request, user.Id));
    }

    [Fact]
    public async Task CreateAsync_WithOrganization_RequiresOrgAdmin()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var nonAdmin = await CreateTestUser("nonadmin@example.com");
        var org = await CreateTestOrganization(creator.Id);
        var request = CreateValidRequest(org.Id);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.CreateAsync(request, nonAdmin.Id));
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyPublicTournaments()
    {
        // Arrange
        var user = await CreateTestUser();
        var org = await CreateTestOrganization(user.Id);

        // Create tournaments with different statuses
        await CreateTournamentWithStatus(user.Id, org.Id, "Draft", "Draft Tournament");
        await CreateTournamentWithStatus(user.Id, org.Id, "Open", "Open Tournament");
        await CreateTournamentWithStatus(user.Id, org.Id, "RegistrationClosed", "Reg Closed Tournament");
        await CreateTournamentWithStatus(user.Id, org.Id, "InProgress", "In Progress Tournament");
        await CreateTournamentWithStatus(user.Id, org.Id, "Completed", "Completed Tournament");
        await CreateTournamentWithStatus(user.Id, org.Id, "Postponed", "Postponed Tournament");
        await CreateTournamentWithStatus(user.Id, org.Id, "Cancelled", "Cancelled Tournament");

        // Act
        var result = await _sut.GetAllAsync();

        // Assert - Only Open, InProgress, Completed are "public"
        result.Should().HaveCount(3);
        result.Select(t => t.Status).Should().BeEquivalentTo(new[] { "Open", "InProgress", "Completed" });
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyListWhenNoPublicTournaments()
    {
        // Arrange - no tournaments created

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().BeEmpty();
    }

    private async Task<Tournament> CreateTournamentWithStatus(Guid creatorId, Guid? orgId, string status, string name)
    {
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            CreatorId = creatorId,
            Name = name,
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

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsTournament()
    {
        // Arrange
        var user = await CreateTestUser();
        var org = await CreateTestOrganization(user.Id);
        var tournament = await CreateTournamentWithStatus(user.Id, org.Id, "Open", "Test Tournament");

        // Act
        var result = await _sut.GetByIdAsync(tournament.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(tournament.Id);
        result.Name.Should().Be("Test Tournament");
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange - no tournament created

        // Act
        var result = await _sut.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_IncludesAllFields()
    {
        // Arrange
        var user = await CreateTestUser();
        var request = CreateValidRequest();
        request.Description = "Full description";
        request.Venue = "Ice Arena";
        request.RulesContent = "Tournament rules here";
        var created = await _sut.CreateAsync(request, user.Id);

        // Act
        var result = await _sut.GetByIdAsync(created.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Description.Should().Be("Full description");
        result.Venue.Should().Be("Ice Arena");
        result.RulesContent.Should().Be("Tournament rules here");
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_InDraftStatus_UpdatesSuccessfully()
    {
        // Arrange
        var user = await CreateTestUser();
        var request = CreateValidRequest();
        var tournament = await _sut.CreateAsync(request, user.Id);

        var updateRequest = new UpdateTournamentRequest
        {
            Name = "Updated Tournament Name",
            Description = "Updated description"
        };

        // Act
        var result = await _sut.UpdateAsync(tournament.Id, updateRequest, user.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated Tournament Name");
        result.Description.Should().Be("Updated description");
    }

    [Fact]
    public async Task UpdateAsync_InOpenStatus_UpdatesSuccessfully()
    {
        // Arrange
        var user = await CreateTestUser();
        var org = await CreateTestOrganization(user.Id);
        var tournament = await CreateTournamentWithStatus(user.Id, org.Id, "Open", "Original Name");

        var updateRequest = new UpdateTournamentRequest
        {
            Name = "Updated Name"
        };

        // Act
        var result = await _sut.UpdateAsync(tournament.Id, updateRequest, user.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task UpdateAsync_InRegistrationClosedStatus_ThrowsException()
    {
        // Arrange
        var user = await CreateTestUser();
        var org = await CreateTestOrganization(user.Id);
        var tournament = await CreateTournamentWithStatus(user.Id, org.Id, "RegistrationClosed", "Test");

        var updateRequest = new UpdateTournamentRequest { Name = "New Name" };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateAsync(tournament.Id, updateRequest, user.Id));
    }

    [Fact]
    public async Task UpdateAsync_InProgressStatus_ThrowsException()
    {
        // Arrange
        var user = await CreateTestUser();
        var org = await CreateTestOrganization(user.Id);
        var tournament = await CreateTournamentWithStatus(user.Id, org.Id, "InProgress", "Test");

        var updateRequest = new UpdateTournamentRequest { Name = "New Name" };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateAsync(tournament.Id, updateRequest, user.Id));
    }

    [Fact]
    public async Task UpdateAsync_InCompletedStatus_ThrowsException()
    {
        // Arrange
        var user = await CreateTestUser();
        var org = await CreateTestOrganization(user.Id);
        var tournament = await CreateTournamentWithStatus(user.Id, org.Id, "Completed", "Test");

        var updateRequest = new UpdateTournamentRequest { Name = "New Name" };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateAsync(tournament.Id, updateRequest, user.Id));
    }

    [Fact]
    public async Task UpdateAsync_ByNonAdmin_ThrowsUnauthorized()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var nonAdmin = await CreateTestUser("nonadmin@example.com");
        var request = CreateValidRequest();
        var tournament = await _sut.CreateAsync(request, creator.Id);

        var updateRequest = new UpdateTournamentRequest { Name = "New Name" };

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.UpdateAsync(tournament.Id, updateRequest, nonAdmin.Id));
    }

    [Fact]
    public async Task UpdateAsync_NonExistentTournament_ReturnsNull()
    {
        // Arrange
        var user = await CreateTestUser();
        var updateRequest = new UpdateTournamentRequest { Name = "New Name" };

        // Act
        var result = await _sut.UpdateAsync(Guid.NewGuid(), updateRequest, user.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_UpdatesTimestamp()
    {
        // Arrange
        var user = await CreateTestUser();
        var request = CreateValidRequest();
        var tournament = await _sut.CreateAsync(request, user.Id);
        var originalUpdatedAt = tournament.UpdatedAt;

        await Task.Delay(10); // Ensure time difference

        var updateRequest = new UpdateTournamentRequest { Name = "Updated Name" };

        // Act
        var result = await _sut.UpdateAsync(tournament.Id, updateRequest, user.Id);

        // Assert
        result!.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_InDraftStatus_DeletesTournament()
    {
        // Arrange
        var user = await CreateTestUser();
        var request = CreateValidRequest();
        var tournament = await _sut.CreateAsync(request, user.Id);

        // Act
        var result = await _sut.DeleteAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeTrue();
        var deleted = await _context.Tournaments.FindAsync(tournament.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_InDraftStatus_DeletesTournamentAdmins()
    {
        // Arrange
        var user = await CreateTestUser();
        var request = CreateValidRequest();
        var tournament = await _sut.CreateAsync(request, user.Id);

        // Act
        await _sut.DeleteAsync(tournament.Id, user.Id);

        // Assert
        var admins = await _context.TournamentAdmins
            .Where(ta => ta.TournamentId == tournament.Id)
            .ToListAsync();
        admins.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_InOpenStatus_ThrowsException()
    {
        // Arrange
        var user = await CreateTestUser();
        var org = await CreateTestOrganization(user.Id);
        var tournament = await CreateTournamentWithStatus(user.Id, org.Id, "Open", "Test");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.DeleteAsync(tournament.Id, user.Id));
    }

    [Fact]
    public async Task DeleteAsync_InProgressStatus_ThrowsException()
    {
        // Arrange
        var user = await CreateTestUser();
        var org = await CreateTestOrganization(user.Id);
        var tournament = await CreateTournamentWithStatus(user.Id, org.Id, "InProgress", "Test");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.DeleteAsync(tournament.Id, user.Id));
    }

    [Fact]
    public async Task DeleteAsync_ByNonAdmin_ThrowsUnauthorized()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var nonAdmin = await CreateTestUser("nonadmin@example.com");
        var request = CreateValidRequest();
        var tournament = await _sut.CreateAsync(request, creator.Id);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.DeleteAsync(tournament.Id, nonAdmin.Id));
    }

    [Fact]
    public async Task DeleteAsync_NonExistentTournament_ReturnsFalse()
    {
        // Arrange
        var user = await CreateTestUser();

        // Act
        var result = await _sut.DeleteAsync(Guid.NewGuid(), user.Id);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region CanUserManageTournamentAsync Tests

    [Fact]
    public async Task CanUserManageTournamentAsync_ForOwner_ReturnsTrue()
    {
        // Arrange
        var user = await CreateTestUser();
        var request = CreateValidRequest();
        var tournament = await _sut.CreateAsync(request, user.Id);

        // Act
        var result = await _sut.CanUserManageTournamentAsync(tournament.Id, user.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanUserManageTournamentAsync_ForNonAdmin_ReturnsFalse()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var nonAdmin = await CreateTestUser("nonadmin@example.com");
        var request = CreateValidRequest();
        var tournament = await _sut.CreateAsync(request, creator.Id);

        // Act
        var result = await _sut.CanUserManageTournamentAsync(tournament.Id, nonAdmin.Id);

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
