using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// Tests for TournamentTeamService - TDD approach for TRN-003.
/// Tests written FIRST before implementation.
/// </summary>
public class TournamentTeamServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly TournamentTeamService _sut;
    private readonly ITournamentService _tournamentService;
    private readonly TournamentAuthorizationService _authService;

    public TournamentTeamServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _authService = new TournamentAuthorizationService(_context);
        var orgAdminService = new OrganizationAdminService(_context);
        _tournamentService = new TournamentService(_context, orgAdminService, _authService);
        _sut = new TournamentTeamService(_context, _tournamentService, _authService);
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

    private CreateTournamentTeamRequest CreateValidTeamRequest(string name = "Team Alpha")
    {
        return new CreateTournamentTeamRequest
        {
            Name = name
        };
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_WithValidData_CreatesTeamInRegisteredStatus()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Open");
        var request = CreateValidTeamRequest();

        // Act
        var result = await _sut.CreateAsync(tournament.Id, request, user.Id);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be("Registered");
        result.Name.Should().Be("Team Alpha");
        result.TournamentId.Should().Be(tournament.Id);
    }

    [Fact]
    public async Task CreateAsync_WithValidData_SetsDefaultValues()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Open");
        var request = CreateValidTeamRequest();

        // Act
        var result = await _sut.CreateAsync(tournament.Id, request, user.Id);

        // Assert
        result.Wins.Should().Be(0);
        result.Losses.Should().Be(0);
        result.Ties.Should().Be(0);
        result.Points.Should().Be(0);
        result.GoalsFor.Should().Be(0);
        result.GoalsAgainst.Should().Be(0);
        result.HasBye.Should().BeFalse();
        result.Seed.Should().BeNull();
        result.CaptainUserId.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_WithSeed_SetsSeed()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Open");
        var request = new CreateTournamentTeamRequest
        {
            Name = "Team Alpha",
            Seed = 1
        };

        // Act
        var result = await _sut.CreateAsync(tournament.Id, request, user.Id);

        // Assert
        result.Seed.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_InDraftTournament_CreatesTeam()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        var request = CreateValidTeamRequest();

        // Act
        var result = await _sut.CreateAsync(tournament.Id, request, user.Id);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_InRegistrationClosedTournament_CreatesTeam()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "RegistrationClosed");
        var request = CreateValidTeamRequest();

        // Act
        var result = await _sut.CreateAsync(tournament.Id, request, user.Id);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAsync_InInProgressTournament_ThrowsException()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "InProgress");
        var request = CreateValidTeamRequest();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CreateAsync(tournament.Id, request, user.Id));
    }

    [Fact]
    public async Task CreateAsync_InCompletedTournament_ThrowsException()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Completed");
        var request = CreateValidTeamRequest();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CreateAsync(tournament.Id, request, user.Id));
    }

    [Fact]
    public async Task CreateAsync_ByNonAdmin_ThrowsUnauthorized()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var nonAdmin = await CreateTestUser("nonadmin@example.com");
        var tournament = await CreateTestTournament(creator.Id, "Open");
        var request = CreateValidTeamRequest();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.CreateAsync(tournament.Id, request, nonAdmin.Id));
    }

    [Fact]
    public async Task CreateAsync_NonExistentTournament_ThrowsException()
    {
        // Arrange
        var user = await CreateTestUser();
        var request = CreateValidTeamRequest();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CreateAsync(Guid.NewGuid(), request, user.Id));
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_ReturnsAllTeamsForTournament()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Open");
        await _sut.CreateAsync(tournament.Id, new CreateTournamentTeamRequest { Name = "Team A" }, user.Id);
        await _sut.CreateAsync(tournament.Id, new CreateTournamentTeamRequest { Name = "Team B" }, user.Id);
        await _sut.CreateAsync(tournament.Id, new CreateTournamentTeamRequest { Name = "Team C" }, user.Id);

        // Act
        var result = await _sut.GetAllAsync(tournament.Id);

        // Assert
        result.Should().HaveCount(3);
        result.Select(t => t.Name).Should().Contain(new[] { "Team A", "Team B", "Team C" });
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyListForTournamentWithNoTeams()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Open");

        // Act
        var result = await _sut.GetAllAsync(tournament.Id);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_DoesNotReturnTeamsFromOtherTournaments()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament1 = await CreateTestTournament(user.Id, "Open");
        var tournament2 = await CreateTestTournament(user.Id, "Open");
        await _sut.CreateAsync(tournament1.Id, new CreateTournamentTeamRequest { Name = "Team A" }, user.Id);
        await _sut.CreateAsync(tournament2.Id, new CreateTournamentTeamRequest { Name = "Team B" }, user.Id);

        // Act
        var result = await _sut.GetAllAsync(tournament1.Id);

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("Team A");
    }

    [Fact]
    public async Task GetAllAsync_OrdersBySeedException()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Open");
        await _sut.CreateAsync(tournament.Id, new CreateTournamentTeamRequest { Name = "Team C", Seed = 3 }, user.Id);
        await _sut.CreateAsync(tournament.Id, new CreateTournamentTeamRequest { Name = "Team A", Seed = 1 }, user.Id);
        await _sut.CreateAsync(tournament.Id, new CreateTournamentTeamRequest { Name = "Team B", Seed = 2 }, user.Id);

        // Act
        var result = await _sut.GetAllAsync(tournament.Id);

        // Assert
        result.Should().HaveCount(3);
        result[0].Name.Should().Be("Team A");
        result[1].Name.Should().Be("Team B");
        result[2].Name.Should().Be("Team C");
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsTeam()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Open");
        var created = await _sut.CreateAsync(tournament.Id, CreateValidTeamRequest(), user.Id);

        // Act
        var result = await _sut.GetByIdAsync(tournament.Id, created.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(created.Id);
        result.Name.Should().Be("Team Alpha");
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidTeamId_ReturnsNull()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Open");

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
        var tournament1 = await CreateTestTournament(user.Id, "Open");
        var tournament2 = await CreateTestTournament(user.Id, "Open");
        var team = await _sut.CreateAsync(tournament1.Id, CreateValidTeamRequest(), user.Id);

        // Act
        var result = await _sut.GetByIdAsync(tournament2.Id, team.Id);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_WithValidData_UpdatesTeamName()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Open");
        var team = await _sut.CreateAsync(tournament.Id, CreateValidTeamRequest(), user.Id);

        var updateRequest = new UpdateTournamentTeamRequest { Name = "New Team Name" };

        // Act
        var result = await _sut.UpdateAsync(tournament.Id, team.Id, updateRequest, user.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("New Team Name");
    }

    [Fact]
    public async Task UpdateAsync_WithValidData_UpdatesSeed()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Open");
        var team = await _sut.CreateAsync(tournament.Id, CreateValidTeamRequest(), user.Id);

        var updateRequest = new UpdateTournamentTeamRequest { Seed = 5 };

        // Act
        var result = await _sut.UpdateAsync(tournament.Id, team.Id, updateRequest, user.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Seed.Should().Be(5);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesTimestamp()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Open");
        var team = await _sut.CreateAsync(tournament.Id, CreateValidTeamRequest(), user.Id);
        var originalUpdatedAt = team.UpdatedAt;

        await Task.Delay(10);

        var updateRequest = new UpdateTournamentTeamRequest { Name = "Updated" };

        // Act
        var result = await _sut.UpdateAsync(tournament.Id, team.Id, updateRequest, user.Id);

        // Assert
        result!.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public async Task UpdateAsync_ByNonAdmin_ThrowsUnauthorized()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var nonAdmin = await CreateTestUser("nonadmin@example.com");
        var tournament = await CreateTestTournament(creator.Id, "Open");
        var team = await _sut.CreateAsync(tournament.Id, CreateValidTeamRequest(), creator.Id);

        var updateRequest = new UpdateTournamentTeamRequest { Name = "New Name" };

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.UpdateAsync(tournament.Id, team.Id, updateRequest, nonAdmin.Id));
    }

    [Fact]
    public async Task UpdateAsync_NonExistentTeam_ReturnsNull()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Open");
        var updateRequest = new UpdateTournamentTeamRequest { Name = "New Name" };

        // Act
        var result = await _sut.UpdateAsync(tournament.Id, Guid.NewGuid(), updateRequest, user.Id);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_InDraftTournament_DeletesTeam()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Draft");
        var team = await _sut.CreateAsync(tournament.Id, CreateValidTeamRequest(), user.Id);

        // Act
        var result = await _sut.DeleteAsync(tournament.Id, team.Id, user.Id);

        // Assert
        result.Should().BeTrue();
        var deleted = await _sut.GetByIdAsync(tournament.Id, team.Id);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_InOpenTournament_DeletesTeam()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Open");
        var team = await _sut.CreateAsync(tournament.Id, CreateValidTeamRequest(), user.Id);

        // Act
        var result = await _sut.DeleteAsync(tournament.Id, team.Id, user.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_InRegistrationClosedTournament_DeletesTeam()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "RegistrationClosed");
        var team = await _sut.CreateAsync(tournament.Id, CreateValidTeamRequest(), user.Id);

        // Act
        var result = await _sut.DeleteAsync(tournament.Id, team.Id, user.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_InInProgressTournament_ThrowsException()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Open");
        var team = await _sut.CreateAsync(tournament.Id, CreateValidTeamRequest(), user.Id);

        // Change tournament status to InProgress
        tournament.Status = "InProgress";
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.DeleteAsync(tournament.Id, team.Id, user.Id));
    }

    [Fact]
    public async Task DeleteAsync_InCompletedTournament_ThrowsException()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Open");
        var team = await _sut.CreateAsync(tournament.Id, CreateValidTeamRequest(), user.Id);

        // Change tournament status to Completed
        tournament.Status = "Completed";
        await _context.SaveChangesAsync();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.DeleteAsync(tournament.Id, team.Id, user.Id));
    }

    [Fact]
    public async Task DeleteAsync_ByNonAdmin_ThrowsUnauthorized()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var nonAdmin = await CreateTestUser("nonadmin@example.com");
        var tournament = await CreateTestTournament(creator.Id, "Open");
        var team = await _sut.CreateAsync(tournament.Id, CreateValidTeamRequest(), creator.Id);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.DeleteAsync(tournament.Id, team.Id, nonAdmin.Id));
    }

    [Fact]
    public async Task DeleteAsync_NonExistentTeam_ReturnsFalse()
    {
        // Arrange
        var user = await CreateTestUser();
        var tournament = await CreateTestTournament(user.Id, "Open");

        // Act
        var result = await _sut.DeleteAsync(tournament.Id, Guid.NewGuid(), user.Id);

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
