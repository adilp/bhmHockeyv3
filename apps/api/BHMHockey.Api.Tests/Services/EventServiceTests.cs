using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// Tests for EventService - protecting event and registration business logic.
/// These tests ensure:
/// - Registration rules are enforced (capacity, re-registration after cancel)
/// - Visibility rules filter events correctly
/// - Only creators can modify/delete events
/// - Validation prevents invalid visibility configurations
/// </summary>
public class EventServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly EventService _sut;

    public EventServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _sut = new EventService(_context);
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

    private async Task<Event> CreateTestEvent(
        Guid creatorId,
        Guid? organizationId = null,
        string name = "Test Event",
        int maxPlayers = 10,
        string visibility = "Public",
        string status = "Published",
        DateTime? eventDate = null)
    {
        var evt = new Event
        {
            Id = Guid.NewGuid(),
            CreatorId = creatorId,
            OrganizationId = organizationId,
            Name = name,
            Description = "Test Description",
            EventDate = eventDate ?? DateTime.UtcNow.AddDays(7),
            Duration = 60,
            Venue = "Test Venue",
            MaxPlayers = maxPlayers,
            Cost = 25.00m,
            Status = status,
            Visibility = visibility,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Events.Add(evt);
        await _context.SaveChangesAsync();
        return evt;
    }

    private async Task<EventRegistration> CreateRegistration(
        Guid eventId,
        Guid userId,
        string status = "Registered")
    {
        var registration = new EventRegistration
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            UserId = userId,
            Status = status,
            RegisteredAt = DateTime.UtcNow
        };

        _context.EventRegistrations.Add(registration);
        await _context.SaveChangesAsync();
        return registration;
    }

    #endregion

    #region Registration Tests

    [Fact]
    public async Task RegisterAsync_WhenNotRegistered_CreatesRegistration()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var evt = await CreateTestEvent(creator.Id);

        // Act
        var result = await _sut.RegisterAsync(evt.Id, user.Id);

        // Assert
        result.Should().BeTrue();
        var registration = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == evt.Id && r.UserId == user.Id);
        registration.Should().NotBeNull();
        registration!.Status.Should().Be("Registered");
    }

    [Fact]
    public async Task RegisterAsync_WhenAlreadyRegistered_ReturnsFalse()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var evt = await CreateTestEvent(creator.Id);
        await CreateRegistration(evt.Id, user.Id, "Registered");

        // Act
        var result = await _sut.RegisterAsync(evt.Id, user.Id);

        // Assert
        result.Should().BeFalse();

        // Verify no duplicate was created
        var registrationCount = await _context.EventRegistrations
            .CountAsync(r => r.EventId == evt.Id && r.UserId == user.Id);
        registrationCount.Should().Be(1);
    }

    [Fact]
    public async Task RegisterAsync_WhenEventFull_ThrowsInvalidOperationException()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id, maxPlayers: 2);

        // Fill the event
        var user1 = await CreateTestUser("user1@example.com");
        var user2 = await CreateTestUser("user2@example.com");
        await CreateRegistration(evt.Id, user1.Id);
        await CreateRegistration(evt.Id, user2.Id);

        var newUser = await CreateTestUser("new@example.com");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RegisterAsync(evt.Id, newUser.Id));
    }

    [Fact]
    public async Task RegisterAsync_AfterCancellingRegistration_ReactivatesRegistration()
    {
        // Arrange - This tests the bug fix for re-registration
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var evt = await CreateTestEvent(creator.Id);

        // Create and cancel a registration
        await CreateRegistration(evt.Id, user.Id, "Cancelled");

        // Act
        var result = await _sut.RegisterAsync(evt.Id, user.Id);

        // Assert
        result.Should().BeTrue();
        var registration = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == evt.Id && r.UserId == user.Id);
        registration.Should().NotBeNull();
        registration!.Status.Should().Be("Registered");

        // Verify only one registration exists (no duplicate)
        var count = await _context.EventRegistrations
            .CountAsync(r => r.EventId == evt.Id && r.UserId == user.Id);
        count.Should().Be(1);
    }

    [Fact]
    public async Task CancelRegistrationAsync_WhenRegistered_SetsStatusToCancelled()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var evt = await CreateTestEvent(creator.Id);
        await CreateRegistration(evt.Id, user.Id);

        // Act
        var result = await _sut.CancelRegistrationAsync(evt.Id, user.Id);

        // Assert
        result.Should().BeTrue();
        var registration = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == evt.Id && r.UserId == user.Id);
        registration!.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task CancelRegistrationAsync_WhenNotRegistered_ReturnsFalse()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var evt = await CreateTestEvent(creator.Id);

        // Act
        var result = await _sut.CancelRegistrationAsync(evt.Id, user.Id);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Visibility Tests

    [Fact]
    public async Task GetAllAsync_WithPublicEvent_ReturnsForAllUsers()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var randomUser = await CreateTestUser("random@example.com");
        await CreateTestEvent(creator.Id, visibility: "Public");

        // Act
        var result = await _sut.GetAllAsync(randomUser.Id);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAllAsync_WithOrganizationMembersEvent_ReturnsOnlyForSubscribers()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var org = await CreateTestOrganization(creator.Id);
        await CreateTestEvent(creator.Id, organizationId: org.Id, visibility: "OrganizationMembers");

        var subscriber = await CreateTestUser("subscriber@example.com");
        var nonSubscriber = await CreateTestUser("nonsubscriber@example.com");
        await CreateSubscription(org.Id, subscriber.Id);

        // Act
        var subscriberResult = await _sut.GetAllAsync(subscriber.Id);
        var nonSubscriberResult = await _sut.GetAllAsync(nonSubscriber.Id);

        // Assert
        subscriberResult.Should().HaveCount(1);
        nonSubscriberResult.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_WithInviteOnlyEvent_ReturnsOnlyForCreator()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var otherUser = await CreateTestUser("other@example.com");
        await CreateTestEvent(creator.Id, visibility: "InviteOnly");

        // Act
        var creatorResult = await _sut.GetAllAsync(creator.Id);
        var otherResult = await _sut.GetAllAsync(otherUser.Id);

        // Assert
        creatorResult.Should().HaveCount(1);
        otherResult.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_CreatorCanAlwaysSeeOwnEvents()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var org = await CreateTestOrganization(creator.Id);

        // Create events with different visibilities
        await CreateTestEvent(creator.Id, visibility: "Public", name: "Public Event");
        await CreateTestEvent(creator.Id, organizationId: org.Id, visibility: "OrganizationMembers", name: "Members Event");
        await CreateTestEvent(creator.Id, visibility: "InviteOnly", name: "Private Event");

        // Act - Creator is NOT subscribed to their own org, but should still see all
        var result = await _sut.GetAllAsync(creator.Id);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetByIdAsync_WithHiddenEvent_ReturnsNullForNonCreator()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var otherUser = await CreateTestUser("other@example.com");
        var evt = await CreateTestEvent(creator.Id, visibility: "InviteOnly");

        // Act
        var result = await _sut.GetByIdAsync(evt.Id, otherUser.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WithHiddenEvent_ReturnsForCreator()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id, visibility: "InviteOnly");

        // Act
        var result = await _sut.GetByIdAsync(evt.Id, creator.Id);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Visibility Validation Tests

    [Fact]
    public async Task CreateAsync_OrganizationMembersWithoutOrg_ThrowsInvalidOperationException()
    {
        // Arrange
        var creator = await CreateTestUser();
        var request = new CreateEventRequest(
            OrganizationId: null, // No org
            Name: "Test Event",
            Description: null,
            EventDate: DateTime.UtcNow.AddDays(7),
            Duration: 60,
            Venue: null,
            MaxPlayers: 10,
            Cost: 0,
            RegistrationDeadline: null,
            Visibility: "OrganizationMembers" // Invalid without org
        );

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CreateAsync(request, creator.Id));
    }

    [Fact]
    public async Task UpdateAsync_SetOrganizationMembersWithoutOrg_ThrowsInvalidOperationException()
    {
        // Arrange
        var creator = await CreateTestUser();
        var evt = await CreateTestEvent(creator.Id, organizationId: null, visibility: "Public");

        var request = new UpdateEventRequest(
            Name: null,
            Description: null,
            EventDate: null,
            Duration: null,
            Venue: null,
            MaxPlayers: null,
            Cost: null,
            RegistrationDeadline: null,
            Status: null,
            Visibility: "OrganizationMembers" // Invalid - no org on event
        );

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateAsync(evt.Id, request, creator.Id));
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task UpdateAsync_AsCreator_UpdatesEvent()
    {
        // Arrange
        var creator = await CreateTestUser();
        var evt = await CreateTestEvent(creator.Id, name: "Original Name");
        var request = new UpdateEventRequest(
            Name: "Updated Name",
            Description: null,
            EventDate: null,
            Duration: null,
            Venue: null,
            MaxPlayers: null,
            Cost: null,
            RegistrationDeadline: null,
            Status: null,
            Visibility: null
        );

        // Act
        var result = await _sut.UpdateAsync(evt.Id, request, creator.Id);

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
        var evt = await CreateTestEvent(creator.Id, name: "Original Name");

        var request = new UpdateEventRequest(
            Name: "Hacked Name",
            Description: null,
            EventDate: null,
            Duration: null,
            Venue: null,
            MaxPlayers: null,
            Cost: null,
            RegistrationDeadline: null,
            Status: null,
            Visibility: null
        );

        // Act
        var result = await _sut.UpdateAsync(evt.Id, request, otherUser.Id);

        // Assert
        result.Should().BeNull();

        // Verify event wasn't modified
        var unchanged = await _context.Events.FindAsync(evt.Id);
        unchanged!.Name.Should().Be("Original Name");
    }

    [Fact]
    public async Task DeleteAsync_AsCreator_SoftDeletesEvent()
    {
        // Arrange
        var creator = await CreateTestUser();
        var evt = await CreateTestEvent(creator.Id);

        // Act
        var result = await _sut.DeleteAsync(evt.Id, creator.Id);

        // Assert
        result.Should().BeTrue();
        var deleted = await _context.Events.FindAsync(evt.Id);
        deleted!.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task DeleteAsync_AsNonCreator_ReturnsFalse()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var otherUser = await CreateTestUser("other@example.com");
        var evt = await CreateTestEvent(creator.Id);

        // Act
        var result = await _sut.DeleteAsync(evt.Id, otherUser.Id);

        // Assert
        result.Should().BeFalse();

        // Verify event wasn't deleted
        var unchanged = await _context.Events.FindAsync(evt.Id);
        unchanged!.Status.Should().Be("Published");
    }

    #endregion

    #region Query Filter Tests

    [Fact]
    public async Task GetAllAsync_ExcludesCancelledEvents()
    {
        // Arrange
        var creator = await CreateTestUser();
        await CreateTestEvent(creator.Id, name: "Active Event", status: "Published");
        await CreateTestEvent(creator.Id, name: "Cancelled Event", status: "Cancelled");

        // Act
        var result = await _sut.GetAllAsync(creator.Id);

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("Active Event");
    }

    [Fact]
    public async Task GetAllAsync_ExcludesPastEvents()
    {
        // Arrange
        var creator = await CreateTestUser();
        await CreateTestEvent(creator.Id, name: "Future Event", eventDate: DateTime.UtcNow.AddDays(7));
        await CreateTestEvent(creator.Id, name: "Past Event", eventDate: DateTime.UtcNow.AddDays(-1));

        // Act
        var result = await _sut.GetAllAsync(creator.Id);

        // Assert
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("Future Event");
    }

    [Fact]
    public async Task GetAllAsync_ReturnsCorrectRegisteredCount()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user1 = await CreateTestUser("user1@example.com");
        var user2 = await CreateTestUser("user2@example.com");
        var user3 = await CreateTestUser("user3@example.com");

        var evt = await CreateTestEvent(creator.Id);
        await CreateRegistration(evt.Id, user1.Id, "Registered");
        await CreateRegistration(evt.Id, user2.Id, "Registered");
        await CreateRegistration(evt.Id, user3.Id, "Cancelled"); // Cancelled shouldn't count

        // Act
        var result = await _sut.GetAllAsync(creator.Id);

        // Assert
        result.Should().HaveCount(1);
        result.First().RegisteredCount.Should().Be(2);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsCorrectIsRegisteredStatus()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var evt = await CreateTestEvent(creator.Id);
        await CreateRegistration(evt.Id, user.Id, "Registered");

        // Act
        var result = await _sut.GetAllAsync(user.Id);

        // Assert
        result.Should().HaveCount(1);
        result.First().IsRegistered.Should().BeTrue();
    }

    #endregion

    #region Standalone Event Tests

    [Fact]
    public async Task CreateAsync_WithoutOrganization_CreatesStandaloneEvent()
    {
        // Arrange
        var creator = await CreateTestUser();
        var request = new CreateEventRequest(
            OrganizationId: null,
            Name: "Pickup Game",
            Description: "Casual hockey",
            EventDate: DateTime.UtcNow.AddDays(7),
            Duration: 90,
            Venue: "Local Rink",
            MaxPlayers: 12,
            Cost: 10.00m,
            RegistrationDeadline: null,
            Visibility: "Public"
        );

        // Act
        var result = await _sut.CreateAsync(request, creator.Id);

        // Assert
        result.OrganizationId.Should().BeNull();
        result.OrganizationName.Should().BeNull();
        result.Name.Should().Be("Pickup Game");
    }

    [Fact]
    public async Task CreateAsync_WithOrganization_IncludesOrganizationName()
    {
        // Arrange
        var creator = await CreateTestUser();
        var org = await CreateTestOrganization(creator.Id, "Hockey Club");
        var request = new CreateEventRequest(
            OrganizationId: org.Id,
            Name: "League Game",
            Description: null,
            EventDate: DateTime.UtcNow.AddDays(7),
            Duration: 60,
            Venue: null,
            MaxPlayers: 20,
            Cost: 0,
            RegistrationDeadline: null,
            Visibility: "Public"
        );

        // Act
        var result = await _sut.CreateAsync(request, creator.Id);

        // Assert
        result.OrganizationId.Should().Be(org.Id);
        result.OrganizationName.Should().Be("Hockey Club");
    }

    #endregion
}
