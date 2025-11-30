using BHMHockey.Api.Data;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// Tests for OrganizationAdminService - protecting multi-admin authorization logic.
/// These tests ensure:
/// - Admin status is correctly determined
/// - Only admins can add/remove other admins
/// - Cannot remove the last admin (business rule)
/// - Non-admins cannot access admin management features
/// </summary>
public class OrganizationAdminServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly OrganizationAdminService _sut;

    public OrganizationAdminServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _sut = new OrganizationAdminService(_context);
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
            CreatorId = creatorId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Organizations.Add(org);
        await _context.SaveChangesAsync();
        return org;
    }

    private async Task<OrganizationAdmin> AddAdmin(Guid orgId, Guid userId, Guid? addedByUserId = null)
    {
        var admin = new OrganizationAdmin
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            UserId = userId,
            AddedAt = DateTime.UtcNow,
            AddedByUserId = addedByUserId
        };

        _context.OrganizationAdmins.Add(admin);
        await _context.SaveChangesAsync();
        return admin;
    }

    #endregion

    #region IsUserAdminAsync Tests

    [Fact]
    public async Task IsUserAdminAsync_WhenUserIsAdmin_ReturnsTrue()
    {
        // Arrange
        var user = await CreateTestUser();
        var org = await CreateTestOrganization(user.Id);
        await AddAdmin(org.Id, user.Id);

        // Act
        var result = await _sut.IsUserAdminAsync(org.Id, user.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsUserAdminAsync_WhenUserIsNotAdmin_ReturnsFalse()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var otherUser = await CreateTestUser("other@example.com");
        var org = await CreateTestOrganization(creator.Id);
        await AddAdmin(org.Id, creator.Id); // Only creator is admin

        // Act
        var result = await _sut.IsUserAdminAsync(org.Id, otherUser.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsUserAdminAsync_WhenOrganizationDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var user = await CreateTestUser();
        var nonExistentOrgId = Guid.NewGuid();

        // Act
        var result = await _sut.IsUserAdminAsync(nonExistentOrgId, user.Id);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetAdminsAsync Tests

    [Fact]
    public async Task GetAdminsAsync_AsAdmin_ReturnsAdminList()
    {
        // Arrange
        var admin1 = await CreateTestUser("admin1@example.com");
        var admin2 = await CreateTestUser("admin2@example.com");
        var org = await CreateTestOrganization(admin1.Id);
        await AddAdmin(org.Id, admin1.Id);
        await AddAdmin(org.Id, admin2.Id, admin1.Id);

        // Act
        var result = await _sut.GetAdminsAsync(org.Id, admin1.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Select(a => a.Email).Should().Contain("admin1@example.com", "admin2@example.com");
    }

    [Fact]
    public async Task GetAdminsAsync_AsNonAdmin_ReturnsEmptyList()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var nonAdmin = await CreateTestUser("nonadmin@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await AddAdmin(org.Id, admin.Id);

        // Act
        var result = await _sut.GetAdminsAsync(org.Id, nonAdmin.Id);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAdminsAsync_ReturnsAddedByInformation()
    {
        // Arrange
        var originalAdmin = await CreateTestUser("original@example.com");
        var addedAdmin = await CreateTestUser("added@example.com");
        var org = await CreateTestOrganization(originalAdmin.Id);
        await AddAdmin(org.Id, originalAdmin.Id, null); // Original has no AddedBy
        await AddAdmin(org.Id, addedAdmin.Id, originalAdmin.Id); // Added by original

        // Act
        var result = await _sut.GetAdminsAsync(org.Id, originalAdmin.Id);

        // Assert
        var original = result.First(a => a.Email == "original@example.com");
        var added = result.First(a => a.Email == "added@example.com");

        original.AddedByUserId.Should().BeNull();
        original.AddedByName.Should().BeNull();

        added.AddedByUserId.Should().Be(originalAdmin.Id);
        added.AddedByName.Should().Be("Test User");
    }

    #endregion

    #region AddAdminAsync Tests

    [Fact]
    public async Task AddAdminAsync_AsAdmin_AddsNewAdmin()
    {
        // Arrange
        var existingAdmin = await CreateTestUser("admin@example.com");
        var newAdmin = await CreateTestUser("newadmin@example.com");
        var org = await CreateTestOrganization(existingAdmin.Id);
        await AddAdmin(org.Id, existingAdmin.Id);

        // Act
        var result = await _sut.AddAdminAsync(org.Id, newAdmin.Id, existingAdmin.Id);

        // Assert
        result.Should().BeTrue();

        var adminRecord = await _context.OrganizationAdmins
            .FirstOrDefaultAsync(a => a.OrganizationId == org.Id && a.UserId == newAdmin.Id);
        adminRecord.Should().NotBeNull();
        adminRecord!.AddedByUserId.Should().Be(existingAdmin.Id);
    }

    [Fact]
    public async Task AddAdminAsync_AsNonAdmin_ReturnsFalse()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var nonAdmin = await CreateTestUser("nonadmin@example.com");
        var targetUser = await CreateTestUser("target@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await AddAdmin(org.Id, admin.Id);

        // Act
        var result = await _sut.AddAdminAsync(org.Id, targetUser.Id, nonAdmin.Id);

        // Assert
        result.Should().BeFalse();

        // Verify no admin was added
        var adminCount = await _context.OrganizationAdmins.CountAsync(a => a.OrganizationId == org.Id);
        adminCount.Should().Be(1); // Only the original admin
    }

    [Fact]
    public async Task AddAdminAsync_UserAlreadyAdmin_ReturnsFalse()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await AddAdmin(org.Id, admin.Id);

        // Act - Try to add the same user again
        var result = await _sut.AddAdminAsync(org.Id, admin.Id, admin.Id);

        // Assert
        result.Should().BeFalse();

        // Verify no duplicate was created
        var adminCount = await _context.OrganizationAdmins
            .CountAsync(a => a.OrganizationId == org.Id && a.UserId == admin.Id);
        adminCount.Should().Be(1);
    }

    [Fact]
    public async Task AddAdminAsync_UserDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await AddAdmin(org.Id, admin.Id);
        var nonExistentUserId = Guid.NewGuid();

        // Act
        var result = await _sut.AddAdminAsync(org.Id, nonExistentUserId, admin.Id);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region RemoveAdminAsync Tests

    [Fact]
    public async Task RemoveAdminAsync_AsAdmin_RemovesOtherAdmin()
    {
        // Arrange
        var admin1 = await CreateTestUser("admin1@example.com");
        var admin2 = await CreateTestUser("admin2@example.com");
        var org = await CreateTestOrganization(admin1.Id);
        await AddAdmin(org.Id, admin1.Id);
        await AddAdmin(org.Id, admin2.Id, admin1.Id);

        // Act
        var result = await _sut.RemoveAdminAsync(org.Id, admin2.Id, admin1.Id);

        // Assert
        result.Should().BeTrue();

        var adminExists = await _context.OrganizationAdmins
            .AnyAsync(a => a.OrganizationId == org.Id && a.UserId == admin2.Id);
        adminExists.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveAdminAsync_AsNonAdmin_ReturnsFalse()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var nonAdmin = await CreateTestUser("nonadmin@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await AddAdmin(org.Id, admin.Id);

        // Act
        var result = await _sut.RemoveAdminAsync(org.Id, admin.Id, nonAdmin.Id);

        // Assert
        result.Should().BeFalse();

        // Verify admin was not removed
        var adminExists = await _context.OrganizationAdmins
            .AnyAsync(a => a.OrganizationId == org.Id && a.UserId == admin.Id);
        adminExists.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveAdminAsync_LastAdmin_ThrowsInvalidOperationException()
    {
        // Arrange
        var admin = await CreateTestUser("admin@example.com");
        var org = await CreateTestOrganization(admin.Id);
        await AddAdmin(org.Id, admin.Id);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RemoveAdminAsync(org.Id, admin.Id, admin.Id));

        exception.Message.Should().Contain("Cannot remove the last admin");

        // Verify admin still exists
        var adminExists = await _context.OrganizationAdmins
            .AnyAsync(a => a.OrganizationId == org.Id && a.UserId == admin.Id);
        adminExists.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveAdminAsync_AdminCanRemoveSelf_WhenOtherAdminsExist()
    {
        // Arrange
        var admin1 = await CreateTestUser("admin1@example.com");
        var admin2 = await CreateTestUser("admin2@example.com");
        var org = await CreateTestOrganization(admin1.Id);
        await AddAdmin(org.Id, admin1.Id);
        await AddAdmin(org.Id, admin2.Id, admin1.Id);

        // Act - admin1 removes themselves
        var result = await _sut.RemoveAdminAsync(org.Id, admin1.Id, admin1.Id);

        // Assert
        result.Should().BeTrue();

        var admin1Exists = await _context.OrganizationAdmins
            .AnyAsync(a => a.OrganizationId == org.Id && a.UserId == admin1.Id);
        admin1Exists.Should().BeFalse();

        // admin2 should still exist
        var admin2Exists = await _context.OrganizationAdmins
            .AnyAsync(a => a.OrganizationId == org.Id && a.UserId == admin2.Id);
        admin2Exists.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveAdminAsync_UserNotAdmin_ReturnsFalse()
    {
        // Arrange - Need two admins so "last admin" check doesn't trigger
        var admin1 = await CreateTestUser("admin1@example.com");
        var admin2 = await CreateTestUser("admin2@example.com");
        var nonAdmin = await CreateTestUser("nonadmin@example.com");
        var org = await CreateTestOrganization(admin1.Id);
        await AddAdmin(org.Id, admin1.Id);
        await AddAdmin(org.Id, admin2.Id, admin1.Id);

        // Act - Try to remove someone who isn't an admin
        var result = await _sut.RemoveAdminAsync(org.Id, nonAdmin.Id, admin1.Id);

        // Assert
        result.Should().BeFalse();
    }

    #endregion
}
