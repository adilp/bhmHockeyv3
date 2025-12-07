using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// Tests for OrganizationService - protecting organization and subscription business logic.
/// These tests ensure subscriptions can't be duplicated, only creators can modify orgs,
/// and soft-deleted orgs are properly excluded from queries.
/// </summary>
public class OrganizationServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly OrganizationAdminService _adminService;
    private readonly OrganizationService _sut;

    public OrganizationServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _adminService = new OrganizationAdminService(_context);
        _sut = new OrganizationService(_context, _adminService);
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

    private async Task<Organization> CreateTestOrganization(
        Guid creatorId,
        string name = "Test Org",
        string? description = "Test Description",
        string? location = "Boston",
        List<string>? skillLevels = null,
        bool isActive = true)
    {
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Location = location,
            SkillLevels = skillLevels ?? new List<string> { "Gold" },
            CreatorId = creatorId,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.Organizations.Add(org);

        // Add creator as admin (multi-admin support)
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

    private async Task<OrganizationSubscription> CreateSubscription(Guid orgId, Guid userId)
    {
        var subscription = new OrganizationSubscription
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            UserId = userId,
            NotificationEnabled = true,
            SubscribedAt = DateTime.UtcNow
        };

        _context.OrganizationSubscriptions.Add(subscription);
        await _context.SaveChangesAsync();
        return subscription;
    }

    #endregion

    #region Subscribe Tests

    [Fact]
    public async Task SubscribeAsync_WhenNotSubscribed_ReturnsTrue()
    {
        // Arrange
        var user = await CreateTestUser();
        var org = await CreateTestOrganization(user.Id);

        // Act
        var result = await _sut.SubscribeAsync(org.Id, user.Id);

        // Assert
        result.Should().BeTrue();
        var subscription = await _context.OrganizationSubscriptions
            .FirstOrDefaultAsync(s => s.OrganizationId == org.Id && s.UserId == user.Id);
        subscription.Should().NotBeNull();
    }

    [Fact]
    public async Task SubscribeAsync_WhenAlreadySubscribed_ReturnsFalse()
    {
        // Arrange
        var user = await CreateTestUser();
        var org = await CreateTestOrganization(user.Id);
        await CreateSubscription(org.Id, user.Id);

        // Act
        var result = await _sut.SubscribeAsync(org.Id, user.Id);

        // Assert
        result.Should().BeFalse();

        // Verify no duplicate was created
        var subscriptionCount = await _context.OrganizationSubscriptions
            .CountAsync(s => s.OrganizationId == org.Id && s.UserId == user.Id);
        subscriptionCount.Should().Be(1);
    }

    #endregion

    #region Unsubscribe Tests

    [Fact]
    public async Task UnsubscribeAsync_WhenSubscribed_ReturnsTrueAndRemovesSubscription()
    {
        // Arrange
        var user = await CreateTestUser();
        var org = await CreateTestOrganization(user.Id);
        await CreateSubscription(org.Id, user.Id);

        // Act
        var result = await _sut.UnsubscribeAsync(org.Id, user.Id);

        // Assert
        result.Should().BeTrue();
        var subscription = await _context.OrganizationSubscriptions
            .FirstOrDefaultAsync(s => s.OrganizationId == org.Id && s.UserId == user.Id);
        subscription.Should().BeNull();
    }

    [Fact]
    public async Task UnsubscribeAsync_WhenNotSubscribed_ReturnsFalse()
    {
        // Arrange
        var user = await CreateTestUser();
        var org = await CreateTestOrganization(user.Id);

        // Act
        var result = await _sut.UnsubscribeAsync(org.Id, user.Id);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetAll Tests

    [Fact]
    public async Task GetAllAsync_ExcludesInactiveOrganizations()
    {
        // Arrange
        var creator = await CreateTestUser();
        var activeOrg = await CreateTestOrganization(creator.Id, "Active Org", isActive: true);
        var inactiveOrg = await CreateTestOrganization(creator.Id, "Inactive Org", isActive: false);

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("Active Org");
    }

    [Fact]
    public async Task GetAllAsync_WithUserId_IncludesSubscriptionStatus()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var subscriber = await CreateTestUser("subscriber@example.com");
        var org1 = await CreateTestOrganization(creator.Id, "Subscribed Org");
        var org2 = await CreateTestOrganization(creator.Id, "Not Subscribed Org");
        await CreateSubscription(org1.Id, subscriber.Id);

        // Act
        var result = await _sut.GetAllAsync(subscriber.Id);

        // Assert
        result.Should().HaveCount(2);
        var subscribedOrg = result.First(o => o.Name == "Subscribed Org");
        var notSubscribedOrg = result.First(o => o.Name == "Not Subscribed Org");
        subscribedOrg.IsSubscribed.Should().BeTrue();
        notSubscribedOrg.IsSubscribed.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsCorrectSubscriberCount()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var sub1 = await CreateTestUser("sub1@example.com");
        var sub2 = await CreateTestUser("sub2@example.com");
        var org = await CreateTestOrganization(creator.Id);
        await CreateSubscription(org.Id, sub1.Id);
        await CreateSubscription(org.Id, sub2.Id);

        // Act
        var result = await _sut.GetAllAsync();

        // Assert
        result.Should().HaveCount(1);
        result.First().SubscriberCount.Should().Be(2);
    }

    #endregion

    #region GetById Tests

    [Fact]
    public async Task GetByIdAsync_WithInactiveOrg_ReturnsNull()
    {
        // Arrange
        var creator = await CreateTestUser();
        var inactiveOrg = await CreateTestOrganization(creator.Id, isActive: false);

        // Act
        var result = await _sut.GetByIdAsync(inactiveOrg.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WithActiveOrg_ReturnsOrganization()
    {
        // Arrange
        var creator = await CreateTestUser();
        var org = await CreateTestOrganization(creator.Id, "Test Org");

        // Act
        var result = await _sut.GetByIdAsync(org.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Test Org");
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task UpdateAsync_AsCreator_UpdatesOrganization()
    {
        // Arrange
        var creator = await CreateTestUser();
        var org = await CreateTestOrganization(creator.Id, "Original Name");
        var request = new UpdateOrganizationRequest("Updated Name", null, null, null);

        // Act
        var result = await _sut.UpdateAsync(org.Id, request, creator.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated Name");
    }

    [Fact]
    public async Task UpdateAsync_AsNonCreator_ReturnsNull()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var otherUser = await CreateTestUser("other@example.com");
        var org = await CreateTestOrganization(creator.Id);
        var request = new UpdateOrganizationRequest("Hacked Name", null, null, null);

        // Act
        var result = await _sut.UpdateAsync(org.Id, request, otherUser.Id);

        // Assert
        result.Should().BeNull();

        // Verify org wasn't modified
        var unchanged = await _context.Organizations.FindAsync(org.Id);
        unchanged!.Name.Should().Be("Test Org");
    }

    [Fact]
    public async Task UpdateAsync_WithPartialData_PreservesUnmodifiedFields()
    {
        // Arrange
        var creator = await CreateTestUser();
        var org = await CreateTestOrganization(
            creator.Id,
            name: "Original Name",
            description: "Original Description",
            location: "Boston",
            skillLevels: new List<string> { "Gold" }
        );

        var request = new UpdateOrganizationRequest("Updated Name", null, null, null);

        // Act
        var result = await _sut.UpdateAsync(org.Id, request, creator.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated Name");           // Changed
        result.Description.Should().Be("Original Description"); // Preserved
        result.Location.Should().Be("Boston");               // Preserved
        result.SkillLevels.Should().Contain("Gold");          // Preserved
    }

    [Fact]
    public async Task UpdateAsync_WithInvalidSkillLevel_ThrowsException()
    {
        // Arrange
        var creator = await CreateTestUser();
        var org = await CreateTestOrganization(creator.Id);
        var request = new UpdateOrganizationRequest(
            null,
            null,
            null,
            new List<string> { "Platinum" } // Invalid skill level
        );

        // Act & Assert
        await _sut.Invoking(s => s.UpdateAsync(org.Id, request, creator.Id))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid skill level*");
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task DeleteAsync_AsCreator_SoftDeletesOrganization()
    {
        // Arrange
        var creator = await CreateTestUser();
        var org = await CreateTestOrganization(creator.Id);

        // Act
        var result = await _sut.DeleteAsync(org.Id, creator.Id);

        // Assert
        result.Should().BeTrue();
        var deleted = await _context.Organizations.FindAsync(org.Id);
        deleted!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_AsNonCreator_ReturnsFalse()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var otherUser = await CreateTestUser("other@example.com");
        var org = await CreateTestOrganization(creator.Id);

        // Act
        var result = await _sut.DeleteAsync(org.Id, otherUser.Id);

        // Assert
        result.Should().BeFalse();

        // Verify org wasn't deleted
        var stillActive = await _context.Organizations.FindAsync(org.Id);
        stillActive!.IsActive.Should().BeTrue();
    }

    #endregion

    #region Create Tests

    [Fact]
    public async Task CreateAsync_ReturnsOrganizationWithCorrectCreator()
    {
        // Arrange
        var creator = await CreateTestUser();
        var request = new CreateOrganizationRequest("New Org", "Description", "Location", new List<string> { "Silver" });

        // Act
        var result = await _sut.CreateAsync(request, creator.Id);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("New Org");
        result.CreatorId.Should().Be(creator.Id);
        result.SubscriberCount.Should().Be(1); // Creator is auto-subscribed
        result.IsSubscribed.Should().BeTrue(); // Creator is auto-subscribed
    }

    [Fact]
    public async Task CreateAsync_WithInvalidSkillLevel_ThrowsException()
    {
        // Arrange
        var creator = await CreateTestUser();
        var request = new CreateOrganizationRequest(
            "New Org",
            "Description",
            "Location",
            new List<string> { "Platinum" } // Invalid skill level
        );

        // Act & Assert
        await _sut.Invoking(s => s.CreateAsync(request, creator.Id))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid skill level*");
    }

    #endregion

    #region GetMembers Tests

    [Fact]
    public async Task GetMembersAsync_AsCreator_ReturnsMembers()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var member1 = await CreateTestUser("member1@example.com");
        var member2 = await CreateTestUser("member2@example.com");
        var org = await CreateTestOrganization(creator.Id);
        await CreateSubscription(org.Id, member1.Id);
        await CreateSubscription(org.Id, member2.Id);

        // Act
        var result = await _sut.GetMembersAsync(org.Id, creator.Id);

        // Assert
        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetMembersAsync_AsNonCreator_ReturnsEmptyList()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var member = await CreateTestUser("member@example.com");
        var attacker = await CreateTestUser("attacker@example.com");
        var org = await CreateTestOrganization(creator.Id);
        await CreateSubscription(org.Id, member.Id);

        // Act
        var result = await _sut.GetMembersAsync(org.Id, attacker.Id);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region RemoveMember Tests

    [Fact]
    public async Task RemoveMemberAsync_AsAdmin_RemovesMember()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var member = await CreateTestUser("member@example.com");
        var org = await CreateTestOrganization(creator.Id);
        await CreateSubscription(org.Id, member.Id);

        // Verify member exists
        var membersBefore = await _context.OrganizationSubscriptions
            .Where(s => s.OrganizationId == org.Id)
            .CountAsync();
        membersBefore.Should().Be(1);

        // Act
        var result = await _sut.RemoveMemberAsync(org.Id, member.Id, creator.Id);

        // Assert
        result.Should().BeTrue();
        var membersAfter = await _context.OrganizationSubscriptions
            .Where(s => s.OrganizationId == org.Id)
            .CountAsync();
        membersAfter.Should().Be(0);
    }

    [Fact]
    public async Task RemoveMemberAsync_AsNonAdmin_ReturnsFalse()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var member = await CreateTestUser("member@example.com");
        var attacker = await CreateTestUser("attacker@example.com");
        var org = await CreateTestOrganization(creator.Id);
        await CreateSubscription(org.Id, member.Id);

        // Act
        var result = await _sut.RemoveMemberAsync(org.Id, member.Id, attacker.Id);

        // Assert
        result.Should().BeFalse();
        // Member should still exist
        var memberStillExists = await _context.OrganizationSubscriptions
            .AnyAsync(s => s.OrganizationId == org.Id && s.UserId == member.Id);
        memberStillExists.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveMemberAsync_NonExistentMember_ReturnsFalse()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var org = await CreateTestOrganization(creator.Id);
        var nonExistentUserId = Guid.NewGuid();

        // Act
        var result = await _sut.RemoveMemberAsync(org.Id, nonExistentUserId, creator.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveMemberAsync_AsNonCreatorAdmin_RemovesMember()
    {
        // Arrange - User is admin but NOT the original creator
        var creator = await CreateTestUser("creator@example.com");
        var nonCreatorAdmin = await CreateTestUser("admin@example.com");
        var member = await CreateTestUser("member@example.com");
        var org = await CreateTestOrganization(creator.Id);
        await AddAdminToOrganization(org.Id, nonCreatorAdmin.Id, creator.Id);
        await CreateSubscription(org.Id, member.Id);

        // Act
        var result = await _sut.RemoveMemberAsync(org.Id, member.Id, nonCreatorAdmin.Id);

        // Assert
        result.Should().BeTrue();
        var memberStillExists = await _context.OrganizationSubscriptions
            .AnyAsync(s => s.OrganizationId == org.Id && s.UserId == member.Id);
        memberStillExists.Should().BeFalse();
    }

    #endregion

    #region Multi-Admin Tests

    /// <summary>
    /// Helper to add a non-creator as admin to an organization.
    /// </summary>
    private async Task AddAdminToOrganization(Guid orgId, Guid userId, Guid? addedByUserId = null)
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
    }

    [Fact]
    public async Task UpdateAsync_AsNonCreatorAdmin_UpdatesOrganization()
    {
        // Arrange - User is admin but NOT the original creator
        var creator = await CreateTestUser("creator@example.com");
        var nonCreatorAdmin = await CreateTestUser("admin@example.com");
        var org = await CreateTestOrganization(creator.Id, "Original Name");
        await AddAdminToOrganization(org.Id, nonCreatorAdmin.Id, creator.Id);

        var request = new UpdateOrganizationRequest("Updated By Non-Creator Admin", null, null, null);

        // Act
        var result = await _sut.UpdateAsync(org.Id, request, nonCreatorAdmin.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated By Non-Creator Admin");
    }

    [Fact]
    public async Task DeleteAsync_AsNonCreatorAdmin_SoftDeletesOrganization()
    {
        // Arrange - User is admin but NOT the original creator
        var creator = await CreateTestUser("creator@example.com");
        var nonCreatorAdmin = await CreateTestUser("admin@example.com");
        var org = await CreateTestOrganization(creator.Id);
        await AddAdminToOrganization(org.Id, nonCreatorAdmin.Id, creator.Id);

        // Act
        var result = await _sut.DeleteAsync(org.Id, nonCreatorAdmin.Id);

        // Assert
        result.Should().BeTrue();
        var deleted = await _context.Organizations.FindAsync(org.Id);
        deleted!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetMembersAsync_AsNonCreatorAdmin_ReturnsMembers()
    {
        // Arrange - User is admin but NOT the original creator
        var creator = await CreateTestUser("creator@example.com");
        var nonCreatorAdmin = await CreateTestUser("admin@example.com");
        var member = await CreateTestUser("member@example.com");
        var org = await CreateTestOrganization(creator.Id);
        await AddAdminToOrganization(org.Id, nonCreatorAdmin.Id, creator.Id);
        await CreateSubscription(org.Id, member.Id);

        // Act
        var result = await _sut.GetMembersAsync(org.Id, nonCreatorAdmin.Id);

        // Assert
        result.Should().HaveCount(1);
        result.First().Email.Should().Be("member@example.com");
    }

    [Fact]
    public async Task CreateAsync_AddsCreatorAsFirstAdmin()
    {
        // Arrange
        var creator = await CreateTestUser();
        var request = new CreateOrganizationRequest("New Org", "Description", "Location", new List<string> { "Silver" });

        // Act
        var result = await _sut.CreateAsync(request, creator.Id);

        // Assert
        result.Should().NotBeNull();

        // Verify an admin record was created for the creator
        var adminRecord = await _context.OrganizationAdmins
            .FirstOrDefaultAsync(a => a.OrganizationId == result.Id && a.UserId == creator.Id);
        adminRecord.Should().NotBeNull();
        adminRecord!.AddedByUserId.Should().BeNull(); // Original creator has no AddedBy
    }

    [Fact]
    public async Task GetUserAdminOrganizationsAsync_ReturnsOrgsWhereUserIsAdmin()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var admin = await CreateTestUser("admin@example.com");

        // Create orgs - admin is only admin of org1, not org2
        var org1 = await CreateTestOrganization(creator.Id, "Org Where Admin");
        var org2 = await CreateTestOrganization(creator.Id, "Org Not Admin");

        // Add admin to org1 only
        await AddAdminToOrganization(org1.Id, admin.Id, creator.Id);

        // Act
        var result = await _sut.GetUserAdminOrganizationsAsync(admin.Id);

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("Org Where Admin");
    }

    [Fact]
    public async Task GetByIdAsync_SetsIsAdminTrueForNonCreatorAdmin()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var nonCreatorAdmin = await CreateTestUser("admin@example.com");
        var org = await CreateTestOrganization(creator.Id, "Test Org");
        await AddAdminToOrganization(org.Id, nonCreatorAdmin.Id, creator.Id);

        // Act
        var result = await _sut.GetByIdAsync(org.Id, nonCreatorAdmin.Id);

        // Assert
        result.Should().NotBeNull();
        result!.IsAdmin.Should().BeTrue();
    }

    #endregion

    #region GetUserSubscriptions Tests

    [Fact]
    public async Task GetUserSubscriptionsAsync_ReturnsOnlyUserSubscriptions()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user1 = await CreateTestUser("user1@example.com");
        var user2 = await CreateTestUser("user2@example.com");
        var org1 = await CreateTestOrganization(creator.Id, "Org 1");
        var org2 = await CreateTestOrganization(creator.Id, "Org 2");

        await CreateSubscription(org1.Id, user1.Id);
        await CreateSubscription(org2.Id, user1.Id);
        await CreateSubscription(org1.Id, user2.Id);

        // Act
        var result = await _sut.GetUserSubscriptionsAsync(user1.Id);

        // Assert
        result.Should().HaveCount(2);
        result.Select(s => s.Organization.Name).Should().Contain("Org 1", "Org 2");
    }

    [Fact]
    public async Task GetUserSubscriptionsAsync_ExcludesInactiveOrganizations()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var activeOrg = await CreateTestOrganization(creator.Id, "Active Org", isActive: true);
        var inactiveOrg = await CreateTestOrganization(creator.Id, "Inactive Org", isActive: false);

        await CreateSubscription(activeOrg.Id, user.Id);
        await CreateSubscription(inactiveOrg.Id, user.Id);

        // Act
        var result = await _sut.GetUserSubscriptionsAsync(user.Id);

        // Assert
        result.Should().HaveCount(1);
        result.First().Organization.Name.Should().Be("Active Org");
    }

    #endregion
}
