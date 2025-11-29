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
    private readonly OrganizationService _sut;

    public OrganizationServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _sut = new OrganizationService(_context);
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
        string? skillLevel = "Gold",
        bool isActive = true)
    {
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            Location = location,
            SkillLevel = skillLevel,
            CreatorId = creatorId,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };

        _context.Organizations.Add(org);
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
            skillLevel: "Gold"
        );

        var request = new UpdateOrganizationRequest("Updated Name", null, null, null);

        // Act
        var result = await _sut.UpdateAsync(org.Id, request, creator.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated Name");           // Changed
        result.Description.Should().Be("Original Description"); // Preserved
        result.Location.Should().Be("Boston");               // Preserved
        result.SkillLevel.Should().Be("Gold");               // Preserved
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
        var request = new CreateOrganizationRequest("New Org", "Description", "Location", "Silver");

        // Act
        var result = await _sut.CreateAsync(request, creator.Id);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("New Org");
        result.CreatorId.Should().Be(creator.Id);
        result.SubscriberCount.Should().Be(0);
        result.IsSubscribed.Should().BeFalse();
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
