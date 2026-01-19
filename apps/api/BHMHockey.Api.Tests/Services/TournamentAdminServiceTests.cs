using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// Tests for TournamentAdminService - TDD approach for TRN-028.
/// Tests written FIRST before implementation.
/// Multi-admin tournament management with Owner, Admin, and Scorekeeper roles.
/// </summary>
public class TournamentAdminServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly TournamentAdminService _sut;
    private readonly ITournamentAuthorizationService _authService;

    public TournamentAdminServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _authService = new TournamentAuthorizationService(_context);
        _sut = new TournamentAdminService(_context, _authService);
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

    #endregion

    #region GetAdminsAsync Tests

    [Fact]
    public async Task GetAdminsAsync_ReturnsAllAdminsWithUserDetails()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com", "Owner", "User");
        var admin = await CreateTestUser("admin@example.com", "Admin", "User");
        var scorekeeper = await CreateTestUser("scorekeeper@example.com", "Score", "Keeper");
        var tournament = await CreateTestTournament(owner.Id);

        await AddTournamentAdmin(tournament.Id, admin.Id, "Admin", owner.Id);
        await AddTournamentAdmin(tournament.Id, scorekeeper.Id, "Scorekeeper", owner.Id);

        // Act
        var result = await _sut.GetAdminsAsync(tournament.Id);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(a => a.UserId == owner.Id && a.Role == "Owner");
        result.Should().Contain(a => a.UserId == admin.Id && a.Role == "Admin");
        result.Should().Contain(a => a.UserId == scorekeeper.Id && a.Role == "Scorekeeper");

        var ownerDto = result.First(a => a.UserId == owner.Id);
        ownerDto.UserFirstName.Should().Be("Owner");
        ownerDto.UserLastName.Should().Be("User");
        ownerDto.UserEmail.Should().Be("owner@example.com");
    }

    [Fact]
    public async Task GetAdminsAsync_ReturnsEmptyListForTournamentWithNoAdmins()
    {
        // Arrange
        var owner = await CreateTestUser();
        var tournament = new Tournament
        {
            Id = Guid.NewGuid(),
            CreatorId = owner.Id,
            Name = "Test Tournament",
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

        // Act
        var result = await _sut.GetAdminsAsync(tournament.Id);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAdminsAsync_IncludesAddedByNameForAdminsAddedByAnotherUser()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com", "Owner", "Boss");
        var admin = await CreateTestUser("admin@example.com", "Admin", "User");
        var tournament = await CreateTestTournament(owner.Id);

        await AddTournamentAdmin(tournament.Id, admin.Id, "Admin", owner.Id);

        // Act
        var result = await _sut.GetAdminsAsync(tournament.Id);

        // Assert
        var adminDto = result.First(a => a.UserId == admin.Id);
        adminDto.AddedByUserId.Should().Be(owner.Id);
        adminDto.AddedByName.Should().Be("Owner Boss");
    }

    [Fact]
    public async Task GetAdminsAsync_AddedByNameIsNullWhenAddedByUserIdIsNull()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var tournament = await CreateTestTournament(owner.Id);

        // Act
        var result = await _sut.GetAdminsAsync(tournament.Id);

        // Assert
        var ownerDto = result.First(a => a.UserId == owner.Id);
        ownerDto.AddedByUserId.Should().BeNull();
        ownerDto.AddedByName.Should().BeNull();
    }

    #endregion

    #region AddAdminAsync Tests

    [Fact]
    public async Task AddAdminAsync_OwnerCanAddAdminRole()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var newAdmin = await CreateTestUser("newadmin@example.com", "New", "Admin");
        var tournament = await CreateTestTournament(owner.Id);

        // Act
        var result = await _sut.AddAdminAsync(tournament.Id, newAdmin.Id, "Admin", owner.Id);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(newAdmin.Id);
        result.Role.Should().Be("Admin");
        result.UserFirstName.Should().Be("New");
        result.UserLastName.Should().Be("Admin");
        result.AddedByUserId.Should().Be(owner.Id);
    }

    [Fact]
    public async Task AddAdminAsync_OwnerCanAddScorekeeperRole()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var scorekeeper = await CreateTestUser("scorekeeper@example.com", "Score", "Keeper");
        var tournament = await CreateTestTournament(owner.Id);

        // Act
        var result = await _sut.AddAdminAsync(tournament.Id, scorekeeper.Id, "Scorekeeper", owner.Id);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(scorekeeper.Id);
        result.Role.Should().Be("Scorekeeper");
        result.AddedByUserId.Should().Be(owner.Id);
    }

    [Fact]
    public async Task AddAdminAsync_AdminCannotAddOtherAdmins()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var admin = await CreateTestUser("admin@example.com");
        var newUser = await CreateTestUser("newuser@example.com");
        var tournament = await CreateTestTournament(owner.Id);
        await AddTournamentAdmin(tournament.Id, admin.Id, "Admin", owner.Id);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.AddAdminAsync(tournament.Id, newUser.Id, "Admin", admin.Id));
    }

    [Fact]
    public async Task AddAdminAsync_ScorekeeperCannotAddAdmins()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var scorekeeper = await CreateTestUser("scorekeeper@example.com");
        var newUser = await CreateTestUser("newuser@example.com");
        var tournament = await CreateTestTournament(owner.Id);
        await AddTournamentAdmin(tournament.Id, scorekeeper.Id, "Scorekeeper", owner.Id);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.AddAdminAsync(tournament.Id, newUser.Id, "Admin", scorekeeper.Id));
    }

    [Fact]
    public async Task AddAdminAsync_CannotAddWithOwnerRole()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var newUser = await CreateTestUser("newuser@example.com");
        var tournament = await CreateTestTournament(owner.Id);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.AddAdminAsync(tournament.Id, newUser.Id, "Owner", owner.Id));
    }

    [Fact]
    public async Task AddAdminAsync_CannotAddUserWhoIsAlreadyAnAdmin()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var existingAdmin = await CreateTestUser("admin@example.com");
        var tournament = await CreateTestTournament(owner.Id);
        await AddTournamentAdmin(tournament.Id, existingAdmin.Id, "Admin", owner.Id);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.AddAdminAsync(tournament.Id, existingAdmin.Id, "Scorekeeper", owner.Id));
    }

    [Fact]
    public async Task AddAdminAsync_CannotAddNonExistentUser()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var tournament = await CreateTestTournament(owner.Id);
        var nonExistentUserId = Guid.NewGuid();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.AddAdminAsync(tournament.Id, nonExistentUserId, "Admin", owner.Id));
    }

    [Fact]
    public async Task AddAdminAsync_RecordsAddedByUserIdCorrectly()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var newAdmin = await CreateTestUser("newadmin@example.com");
        var tournament = await CreateTestTournament(owner.Id);

        // Act
        var result = await _sut.AddAdminAsync(tournament.Id, newAdmin.Id, "Admin", owner.Id);

        // Assert
        result.AddedByUserId.Should().Be(owner.Id);

        var dbAdmin = await _context.TournamentAdmins
            .FirstOrDefaultAsync(ta => ta.Id == result.Id);
        dbAdmin!.AddedByUserId.Should().Be(owner.Id);
    }

    #endregion

    #region UpdateRoleAsync Tests

    [Fact]
    public async Task UpdateRoleAsync_OwnerCanUpdateAdminToScorekeeper()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var admin = await CreateTestUser("admin@example.com");
        var tournament = await CreateTestTournament(owner.Id);
        await AddTournamentAdmin(tournament.Id, admin.Id, "Admin", owner.Id);

        // Act
        var result = await _sut.UpdateRoleAsync(tournament.Id, admin.Id, "Scorekeeper", owner.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Role.Should().Be("Scorekeeper");
        result.UserId.Should().Be(admin.Id);
    }

    [Fact]
    public async Task UpdateRoleAsync_OwnerCanUpdateScorekeeperToAdmin()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var scorekeeper = await CreateTestUser("scorekeeper@example.com");
        var tournament = await CreateTestTournament(owner.Id);
        await AddTournamentAdmin(tournament.Id, scorekeeper.Id, "Scorekeeper", owner.Id);

        // Act
        var result = await _sut.UpdateRoleAsync(tournament.Id, scorekeeper.Id, "Admin", owner.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Role.Should().Be("Admin");
        result.UserId.Should().Be(scorekeeper.Id);
    }

    [Fact]
    public async Task UpdateRoleAsync_CannotUpdateToOwnerRole()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var admin = await CreateTestUser("admin@example.com");
        var tournament = await CreateTestTournament(owner.Id);
        await AddTournamentAdmin(tournament.Id, admin.Id, "Admin", owner.Id);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateRoleAsync(tournament.Id, admin.Id, "Owner", owner.Id));
    }

    [Fact]
    public async Task UpdateRoleAsync_CannotUpdateOwnersRole()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var tournament = await CreateTestTournament(owner.Id);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateRoleAsync(tournament.Id, owner.Id, "Admin", owner.Id));
    }

    [Fact]
    public async Task UpdateRoleAsync_AdminCannotUpdateRoles()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var admin = await CreateTestUser("admin@example.com");
        var scorekeeper = await CreateTestUser("scorekeeper@example.com");
        var tournament = await CreateTestTournament(owner.Id);
        await AddTournamentAdmin(tournament.Id, admin.Id, "Admin", owner.Id);
        await AddTournamentAdmin(tournament.Id, scorekeeper.Id, "Scorekeeper", owner.Id);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.UpdateRoleAsync(tournament.Id, scorekeeper.Id, "Admin", admin.Id));
    }

    [Fact]
    public async Task UpdateRoleAsync_ReturnsNullForNonExistentAdmin()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var tournament = await CreateTestTournament(owner.Id);
        var nonExistentUserId = Guid.NewGuid();

        // Act
        var result = await _sut.UpdateRoleAsync(tournament.Id, nonExistentUserId, "Admin", owner.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateRoleAsync_ScorekeeperCannotUpdateRoles()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var admin = await CreateTestUser("admin@example.com");
        var scorekeeper = await CreateTestUser("scorekeeper@example.com");
        var tournament = await CreateTestTournament(owner.Id);
        await AddTournamentAdmin(tournament.Id, admin.Id, "Admin", owner.Id);
        await AddTournamentAdmin(tournament.Id, scorekeeper.Id, "Scorekeeper", owner.Id);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.UpdateRoleAsync(tournament.Id, admin.Id, "Scorekeeper", scorekeeper.Id));
    }

    #endregion

    #region RemoveAdminAsync Tests

    [Fact]
    public async Task RemoveAdminAsync_OwnerCanRemoveAdmin()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var admin = await CreateTestUser("admin@example.com");
        var tournament = await CreateTestTournament(owner.Id);
        await AddTournamentAdmin(tournament.Id, admin.Id, "Admin", owner.Id);

        // Act
        var result = await _sut.RemoveAdminAsync(tournament.Id, admin.Id, owner.Id);

        // Assert
        result.Should().BeTrue();
        var removedAdmin = await _context.TournamentAdmins
            .Where(ta => ta.TournamentId == tournament.Id && ta.UserId == admin.Id && ta.RemovedAt == null)
            .FirstOrDefaultAsync();
        removedAdmin.Should().BeNull();
    }

    [Fact]
    public async Task RemoveAdminAsync_OwnerCanRemoveScorekeeper()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var scorekeeper = await CreateTestUser("scorekeeper@example.com");
        var tournament = await CreateTestTournament(owner.Id);
        await AddTournamentAdmin(tournament.Id, scorekeeper.Id, "Scorekeeper", owner.Id);

        // Act
        var result = await _sut.RemoveAdminAsync(tournament.Id, scorekeeper.Id, owner.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveAdminAsync_CannotRemoveOwner()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var tournament = await CreateTestTournament(owner.Id);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RemoveAdminAsync(tournament.Id, owner.Id, owner.Id));
    }

    [Fact]
    public async Task RemoveAdminAsync_CannotRemoveLastAdmin()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var tournament = await CreateTestTournament(owner.Id);

        // Act & Assert - Owner is the only admin
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RemoveAdminAsync(tournament.Id, owner.Id, owner.Id));
    }

    [Fact]
    public async Task RemoveAdminAsync_AdminCannotRemoveAdmins()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var admin1 = await CreateTestUser("admin1@example.com");
        var admin2 = await CreateTestUser("admin2@example.com");
        var tournament = await CreateTestTournament(owner.Id);
        await AddTournamentAdmin(tournament.Id, admin1.Id, "Admin", owner.Id);
        await AddTournamentAdmin(tournament.Id, admin2.Id, "Admin", owner.Id);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.RemoveAdminAsync(tournament.Id, admin2.Id, admin1.Id));
    }

    [Fact]
    public async Task RemoveAdminAsync_ReturnsFalseForNonExistentAdmin()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var tournament = await CreateTestTournament(owner.Id);
        var nonExistentUserId = Guid.NewGuid();

        // Act
        var result = await _sut.RemoveAdminAsync(tournament.Id, nonExistentUserId, owner.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveAdminAsync_ScorekeeperCannotRemoveAdmins()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var admin = await CreateTestUser("admin@example.com");
        var scorekeeper = await CreateTestUser("scorekeeper@example.com");
        var tournament = await CreateTestTournament(owner.Id);
        await AddTournamentAdmin(tournament.Id, admin.Id, "Admin", owner.Id);
        await AddTournamentAdmin(tournament.Id, scorekeeper.Id, "Scorekeeper", owner.Id);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.RemoveAdminAsync(tournament.Id, admin.Id, scorekeeper.Id));
    }

    #endregion

    #region TransferOwnershipAsync Tests

    [Fact]
    public async Task TransferOwnershipAsync_OwnerCanTransferToExistingAdmin()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var admin = await CreateTestUser("admin@example.com");
        var tournament = await CreateTestTournament(owner.Id);
        await AddTournamentAdmin(tournament.Id, admin.Id, "Admin", owner.Id);

        // Act
        var result = await _sut.TransferOwnershipAsync(tournament.Id, admin.Id, owner.Id);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(admin.Id);
        result.Role.Should().Be("Owner");
    }

    [Fact]
    public async Task TransferOwnershipAsync_OwnerCanTransferToExistingScorekeeper()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var scorekeeper = await CreateTestUser("scorekeeper@example.com");
        var tournament = await CreateTestTournament(owner.Id);
        await AddTournamentAdmin(tournament.Id, scorekeeper.Id, "Scorekeeper", owner.Id);

        // Act
        var result = await _sut.TransferOwnershipAsync(tournament.Id, scorekeeper.Id, owner.Id);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(scorekeeper.Id);
        result.Role.Should().Be("Owner");
    }

    [Fact]
    public async Task TransferOwnershipAsync_OldOwnerBecomesAdminAfterTransfer()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var admin = await CreateTestUser("admin@example.com");
        var tournament = await CreateTestTournament(owner.Id);
        await AddTournamentAdmin(tournament.Id, admin.Id, "Admin", owner.Id);

        // Act
        await _sut.TransferOwnershipAsync(tournament.Id, admin.Id, owner.Id);

        // Assert
        var oldOwner = await _context.TournamentAdmins
            .FirstOrDefaultAsync(ta => ta.TournamentId == tournament.Id && ta.UserId == owner.Id && ta.RemovedAt == null);
        oldOwner.Should().NotBeNull();
        oldOwner!.Role.Should().Be("Admin");
    }

    [Fact]
    public async Task TransferOwnershipAsync_NewOwnerBecomesOwnerAfterTransfer()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var admin = await CreateTestUser("admin@example.com");
        var tournament = await CreateTestTournament(owner.Id);
        await AddTournamentAdmin(tournament.Id, admin.Id, "Admin", owner.Id);

        // Act
        await _sut.TransferOwnershipAsync(tournament.Id, admin.Id, owner.Id);

        // Assert
        var newOwner = await _context.TournamentAdmins
            .FirstOrDefaultAsync(ta => ta.TournamentId == tournament.Id && ta.UserId == admin.Id && ta.RemovedAt == null);
        newOwner.Should().NotBeNull();
        newOwner!.Role.Should().Be("Owner");
    }

    [Fact]
    public async Task TransferOwnershipAsync_CannotTransferToSelf()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var tournament = await CreateTestTournament(owner.Id);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.TransferOwnershipAsync(tournament.Id, owner.Id, owner.Id));
    }

    [Fact]
    public async Task TransferOwnershipAsync_CannotTransferToNonAdmin()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var nonAdmin = await CreateTestUser("nonadmin@example.com");
        var tournament = await CreateTestTournament(owner.Id);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.TransferOwnershipAsync(tournament.Id, nonAdmin.Id, owner.Id));
    }

    [Fact]
    public async Task TransferOwnershipAsync_NonOwnerCannotTransfer()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var admin1 = await CreateTestUser("admin1@example.com");
        var admin2 = await CreateTestUser("admin2@example.com");
        var tournament = await CreateTestTournament(owner.Id);
        await AddTournamentAdmin(tournament.Id, admin1.Id, "Admin", owner.Id);
        await AddTournamentAdmin(tournament.Id, admin2.Id, "Admin", owner.Id);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.TransferOwnershipAsync(tournament.Id, admin2.Id, admin1.Id));
    }

    [Fact]
    public async Task TransferOwnershipAsync_ScorekeeperCannotTransfer()
    {
        // Arrange
        var owner = await CreateTestUser("owner@example.com");
        var admin = await CreateTestUser("admin@example.com");
        var scorekeeper = await CreateTestUser("scorekeeper@example.com");
        var tournament = await CreateTestTournament(owner.Id);
        await AddTournamentAdmin(tournament.Id, admin.Id, "Admin", owner.Id);
        await AddTournamentAdmin(tournament.Id, scorekeeper.Id, "Scorekeeper", owner.Id);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.TransferOwnershipAsync(tournament.Id, admin.Id, scorekeeper.Id));
    }

    #endregion
}
