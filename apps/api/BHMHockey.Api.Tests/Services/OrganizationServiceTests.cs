using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
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
    private readonly OrganizationWaiverService _waiverService;
    private readonly Mock<IWaitlistService> _mockWaitlistService;
    private readonly EventService _eventService;
    private readonly OrganizationService _sut;

    public OrganizationServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new AppDbContext(options);
        _adminService = new OrganizationAdminService(_context);
        _waiverService = new OrganizationWaiverService(_context, _adminService, Mock.Of<ILogger<OrganizationWaiverService>>());
        _mockWaitlistService = new Mock<IWaitlistService>();
        _mockWaitlistService.Setup(w => w.GetNextWaitlistPositionAsync(It.IsAny<Guid>()))
            .ReturnsAsync(1);
        _mockWaitlistService.Setup(w => w.PromoteFromWaitlistAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<bool>()))
            .ReturnsAsync(new PromotionResult());
        // Real EventService so LeaveAsync composes the standard cancellation path
        _eventService = new EventService(
            _context,
            Mock.Of<INotificationService>(),
            _adminService,
            _mockWaitlistService.Object,
            _waiverService,
            Mock.Of<ILogger<EventService>>());
        _sut = new OrganizationService(_context, _adminService, _waiverService, _eventService);
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
        var request = new UpdateOrganizationRequest("Updated Name", null, null, null, null, null, null, null, null, null, null);

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
        var request = new UpdateOrganizationRequest("Hacked Name", null, null, null, null, null, null, null, null, null, null);

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

        var request = new UpdateOrganizationRequest("Updated Name", null, null, null, null, null, null, null, null, null, null);

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
            new List<string> { "Platinum" }, // Invalid skill level
            null,
            null,
            null,
            null,
            null,
            null,
            null
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
        var request = new CreateOrganizationRequest("New Org", "Description", "Location", new List<string> { "Silver" }, null, null, null, null, null, null, null);

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
            new List<string> { "Platinum" }, // Invalid skill level
            null,
            null,
            null,
            null,
            null,
            null,
            null
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

        var request = new UpdateOrganizationRequest("Updated By Non-Creator Admin", null, null, null, null, null, null, null, null, null, null);

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
        var request = new CreateOrganizationRequest("New Org", "Description", "Location", new List<string> { "Silver" }, null, null, null, null, null, null, null);

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

    #region GroupMe Link Tests

    private static UpdateOrganizationRequest GroupMeLinkUpdateRequest(string? groupMeLink)
    {
        return new UpdateOrganizationRequest(
            Name: null,
            Description: null,
            Location: null,
            SkillLevels: null,
            DefaultDayOfWeek: null,
            DefaultStartTime: null,
            DefaultDurationMinutes: null,
            DefaultMaxPlayers: null,
            DefaultCost: null,
            DefaultVenue: null,
            DefaultVisibility: null,
            GroupMeLink: groupMeLink
        );
    }

    [Fact]
    public async Task CreateAsync_WithWhitespacePaddedGroupMeLink_TrimsAndStores()
    {
        // Arrange
        var creator = await CreateTestUser();
        var request = new CreateOrganizationRequest(
            Name: "GroupMe Org",
            Description: null,
            Location: null,
            SkillLevels: null,
            DefaultDayOfWeek: null,
            DefaultStartTime: null,
            DefaultDurationMinutes: null,
            DefaultMaxPlayers: null,
            DefaultCost: null,
            DefaultVenue: null,
            DefaultVisibility: null,
            GroupMeLink: "  https://groupme.com/join_group/abc  ");

        // Act
        var result = await _sut.CreateAsync(request, creator.Id);

        // Assert
        result.GroupMeLink.Should().Be("https://groupme.com/join_group/abc");
        var org = await _context.Organizations.FindAsync(result.Id);
        org!.GroupMeLink.Should().Be("https://groupme.com/join_group/abc");
    }

    [Fact]
    public async Task CreateAsync_WithInvalidGroupMeLink_ThrowsInvalidOperationException()
    {
        // Arrange
        var creator = await CreateTestUser();
        var request = new CreateOrganizationRequest(
            Name: "Bad Link Org",
            Description: null,
            Location: null,
            SkillLevels: null,
            DefaultDayOfWeek: null,
            DefaultStartTime: null,
            DefaultDurationMinutes: null,
            DefaultMaxPlayers: null,
            DefaultCost: null,
            DefaultVenue: null,
            DefaultVisibility: null,
            GroupMeLink: "http://groupme.com/join_group/abc");

        // Act & Assert
        await _sut.Invoking(s => s.CreateAsync(request, creator.Id))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*GroupMe link*");
    }

    [Fact]
    public async Task UpdateAsync_SetGroupMeLink_ReturnsLinkInDto()
    {
        // Arrange
        var creator = await CreateTestUser();
        var org = await CreateTestOrganization(creator.Id);

        // Act
        var result = await _sut.UpdateAsync(org.Id, GroupMeLinkUpdateRequest("https://www.groupme.com/join_group/xyz"), creator.Id);

        // Assert
        result!.GroupMeLink.Should().Be("https://www.groupme.com/join_group/xyz");
    }

    [Fact]
    public async Task UpdateAsync_EmptyGroupMeLink_ClearsLink()
    {
        // Arrange
        var creator = await CreateTestUser();
        var org = await CreateTestOrganization(creator.Id);
        org.GroupMeLink = "https://groupme.com/join_group/old";
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.UpdateAsync(org.Id, GroupMeLinkUpdateRequest(""), creator.Id);

        // Assert
        result!.GroupMeLink.Should().BeNull();
        var updated = await _context.Organizations.FindAsync(org.Id);
        updated!.GroupMeLink.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_NullGroupMeLink_LeavesLinkUnchanged()
    {
        // Arrange
        var creator = await CreateTestUser();
        var org = await CreateTestOrganization(creator.Id);
        org.GroupMeLink = "https://groupme.com/join_group/keep";
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.UpdateAsync(org.Id, GroupMeLinkUpdateRequest(null), creator.Id);

        // Assert
        result!.GroupMeLink.Should().Be("https://groupme.com/join_group/keep");
    }

    [Theory]
    [InlineData("http://groupme.com/join_group/abc")]  // not https
    [InlineData("https://discord.gg/abc")]             // wrong host
    [InlineData("groupme.com/join_group/abc")]         // not absolute https URL
    public async Task UpdateAsync_WithInvalidGroupMeLink_ThrowsInvalidOperationException(string link)
    {
        // Arrange
        var creator = await CreateTestUser();
        var org = await CreateTestOrganization(creator.Id);

        // Act & Assert
        await _sut.Invoking(s => s.UpdateAsync(org.Id, GroupMeLinkUpdateRequest(link), creator.Id))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*GroupMe link*");
    }

    [Fact]
    public async Task UpdateAsync_GroupMeLinkOverMaxLength_ThrowsInvalidOperationException()
    {
        // Arrange
        var creator = await CreateTestUser();
        var org = await CreateTestOrganization(creator.Id);
        var longLink = "https://groupme.com/join_group/" + new string('a', 500);

        // Act & Assert
        await _sut.Invoking(s => s.UpdateAsync(org.Id, GroupMeLinkUpdateRequest(longLink), creator.Id))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*500*");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsGroupMeLink()
    {
        // Arrange
        var creator = await CreateTestUser();
        var org = await CreateTestOrganization(creator.Id);
        org.GroupMeLink = "https://groupme.com/join_group/visible";
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByIdAsync(org.Id, creator.Id);

        // Assert
        result!.GroupMeLink.Should().Be("https://groupme.com/join_group/visible");
    }

    #endregion

    #region DefaultShowWaitlistBeforePublish Tests

    private static UpdateOrganizationRequest ShowWaitlistDefaultUpdateRequest(bool? defaultShowWaitlistBeforePublish)
    {
        return new UpdateOrganizationRequest(
            Name: null,
            Description: null,
            Location: null,
            SkillLevels: null,
            DefaultDayOfWeek: null,
            DefaultStartTime: null,
            DefaultDurationMinutes: null,
            DefaultMaxPlayers: null,
            DefaultCost: null,
            DefaultVenue: null,
            DefaultVisibility: null,
            DefaultShowWaitlistBeforePublish: defaultShowWaitlistBeforePublish
        );
    }

    [Fact]
    public async Task CreateAsync_WithDefaultShowWaitlistBeforePublish_StoresAndReturnsIt()
    {
        // Arrange
        var creator = await CreateTestUser();
        var request = new CreateOrganizationRequest(
            Name: "Waitlist Default Org",
            Description: null,
            Location: null,
            SkillLevels: null,
            DefaultDayOfWeek: null,
            DefaultStartTime: null,
            DefaultDurationMinutes: null,
            DefaultMaxPlayers: null,
            DefaultCost: null,
            DefaultVenue: null,
            DefaultVisibility: null,
            DefaultShowWaitlistBeforePublish: true);

        // Act
        var result = await _sut.CreateAsync(request, creator.Id);

        // Assert
        result.DefaultShowWaitlistBeforePublish.Should().BeTrue();
        var org = await _context.Organizations.FindAsync(result.Id);
        org!.DefaultShowWaitlistBeforePublish.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_SetsDefaultShowWaitlistBeforePublish_ReturnsItInDto()
    {
        // Arrange
        var creator = await CreateTestUser();
        var org = await CreateTestOrganization(creator.Id);

        // Act
        var result = await _sut.UpdateAsync(org.Id, ShowWaitlistDefaultUpdateRequest(true), creator.Id);

        // Assert
        result!.DefaultShowWaitlistBeforePublish.Should().BeTrue();
        var updated = await _context.Organizations.FindAsync(org.Id);
        updated!.DefaultShowWaitlistBeforePublish.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_NullDefaultShowWaitlistBeforePublish_LeavesUnchanged()
    {
        // Arrange
        var creator = await CreateTestUser();
        var org = await CreateTestOrganization(creator.Id);
        org.DefaultShowWaitlistBeforePublish = true;
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.UpdateAsync(org.Id, ShowWaitlistDefaultUpdateRequest(null), creator.Id);

        // Assert
        result!.DefaultShowWaitlistBeforePublish.Should().BeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsDefaultShowWaitlistBeforePublish()
    {
        // Arrange
        var creator = await CreateTestUser();
        var org = await CreateTestOrganization(creator.Id);
        org.DefaultShowWaitlistBeforePublish = true;
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByIdAsync(org.Id, creator.Id);

        // Assert
        result!.DefaultShowWaitlistBeforePublish.Should().BeTrue();
    }

    #endregion

    #region Leave Organization Tests

    private async Task<Event> CreateOrgEvent(Guid creatorId, Guid orgId, DateTime? eventDate = null, string status = "Published")
    {
        var evt = new Event
        {
            Id = Guid.NewGuid(),
            CreatorId = creatorId,
            OrganizationId = orgId,
            Name = "Org Event",
            EventDate = eventDate ?? DateTime.UtcNow.AddDays(7),
            MaxPlayers = 10,
            Cost = 0,
            Status = status,
            Visibility = "Public"
        };
        _context.Events.Add(evt);
        await _context.SaveChangesAsync();
        return evt;
    }

    private async Task<EventRegistration> CreateEventRegistration(Guid eventId, Guid userId, string status = "Registered")
    {
        var registration = new EventRegistration
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            UserId = userId,
            Status = status,
            RegisteredPosition = "Skater",
            WaitlistPosition = status == "Waitlisted" ? 1 : null,
            RegisteredAt = DateTime.UtcNow
        };
        _context.EventRegistrations.Add(registration);
        await _context.SaveChangesAsync();
        return registration;
    }

    [Fact]
    public async Task LeaveAsync_UnsubscribesAndCancelsUpcomingRegistrations()
    {
        // Arrange
        var creator = await CreateTestUser();
        var member = await CreateTestUser("member@example.com");
        var org = await CreateTestOrganization(creator.Id);
        await CreateSubscription(org.Id, member.Id);
        var upcoming1 = await CreateOrgEvent(creator.Id, org.Id);
        var upcoming2 = await CreateOrgEvent(creator.Id, org.Id);
        var reg1 = await CreateEventRegistration(upcoming1.Id, member.Id, "Registered");
        var reg2 = await CreateEventRegistration(upcoming2.Id, member.Id, "Waitlisted");

        // Act
        var result = await _sut.LeaveAsync(org.Id, member.Id);

        // Assert
        result.Should().BeTrue();
        (await _context.OrganizationSubscriptions
            .AnyAsync(s => s.OrganizationId == org.Id && s.UserId == member.Id)).Should().BeFalse();
        (await _context.EventRegistrations.FindAsync(reg1.Id))!.Status.Should().Be("Cancelled");
        (await _context.EventRegistrations.FindAsync(reg2.Id))!.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task LeaveAsync_ReusesCancellationPath_FiresPromotionForRegisteredCancellations()
    {
        // Arrange - cancelling a REGISTERED spot must go through the standard
        // promotion side effect (PromoteFromWaitlistAsync), same as a manual cancel
        var creator = await CreateTestUser();
        var member = await CreateTestUser("member@example.com");
        var org = await CreateTestOrganization(creator.Id);
        await CreateSubscription(org.Id, member.Id);
        var evt = await CreateOrgEvent(creator.Id, org.Id);
        await CreateEventRegistration(evt.Id, member.Id, "Registered");

        // Act
        await _sut.LeaveAsync(org.Id, member.Id);

        // Assert
        _mockWaitlistService.Verify(
            w => w.PromoteFromWaitlistAsync(evt.Id, 1, true),
            Times.Once);
    }

    [Fact]
    public async Task LeaveAsync_PastAndCancelledEventRegistrations_Untouched()
    {
        // Arrange
        var creator = await CreateTestUser();
        var member = await CreateTestUser("member@example.com");
        var org = await CreateTestOrganization(creator.Id);
        await CreateSubscription(org.Id, member.Id);
        var pastEvent = await CreateOrgEvent(creator.Id, org.Id, eventDate: DateTime.UtcNow.AddDays(-3));
        var cancelledEvent = await CreateOrgEvent(creator.Id, org.Id, status: "Cancelled");
        var pastReg = await CreateEventRegistration(pastEvent.Id, member.Id, "Registered");
        var cancelledEventReg = await CreateEventRegistration(cancelledEvent.Id, member.Id, "Registered");

        // Act
        await _sut.LeaveAsync(org.Id, member.Id);

        // Assert - historical/cancelled-event registrations preserved
        (await _context.EventRegistrations.FindAsync(pastReg.Id))!.Status.Should().Be("Registered");
        (await _context.EventRegistrations.FindAsync(cancelledEventReg.Id))!.Status.Should().Be("Registered");
    }

    [Fact]
    public async Task LeaveAsync_OtherOrgRegistrations_Untouched()
    {
        // Arrange
        var creator = await CreateTestUser();
        var member = await CreateTestUser("member@example.com");
        var org = await CreateTestOrganization(creator.Id, "Org A");
        var otherOrg = await CreateTestOrganization(creator.Id, "Org B");
        await CreateSubscription(org.Id, member.Id);
        await CreateSubscription(otherOrg.Id, member.Id);
        var otherEvent = await CreateOrgEvent(creator.Id, otherOrg.Id);
        var otherReg = await CreateEventRegistration(otherEvent.Id, member.Id, "Registered");

        // Act
        await _sut.LeaveAsync(org.Id, member.Id);

        // Assert
        (await _context.EventRegistrations.FindAsync(otherReg.Id))!.Status.Should().Be("Registered");
        (await _context.OrganizationSubscriptions
            .AnyAsync(s => s.OrganizationId == otherOrg.Id && s.UserId == member.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task LeaveAsync_NotSubscribedButRegistered_StillCancelsRegistrations()
    {
        // Arrange - the blocking gate lists orgs by registration, not subscription,
        // so leave must work for registered-but-unsubscribed users too
        var creator = await CreateTestUser();
        var member = await CreateTestUser("member@example.com");
        var org = await CreateTestOrganization(creator.Id);
        var evt = await CreateOrgEvent(creator.Id, org.Id);
        var reg = await CreateEventRegistration(evt.Id, member.Id, "Registered");

        // Act
        var result = await _sut.LeaveAsync(org.Id, member.Id);

        // Assert
        result.Should().BeTrue();
        (await _context.EventRegistrations.FindAsync(reg.Id))!.Status.Should().Be("Cancelled");
    }

    #endregion

    #region Member Waiver Status Tests

    [Fact]
    public async Task GetMembersAsync_NoActiveWaiver_WaiverFlagsAreNull()
    {
        // Arrange
        var creator = await CreateTestUser();
        var member = await CreateTestUser("member@example.com");
        var org = await CreateTestOrganization(creator.Id);
        await CreateSubscription(org.Id, creator.Id);
        await CreateSubscription(org.Id, member.Id);

        // Act
        var members = await _sut.GetMembersAsync(org.Id, creator.Id);

        // Assert
        members.Should().OnlyContain(m => m.HasAcceptedCurrentWaiver == null);
    }

    [Fact]
    public async Task GetMembersAsync_ActiveWaiver_FlagsAcceptedAndUnaccepted()
    {
        // Arrange
        var creator = await CreateTestUser();
        var acceptedMember = await CreateTestUser("accepted@example.com");
        var unacceptedMember = await CreateTestUser("unaccepted@example.com");
        var org = await CreateTestOrganization(creator.Id);
        await CreateSubscription(org.Id, creator.Id);
        await CreateSubscription(org.Id, acceptedMember.Id);
        await CreateSubscription(org.Id, unacceptedMember.Id);
        var waiver = await _waiverService.SetWaiverAsync(org.Id, "waiver text", creator.Id);
        await _waiverService.AcceptWaiverAsync(org.Id, new AcceptWaiverRequest(waiver!.Id, "John Doe"), acceptedMember.Id);

        // Act
        var members = await _sut.GetMembersAsync(org.Id, creator.Id);

        // Assert
        members.Single(m => m.Id == acceptedMember.Id).HasAcceptedCurrentWaiver.Should().BeTrue();
        members.Single(m => m.Id == unacceptedMember.Id).HasAcceptedCurrentWaiver.Should().BeFalse();
        members.Single(m => m.Id == creator.Id).HasAcceptedCurrentWaiver.Should().BeFalse();
    }

    [Fact]
    public async Task GetMembersAsync_ClearedWaiver_WaiverFlagsBackToNull()
    {
        // Arrange
        var creator = await CreateTestUser();
        var member = await CreateTestUser("member@example.com");
        var org = await CreateTestOrganization(creator.Id);
        await CreateSubscription(org.Id, creator.Id);
        await CreateSubscription(org.Id, member.Id);
        var waiver = await _waiverService.SetWaiverAsync(org.Id, "waiver text", creator.Id);
        await _waiverService.AcceptWaiverAsync(org.Id, new AcceptWaiverRequest(waiver!.Id, "John Doe"), member.Id);
        await _waiverService.SetWaiverAsync(org.Id, "", creator.Id);

        // Act
        var members = await _sut.GetMembersAsync(org.Id, creator.Id);

        // Assert - gating and indicators fully off after clearing
        members.Should().OnlyContain(m => m.HasAcceptedCurrentWaiver == null);
    }

    [Fact]
    public async Task GetMembersAsync_NewVersion_ResetsAcceptanceFlags()
    {
        // Arrange
        var creator = await CreateTestUser();
        var member = await CreateTestUser("member@example.com");
        var org = await CreateTestOrganization(creator.Id);
        await CreateSubscription(org.Id, creator.Id);
        await CreateSubscription(org.Id, member.Id);
        var v1 = await _waiverService.SetWaiverAsync(org.Id, "v1", creator.Id);
        await _waiverService.AcceptWaiverAsync(org.Id, new AcceptWaiverRequest(v1!.Id, "John Doe"), member.Id);
        await _waiverService.SetWaiverAsync(org.Id, "v2", creator.Id);

        // Act
        var members = await _sut.GetMembersAsync(org.Id, creator.Id);

        // Assert - v1 acceptance does not carry over to v2
        members.Single(m => m.Id == member.Id).HasAcceptedCurrentWaiver.Should().BeFalse();
    }

    #endregion
}
