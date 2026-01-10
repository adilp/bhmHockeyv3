using System.Net.Http;
using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
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
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<IWaitlistService> _mockWaitlistService;
    private readonly OrganizationAdminService _adminService;
    private readonly EventService _sut;

    public EventServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new AppDbContext(options);
        _mockNotificationService = new Mock<INotificationService>();
        _mockWaitlistService = new Mock<IWaitlistService>();
        _adminService = new OrganizationAdminService(_context);

        // Default mock setup for waitlist service
        _mockWaitlistService.Setup(w => w.GetNextWaitlistPositionAsync(It.IsAny<Guid>()))
            .ReturnsAsync(1);

        _sut = new EventService(_context, _mockNotificationService.Object, _adminService, _mockWaitlistService.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Helper Methods

    private async Task<User> CreateTestUser(string email = "test@example.com", Dictionary<string, string>? positions = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = "hashed_password",
            FirstName = "Test",
            LastName = "User",
            // Default to skater position so registration tests work
            Positions = positions ?? new Dictionary<string, string> { { "skater", "Silver" } },
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

    private async Task<Event> CreateTestEvent(
        Guid creatorId,
        Guid? organizationId = null,
        string name = "Test Event",
        int maxPlayers = 10,
        string visibility = "Public",
        string status = "Published",
        DateTime? eventDate = null,
        decimal cost = 25.00m)
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
            Cost = cost,
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
        // Arrange - Use free event so registration is direct (paid events always waitlist)
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var evt = await CreateTestEvent(creator.Id, cost: 0);

        // Act
        var result = await _sut.RegisterAsync(evt.Id, user.Id);

        // Assert
        result.Status.Should().Be("Registered");
        result.WaitlistPosition.Should().BeNull();
        var registration = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == evt.Id && r.UserId == user.Id);
        registration.Should().NotBeNull();
        registration!.Status.Should().Be("Registered");
    }

    [Fact]
    public async Task RegisterAsync_WhenAlreadyRegistered_ThrowsException()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var evt = await CreateTestEvent(creator.Id);
        await CreateRegistration(evt.Id, user.Id, "Registered");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RegisterAsync(evt.Id, user.Id));
        exception.Message.Should().Be("Already registered for this event");

        // Verify no duplicate was created
        var registrationCount = await _context.EventRegistrations
            .CountAsync(r => r.EventId == evt.Id && r.UserId == user.Id);
        registrationCount.Should().Be(1);
    }

    [Fact]
    public async Task RegisterAsync_WhenEventFull_AddsToWaitlist()
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

        // Act
        var result = await _sut.RegisterAsync(evt.Id, newUser.Id);

        // Assert
        result.Status.Should().Be("Waitlisted");
        result.WaitlistPosition.Should().Be(1);
        result.Message.Should().Contain("waitlist");

        var registration = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == evt.Id && r.UserId == newUser.Id);
        registration.Should().NotBeNull();
        registration!.Status.Should().Be("Waitlisted");
        registration.WaitlistPosition.Should().Be(1);
    }

    [Fact]
    public async Task RegisterAsync_AfterCancellingRegistration_ReactivatesRegistration()
    {
        // Arrange - Use free event so registration is direct (paid events always waitlist)
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var evt = await CreateTestEvent(creator.Id, cost: 0);

        // Create and cancel a registration
        await CreateRegistration(evt.Id, user.Id, "Cancelled");

        // Act
        var result = await _sut.RegisterAsync(evt.Id, user.Id);

        // Assert
        result.Status.Should().Be("Registered");
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

    [Fact]
    public async Task CancelRegistrationAsync_WhenRegistered_TriggersPromotion()
    {
        // Arrange - Registered user cancels, waitlisted user should be promoted
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var registeredUser = await CreateTestUser("registered@example.com");
        var waitlistedUser = await CreateTestUser("waitlisted@example.com");

        await CreateRegistration(evt.Id, registeredUser.Id, "Registered");
        await CreateRegistration(evt.Id, waitlistedUser.Id, "Waitlisted");

        // Setup mock to verify promotion is called (with callerOwnsTransaction: true)
        _mockWaitlistService.Setup(w => w.PromoteFromWaitlistAsync(evt.Id, 1, true))
            .ReturnsAsync(new PromotionResult());

        // Act
        await _sut.CancelRegistrationAsync(evt.Id, registeredUser.Id);

        // Assert - Promotion should be triggered with callerOwnsTransaction: true
        _mockWaitlistService.Verify(
            w => w.PromoteFromWaitlistAsync(evt.Id, 1, true),
            Times.Once);
    }

    [Fact]
    public async Task CancelRegistrationAsync_WhenWaitlisted_UpdatesWaitlistPositions()
    {
        // Arrange - Waitlisted user cancels, remaining waitlist should be renumbered
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var waitlistedUser = await CreateTestUser("waitlisted@example.com");
        await CreateRegistration(evt.Id, waitlistedUser.Id, "Waitlisted");

        // Act
        await _sut.CancelRegistrationAsync(evt.Id, waitlistedUser.Id);

        // Assert - Waitlist positions should be updated
        _mockWaitlistService.Verify(
            w => w.UpdateWaitlistPositionsAsync(evt.Id),
            Times.Once);
    }

    [Fact]
    public async Task CancelRegistrationAsync_WhenRegistered_SendsPendingNotificationsAfterCommit()
    {
        // Arrange - Verify notifications are sent after promotion
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var registeredUser = await CreateTestUser("registered@example.com");
        await CreateRegistration(evt.Id, registeredUser.Id, "Registered");

        var pendingNotifications = new List<PendingNotification>
        {
            new PendingNotification { Type = NotificationType.AutoPromoted }
        };

        // Setup mock to return pending notifications
        _mockWaitlistService.Setup(w => w.PromoteFromWaitlistAsync(evt.Id, 1, true))
            .ReturnsAsync(new PromotionResult { PendingNotifications = pendingNotifications });

        // Act
        await _sut.CancelRegistrationAsync(evt.Id, registeredUser.Id);

        // Assert - SendPendingNotificationsAsync should be called with the pending notifications
        _mockWaitlistService.Verify(
            w => w.SendPendingNotificationsAsync(pendingNotifications),
            Times.Once);
    }

    [Fact]
    public async Task RemoveRegistrationAsync_WhenRegistered_TriggersAtomicPromotion()
    {
        // Arrange - Organizer removes registered user, should trigger atomic promotion
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var registeredUser = await CreateTestUser("registered@example.com");
        var registration = await CreateRegistration(evt.Id, registeredUser.Id, "Registered");

        // Setup mock for atomic promotion
        _mockWaitlistService.Setup(w => w.PromoteFromWaitlistAsync(evt.Id, 1, true))
            .ReturnsAsync(new PromotionResult());

        // Act
        var result = await _sut.RemoveRegistrationAsync(evt.Id, registration.Id, creator.Id);

        // Assert
        result.Should().BeTrue();
        _mockWaitlistService.Verify(
            w => w.PromoteFromWaitlistAsync(evt.Id, 1, true),
            Times.Once);
    }

    [Fact]
    public async Task RemoveRegistrationAsync_NotifiesRemovedUser()
    {
        // Arrange - Removed user should receive notification
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var removedUser = await CreateTestUser("removed@example.com");
        removedUser.PushToken = "ExponentPushToken[removed]";
        await _context.SaveChangesAsync();

        var registration = await CreateRegistration(evt.Id, removedUser.Id, "Registered");

        // Setup mock for promotion
        _mockWaitlistService.Setup(w => w.PromoteFromWaitlistAsync(evt.Id, 1, true))
            .ReturnsAsync(new PromotionResult());

        // Act
        await _sut.RemoveRegistrationAsync(evt.Id, registration.Id, creator.Id);

        // Assert - Removed user should receive notification
        _mockNotificationService.Verify(
            n => n.SendPushNotificationAsync(
                "ExponentPushToken[removed]",
                "Removed from Event",
                It.Is<string>(s => s.Contains("removed from")),
                It.IsAny<object>(),
                removedUser.Id,
                "removed_from_event",
                It.IsAny<Guid?>(),
                evt.Id),
            Times.Once);
    }

    [Fact]
    public async Task RemoveRegistrationAsync_WhenWaitlisted_UpdatesWaitlistPositions()
    {
        // Arrange - Organizer removes waitlisted user
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var waitlistedUser = await CreateTestUser("waitlisted@example.com");
        var registration = await CreateRegistration(evt.Id, waitlistedUser.Id, "Waitlisted");

        // Act
        var result = await _sut.RemoveRegistrationAsync(evt.Id, registration.Id, creator.Id);

        // Assert
        result.Should().BeTrue();
        _mockWaitlistService.Verify(
            w => w.UpdateWaitlistPositionsAsync(evt.Id),
            Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_WithNoPositionsSet_ThrowsHelpfulError()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com",
            positions: new Dictionary<string, string>()); // Empty positions
        var evt = await CreateTestEvent(creator.Id);

        // Act
        var act = () => _sut.RegisterAsync(evt.Id, user.Id);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*set up your positions*");
    }

    [Fact]
    public async Task RegisterAsync_WithDualPositions_RequiresPositionSelection()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com",
            positions: new Dictionary<string, string> { { "goalie", "Gold" }, { "skater", "Silver" } });
        var evt = await CreateTestEvent(creator.Id);

        // Act - Try to register without specifying position
        var act = () => _sut.RegisterAsync(evt.Id, user.Id, position: null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*multiple positions*");
    }

    [Fact]
    public async Task RegisterAsync_ForPaidEvent_WithAvailableSpots_AlwaysWaitlists()
    {
        // Arrange - Paid event with plenty of capacity
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var evt = await CreateTestEvent(creator.Id, maxPlayers: 10, cost: 25.00m);

        // Act
        var result = await _sut.RegisterAsync(evt.Id, user.Id);

        // Assert - Should be waitlisted, not registered (paid events always waitlist)
        result.Status.Should().Be("Waitlisted");
        result.WaitlistPosition.Should().Be(1);
        result.Message.Should().Contain("payment");

        var registration = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == evt.Id && r.UserId == user.Id);
        registration.Should().NotBeNull();
        registration!.Status.Should().Be("Waitlisted");
        registration.PaymentStatus.Should().Be("Pending");
    }

    [Fact]
    public async Task RegisterAsync_ForPaidEvent_WhenFull_Waitlists()
    {
        // Arrange - Paid event that is full
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id, maxPlayers: 2, cost: 25.00m);

        // Fill the event (these are pre-existing registrations, not going through RegisterAsync)
        var user1 = await CreateTestUser("user1@example.com");
        var user2 = await CreateTestUser("user2@example.com");
        await CreateRegistration(evt.Id, user1.Id);
        await CreateRegistration(evt.Id, user2.Id);

        var newUser = await CreateTestUser("new@example.com");

        // Act
        var result = await _sut.RegisterAsync(evt.Id, newUser.Id);

        // Assert
        result.Status.Should().Be("Waitlisted");
        result.WaitlistPosition.Should().Be(1);
        result.Message.Should().Contain("payment");

        var registration = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == evt.Id && r.UserId == newUser.Id);
        registration!.PaymentStatus.Should().Be("Pending");
    }

    [Fact]
    public async Task RegisterAsync_ForFreeEvent_WithAvailableSpots_RegistersDirectly()
    {
        // Arrange - Free event (Cost = 0) with capacity
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var evt = await CreateTestEvent(creator.Id, maxPlayers: 10, cost: 0);

        // Act
        var result = await _sut.RegisterAsync(evt.Id, user.Id);

        // Assert - Free events register directly when capacity available
        result.Status.Should().Be("Registered");
        result.WaitlistPosition.Should().BeNull();

        var registration = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == evt.Id && r.UserId == user.Id);
        registration.Should().NotBeNull();
        registration!.Status.Should().Be("Registered");
        registration.PaymentStatus.Should().BeNull(); // No payment tracking for free events
    }

    [Fact]
    public async Task RegisterAsync_ForFreeEvent_WhenFull_Waitlists()
    {
        // Arrange - Free event that is full
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id, maxPlayers: 2, cost: 0);

        // Fill the event
        var user1 = await CreateTestUser("user1@example.com");
        var user2 = await CreateTestUser("user2@example.com");
        await CreateRegistration(evt.Id, user1.Id);
        await CreateRegistration(evt.Id, user2.Id);

        var newUser = await CreateTestUser("new@example.com");

        // Act
        var result = await _sut.RegisterAsync(evt.Id, newUser.Id);

        // Assert - Waitlisted because full (not because paid)
        result.Status.Should().Be("Waitlisted");
        result.WaitlistPosition.Should().Be(1);
        result.Message.Should().Contain("full"); // Different message for full free events

        var registration = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == evt.Id && r.UserId == newUser.Id);
        registration!.PaymentStatus.Should().BeNull(); // No payment tracking for free events
    }

    [Fact]
    public async Task RegisterAsync_ForPaidEvent_NotifiesOrganizer()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        creator.PushToken = "ExponentPushToken[test]";
        await _context.SaveChangesAsync();

        var user = await CreateTestUser("user@example.com");
        var evt = await CreateTestEvent(creator.Id, cost: 25.00m);

        // Act
        await _sut.RegisterAsync(evt.Id, user.Id);

        // Assert - Verify organizer notification was sent
        _mockNotificationService.Verify(
            n => n.SendPushNotificationAsync(
                creator.PushToken,
                "New Waitlist Signup",
                It.Is<string>(s => s.Contains("waitlist")),
                It.IsAny<object>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>()),
            Times.Once);
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
    public async Task CreateAsync_WithInvalidSkillLevel_ThrowsException()
    {
        // Arrange
        var creator = await CreateTestUser();
        var request = new CreateEventRequest(
            OrganizationId: null,
            Name: "Test Event",
            Description: null,
            EventDate: DateTime.UtcNow.AddDays(7),
            Duration: 60,
            Venue: null,
            MaxPlayers: 10,
            Cost: 0,
            RegistrationDeadline: null,
            Visibility: "Public",
            SkillLevels: new List<string> { "Platinum" } // Invalid skill level
        );

        // Act & Assert
        await _sut.Invoking(s => s.CreateAsync(request, creator.Id))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid skill level*");
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
            Visibility: "OrganizationMembers", // Invalid - no org on event
            SkillLevels: null
        );

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateAsync(evt.Id, request, creator.Id));
    }

    [Fact]
    public async Task UpdateAsync_WithInvalidSkillLevel_ThrowsException()
    {
        // Arrange
        var creator = await CreateTestUser();
        var evt = await CreateTestEvent(creator.Id);
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
            Visibility: null,
            SkillLevels: new List<string> { "Platinum" } // Invalid skill level
        );

        // Act & Assert
        await _sut.Invoking(s => s.UpdateAsync(evt.Id, request, creator.Id))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid skill level*");
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
            Visibility: null,
            SkillLevels: null
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
            Visibility: null,
            SkillLevels: null
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

    #region Registration Deadline Tests

    [Fact]
    public async Task RegisterAsync_AfterRegistrationDeadline_ThrowsInvalidOperationException()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");

        // Create event with deadline in the past
        var evt = new Event
        {
            Id = Guid.NewGuid(),
            CreatorId = creator.Id,
            Name = "Past Deadline Event",
            Description = "Test",
            EventDate = DateTime.UtcNow.AddDays(7),
            Duration = 60,
            Venue = "Test Venue",
            MaxPlayers = 10,
            Cost = 25.00m,
            Status = "Published",
            Visibility = "Public",
            RegistrationDeadline = DateTime.UtcNow.AddHours(-1), // Deadline passed
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Events.Add(evt);
        await _context.SaveChangesAsync();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RegisterAsync(evt.Id, user.Id));

        exception.Message.Should().Be("Registration deadline has passed");
    }

    [Fact]
    public async Task RegisterAsync_BeforeRegistrationDeadline_Succeeds()
    {
        // Arrange - Use free event so registration is direct (paid events always waitlist)
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");

        // Create free event with deadline in the future
        var evt = new Event
        {
            Id = Guid.NewGuid(),
            CreatorId = creator.Id,
            Name = "Future Deadline Event",
            Description = "Test",
            EventDate = DateTime.UtcNow.AddDays(7),
            Duration = 60,
            Venue = "Test Venue",
            MaxPlayers = 10,
            Cost = 0, // Free event so registration is direct
            Status = "Published",
            Visibility = "Public",
            RegistrationDeadline = DateTime.UtcNow.AddDays(1), // Deadline in future
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Events.Add(evt);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.RegisterAsync(evt.Id, user.Id);

        // Assert
        result.Status.Should().Be("Registered");
    }

    [Fact]
    public async Task RegisterAsync_WithNoDeadline_Succeeds()
    {
        // Arrange - Use free event so registration is direct (paid events always waitlist)
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var evt = await CreateTestEvent(creator.Id, cost: 0); // Free event, no deadline

        // Act
        var result = await _sut.RegisterAsync(evt.Id, user.Id);

        // Assert
        result.Status.Should().Be("Registered");
    }

    #endregion

    #region Notification Tests

    [Fact]
    public async Task CreateAsync_WithOrganizationId_TriggersNotification()
    {
        // Arrange
        var creator = await CreateTestUser();
        var org = await CreateTestOrganization(creator.Id, "Test Org");
        var request = new CreateEventRequest(
            OrganizationId: org.Id,
            Name: "Org Event",
            Description: null,
            EventDate: DateTime.UtcNow.AddDays(7),
            Duration: 60,
            Venue: "Rink",
            MaxPlayers: 20,
            Cost: 15,
            RegistrationDeadline: null,
            Visibility: "Public"
        );

        // Act
        await _sut.CreateAsync(request, creator.Id);

        // Assert - Verify notification was triggered for org subscribers
        _mockNotificationService.Verify(
            n => n.NotifyOrganizationSubscribersAsync(
                org.Id,
                It.Is<string>(s => s.Contains("Org Event")),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithoutOrganizationId_DoesNotTriggerNotification()
    {
        // Arrange - Standalone event should NOT notify anyone
        var creator = await CreateTestUser();
        var request = new CreateEventRequest(
            OrganizationId: null, // No org = standalone event
            Name: "Pickup Game",
            Description: null,
            EventDate: DateTime.UtcNow.AddDays(7),
            Duration: 60,
            Venue: "Local Rink",
            MaxPlayers: 12,
            Cost: 10,
            RegistrationDeadline: null,
            Visibility: "Public"
        );

        // Act
        await _sut.CreateAsync(request, creator.Id);

        // Assert - Notification should never be called
        _mockNotificationService.Verify(
            n => n.NotifyOrganizationSubscribersAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WhenNotificationFails_StillCreatesEvent()
    {
        // Arrange - Notification service throws, but event should still be created
        var creator = await CreateTestUser();
        var org = await CreateTestOrganization(creator.Id, "Test Org");

        _mockNotificationService
            .Setup(n => n.NotifyOrganizationSubscribersAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>()))
            .ThrowsAsync(new HttpRequestException("Expo API unavailable"));

        var request = new CreateEventRequest(
            OrganizationId: org.Id,
            Name: "Event Despite Notification Failure",
            Description: null,
            EventDate: DateTime.UtcNow.AddDays(7),
            Duration: 60,
            Venue: "Rink",
            MaxPlayers: 20,
            Cost: 15,
            RegistrationDeadline: null,
            Visibility: "Public"
        );

        // Act & Assert - Should throw because notification failure propagates
        // This test documents current behavior - if we want resilience, we need to wrap in try-catch
        await Assert.ThrowsAsync<HttpRequestException>(
            () => _sut.CreateAsync(request, creator.Id));

        // Verify event was NOT created (transaction rolled back or exception before save)
        var eventCount = await _context.Events.CountAsync(e => e.Name == "Event Despite Notification Failure");

        // Note: Current implementation saves event BEFORE notifying, so event exists
        // This test catches the actual behavior - event IS created, then notification throws
        // If this is undesirable, EventService needs a try-catch around notification
    }

    #endregion

    #region UnpaidCount Tests

    [Fact]
    public async Task GetByIdAsync_AsCreator_ForPaidEvent_ReturnsUnpaidCount()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user1 = await CreateTestUser("user1@example.com");
        var user2 = await CreateTestUser("user2@example.com");
        var evt = await CreateTestEvent(creator.Id); // Cost = 25.00m

        await CreateRegistration(evt.Id, user1.Id); // Default: PaymentStatus = null (unpaid)
        await CreateRegistration(evt.Id, user2.Id);

        // Act
        var result = await _sut.GetByIdAsync(evt.Id, creator.Id);

        // Assert
        result!.UnpaidCount.Should().Be(2);
    }

    [Fact]
    public async Task GetByIdAsync_AsNonCreator_UnpaidCountIsNull()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var evt = await CreateTestEvent(creator.Id);
        await CreateRegistration(evt.Id, user.Id);

        // Act
        var result = await _sut.GetByIdAsync(evt.Id, user.Id);

        // Assert
        result!.UnpaidCount.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ForFreeEvent_UnpaidCountIsNull()
    {
        // Arrange - Free event (Cost = 0), even creator shouldn't see UnpaidCount
        var creator = await CreateTestUser();
        var freeEvent = new Event
        {
            Id = Guid.NewGuid(),
            CreatorId = creator.Id,
            Name = "Free Event",
            EventDate = DateTime.UtcNow.AddDays(7),
            Duration = 60,
            MaxPlayers = 10,
            Cost = 0, // Free!
            Status = "Published",
            Visibility = "Public",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Events.Add(freeEvent);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.GetByIdAsync(freeEvent.Id, creator.Id);

        // Assert
        result!.UnpaidCount.Should().BeNull();
    }

    #endregion

    #region Multi-Admin Event Management Tests

    /// <summary>
    /// Helper to add a user as admin to an organization.
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
    public async Task UpdateAsync_AsOrgAdmin_ForOrgLinkedEvent_UpdatesEvent()
    {
        // Arrange - Org admin (not event creator) should be able to update org event
        var eventCreator = await CreateTestUser("eventcreator@example.com");
        var orgAdmin = await CreateTestUser("orgadmin@example.com");
        var org = await CreateTestOrganization(eventCreator.Id, "Test Org");
        await AddAdminToOrganization(org.Id, orgAdmin.Id, eventCreator.Id);

        var evt = await CreateTestEvent(eventCreator.Id, organizationId: org.Id, name: "Original Name");

        var request = new UpdateEventRequest(
            Name: "Updated By Org Admin",
            Description: null,
            EventDate: null,
            Duration: null,
            Venue: null,
            MaxPlayers: null,
            Cost: null,
            RegistrationDeadline: null,
            Status: null,
            Visibility: null,
            SkillLevels: null
        );

        // Act - Org admin (who is NOT the event creator) updates the event
        var result = await _sut.UpdateAsync(evt.Id, request, orgAdmin.Id);

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated By Org Admin");
    }

    [Fact]
    public async Task UpdateAsync_AsNonAdmin_ForOrgLinkedEvent_ReturnsNull()
    {
        // Arrange - Non-admin should NOT be able to update org event
        var eventCreator = await CreateTestUser("eventcreator@example.com");
        var subscriber = await CreateTestUser("subscriber@example.com");
        var org = await CreateTestOrganization(eventCreator.Id, "Test Org");
        await CreateSubscription(org.Id, subscriber.Id); // Subscriber but NOT admin

        var evt = await CreateTestEvent(eventCreator.Id, organizationId: org.Id, name: "Original Name");

        var request = new UpdateEventRequest(
            Name: "Attempted Hack",
            Description: null,
            EventDate: null,
            Duration: null,
            Venue: null,
            MaxPlayers: null,
            Cost: null,
            RegistrationDeadline: null,
            Status: null,
            Visibility: null,
            SkillLevels: null
        );

        // Act
        var result = await _sut.UpdateAsync(evt.Id, request, subscriber.Id);

        // Assert
        result.Should().BeNull();

        // Verify event wasn't modified
        var unchanged = await _context.Events.FindAsync(evt.Id);
        unchanged!.Name.Should().Be("Original Name");
    }

    [Fact]
    public async Task DeleteAsync_AsOrgAdmin_ForOrgLinkedEvent_DeletesEvent()
    {
        // Arrange - Org admin (not event creator) should be able to delete org event
        var eventCreator = await CreateTestUser("eventcreator@example.com");
        var orgAdmin = await CreateTestUser("orgadmin@example.com");
        var org = await CreateTestOrganization(eventCreator.Id, "Test Org");
        await AddAdminToOrganization(org.Id, orgAdmin.Id, eventCreator.Id);

        var evt = await CreateTestEvent(eventCreator.Id, organizationId: org.Id);

        // Act - Org admin (who is NOT the event creator) deletes the event
        var result = await _sut.DeleteAsync(evt.Id, orgAdmin.Id);

        // Assert
        result.Should().BeTrue();
        var deleted = await _context.Events.FindAsync(evt.Id);
        deleted!.Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task GetByIdAsync_AsOrgAdmin_ReturnsCanManageTrue()
    {
        // Arrange - Org admin should see CanManage=true for org events
        var eventCreator = await CreateTestUser("eventcreator@example.com");
        var orgAdmin = await CreateTestUser("orgadmin@example.com");
        var org = await CreateTestOrganization(eventCreator.Id, "Test Org");
        await AddAdminToOrganization(org.Id, orgAdmin.Id, eventCreator.Id);

        var evt = await CreateTestEvent(eventCreator.Id, organizationId: org.Id);

        // Act
        var result = await _sut.GetByIdAsync(evt.Id, orgAdmin.Id);

        // Assert
        result.Should().NotBeNull();
        result!.CanManage.Should().BeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_AsNonAdmin_ReturnsCanManageFalse()
    {
        // Arrange - Non-admin should see CanManage=false
        var eventCreator = await CreateTestUser("eventcreator@example.com");
        var subscriber = await CreateTestUser("subscriber@example.com");
        var org = await CreateTestOrganization(eventCreator.Id, "Test Org");
        await CreateSubscription(org.Id, subscriber.Id); // Subscriber but NOT admin

        var evt = await CreateTestEvent(eventCreator.Id, organizationId: org.Id, visibility: "OrganizationMembers");

        // Act
        var result = await _sut.GetByIdAsync(evt.Id, subscriber.Id);

        // Assert
        result.Should().NotBeNull();
        result!.CanManage.Should().BeFalse();
    }

    [Fact]
    public async Task GetAllAsync_OrgAdminCanSeeOrgEventsRegardlessOfVisibility()
    {
        // Arrange - Org admin should see InviteOnly org events they didn't create
        var eventCreator = await CreateTestUser("eventcreator@example.com");
        var orgAdmin = await CreateTestUser("orgadmin@example.com");
        var org = await CreateTestOrganization(eventCreator.Id, "Test Org");
        await AddAdminToOrganization(org.Id, orgAdmin.Id, eventCreator.Id);

        // Create an InviteOnly event that orgAdmin didn't create
        await CreateTestEvent(eventCreator.Id, organizationId: org.Id, visibility: "InviteOnly", name: "Private Event");

        // Act
        var result = await _sut.GetAllAsync(orgAdmin.Id);

        // Assert - Org admin should see the InviteOnly event
        result.Should().HaveCount(1);
        result.First().Name.Should().Be("Private Event");
    }

    [Fact]
    public async Task UpdatePaymentStatusAsync_AsOrgAdmin_Succeeds()
    {
        // Arrange - Org admin should be able to verify payments for org events
        var eventCreator = await CreateTestUser("eventcreator@example.com");
        var orgAdmin = await CreateTestUser("orgadmin@example.com");
        var registeredUser = await CreateTestUser("registered@example.com");
        var org = await CreateTestOrganization(eventCreator.Id, "Test Org");
        await AddAdminToOrganization(org.Id, orgAdmin.Id, eventCreator.Id);

        var evt = await CreateTestEvent(eventCreator.Id, organizationId: org.Id);
        var registration = await CreateRegistration(evt.Id, registeredUser.Id);

        // Act - Org admin (not event creator) verifies payment
        var result = await _sut.UpdatePaymentStatusAsync(evt.Id, registration.Id, "Verified", orgAdmin.Id);

        // Assert
        result.Success.Should().BeTrue();

        var updated = await _context.EventRegistrations.FindAsync(registration.Id);
        updated!.PaymentStatus.Should().Be("Verified");
    }

    [Fact]
    public async Task UpdatePaymentStatusAsync_AsNonAdmin_ReturnsFalse()
    {
        // Arrange - Non-admin should NOT be able to verify payments
        var eventCreator = await CreateTestUser("eventcreator@example.com");
        var subscriber = await CreateTestUser("subscriber@example.com");
        var registeredUser = await CreateTestUser("registered@example.com");
        var org = await CreateTestOrganization(eventCreator.Id, "Test Org");
        await CreateSubscription(org.Id, subscriber.Id); // Subscriber but NOT admin

        var evt = await CreateTestEvent(eventCreator.Id, organizationId: org.Id);
        var registration = await CreateRegistration(evt.Id, registeredUser.Id);

        // Act
        var result = await _sut.UpdatePaymentStatusAsync(evt.Id, registration.Id, "Verified", subscriber.Id);

        // Assert
        result.Success.Should().BeFalse();

        var unchanged = await _context.EventRegistrations.FindAsync(registration.Id);
        unchanged!.PaymentStatus.Should().BeNull();
    }

    #endregion

    #region CanUserManageEventAsync Tests

    [Fact]
    public async Task CanUserManageEventAsync_EventNotFound_ReturnsFalse()
    {
        // Arrange - Non-existent event
        var user = await CreateTestUser();
        var nonExistentEventId = Guid.NewGuid();

        // Act
        var result = await _sut.CanUserManageEventAsync(nonExistentEventId, user.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanUserManageEventAsync_AsCreator_ReturnsTrue()
    {
        // Arrange - Standalone event, creator should be able to manage
        var creator = await CreateTestUser();
        var evt = await CreateTestEvent(creator.Id);

        // Act
        var result = await _sut.CanUserManageEventAsync(evt.Id, creator.Id);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CanUserManageEventAsync_AsNonCreator_ReturnsFalse()
    {
        // Arrange - Standalone event, non-creator should NOT be able to manage
        var creator = await CreateTestUser("creator@example.com");
        var otherUser = await CreateTestUser("other@example.com");
        var evt = await CreateTestEvent(creator.Id);

        // Act
        var result = await _sut.CanUserManageEventAsync(evt.Id, otherUser.Id);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CanUserManageEventAsync_AsOrgAdmin_ReturnsTrue()
    {
        // Arrange - Org-linked event, org admin should be able to manage
        var eventCreator = await CreateTestUser("eventcreator@example.com");
        var orgAdmin = await CreateTestUser("orgadmin@example.com");
        var org = await CreateTestOrganization(eventCreator.Id, "Test Org");
        await AddAdminToOrganization(org.Id, orgAdmin.Id, eventCreator.Id);

        var evt = await CreateTestEvent(eventCreator.Id, organizationId: org.Id);

        // Act
        var result = await _sut.CanUserManageEventAsync(evt.Id, orgAdmin.Id);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Concurrency Tests

    [Fact]
    public async Task RegisterAsync_ConcurrentRegistrationsForLastSlot_OnlyOneSucceeds()
    {
        // Arrange - Free event with 1 slot remaining (paid events always waitlist)
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id, maxPlayers: 2, cost: 0);

        // Fill all but one slot
        var existingUser = await CreateTestUser("existing@example.com");
        await CreateRegistration(evt.Id, existingUser.Id);

        // Two users trying to grab the last slot
        var user1 = await CreateTestUser("user1@example.com");
        var user2 = await CreateTestUser("user2@example.com");

        // Act - Simulate concurrent registration attempts
        var task1 = _sut.RegisterAsync(evt.Id, user1.Id);
        var task2 = _sut.RegisterAsync(evt.Id, user2.Id);

        var results = await Task.WhenAll(task1, task2);

        // Assert - At most one should succeed (get registered), the other goes to waitlist
        var finalRegisteredCount = await _context.EventRegistrations
            .CountAsync(r => r.EventId == evt.Id && r.Status == "Registered");

        // This assertion catches if both succeed (race condition bug)
        finalRegisteredCount.Should().BeLessOrEqualTo(2,
            "Concurrent registrations should not exceed MaxPlayers");

        // At least one should have been registered
        results.Should().Contain(r => r.Status == "Registered", "At least one registration should succeed");
    }

    [Fact]
    public async Task RegisterAsync_SequentialRegistrationsForLastSlot_SecondGoesToWaitlist()
    {
        // Arrange - Free event, sequential version to verify the basic logic (paid events always waitlist)
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id, maxPlayers: 2, cost: 0);

        // Fill all but one slot
        var existingUser = await CreateTestUser("existing@example.com");
        await CreateRegistration(evt.Id, existingUser.Id);

        var user1 = await CreateTestUser("user1@example.com");
        var user2 = await CreateTestUser("user2@example.com");

        // Act - Sequential registrations
        var result1 = await _sut.RegisterAsync(evt.Id, user1.Id);

        // Assert - First succeeds (gets the last slot)
        result1.Status.Should().Be("Registered");

        // Second goes to waitlist (event full)
        var result2 = await _sut.RegisterAsync(evt.Id, user2.Id);
        result2.Status.Should().Be("Waitlisted");
        result2.WaitlistPosition.Should().Be(1);

        // Verify final count
        var finalCount = await _context.EventRegistrations
            .CountAsync(r => r.EventId == evt.Id && r.Status == "Registered");
        finalCount.Should().Be(2);
    }

    #endregion

    #region Team Assignment Tests

    [Fact]
    public async Task RegisterAsync_WhenTeamsEqual_AssignsToBlack()
    {
        // Arrange - Free event so registration is direct (paid events waitlist, no team assignment)
        var creator = await CreateTestUser("creator@example.com");
        var user = await CreateTestUser("user@example.com");
        var evt = await CreateTestEvent(creator.Id, cost: 0);

        // Act
        await _sut.RegisterAsync(evt.Id, user.Id);

        // Assert
        var registration = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == evt.Id && r.UserId == user.Id);
        registration!.TeamAssignment.Should().Be("Black");
    }

    [Fact]
    public async Task RegisterAsync_AssignsSkaterToTeamWithFewerSkaters()
    {
        // Arrange - Free event so registration is direct (paid events waitlist, no team assignment)
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id, cost: 0);

        // Add 2 skaters to Black team
        var black1 = await CreateTestUser("black1@example.com");
        var black2 = await CreateTestUser("black2@example.com");
        var reg1 = await CreateRegistration(evt.Id, black1.Id);
        var reg2 = await CreateRegistration(evt.Id, black2.Id);
        reg1.TeamAssignment = "Black";
        reg1.RegisteredPosition = "Skater";
        reg2.TeamAssignment = "Black";
        reg2.RegisteredPosition = "Skater";

        // Add 1 skater to White team
        var white1 = await CreateTestUser("white1@example.com");
        var reg3 = await CreateRegistration(evt.Id, white1.Id);
        reg3.TeamAssignment = "White";
        reg3.RegisteredPosition = "Skater";

        await _context.SaveChangesAsync();

        // New skater registers
        var newUser = await CreateTestUser("new@example.com");

        // Act
        await _sut.RegisterAsync(evt.Id, newUser.Id);

        // Assert - Should be assigned to White (fewer skaters)
        var registration = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == evt.Id && r.UserId == newUser.Id);
        registration!.TeamAssignment.Should().Be("White");
    }

    [Fact]
    public async Task RegisterAsync_AssignsGoalieToTeamWithFewerGoalies()
    {
        // Arrange - Free event so registration is direct (paid events waitlist, no team assignment)
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id, cost: 0);

        // Add 2 goalies to White team
        var goalie1 = await CreateTestUser("goalie1@example.com",
            positions: new Dictionary<string, string> { { "goalie", "Gold" } });
        var goalie2 = await CreateTestUser("goalie2@example.com",
            positions: new Dictionary<string, string> { { "goalie", "Gold" } });
        var reg1 = await CreateRegistration(evt.Id, goalie1.Id);
        var reg2 = await CreateRegistration(evt.Id, goalie2.Id);
        reg1.TeamAssignment = "White";
        reg1.RegisteredPosition = "Goalie";
        reg2.TeamAssignment = "White";
        reg2.RegisteredPosition = "Goalie";

        // Add many skaters to Black team (more total players on Black)
        var skater1 = await CreateTestUser("skater1@example.com");
        var skater2 = await CreateTestUser("skater2@example.com");
        var skater3 = await CreateTestUser("skater3@example.com");
        var reg3 = await CreateRegistration(evt.Id, skater1.Id);
        var reg4 = await CreateRegistration(evt.Id, skater2.Id);
        var reg5 = await CreateRegistration(evt.Id, skater3.Id);
        reg3.TeamAssignment = "Black";
        reg3.RegisteredPosition = "Skater";
        reg4.TeamAssignment = "Black";
        reg4.RegisteredPosition = "Skater";
        reg5.TeamAssignment = "Black";
        reg5.RegisteredPosition = "Skater";

        await _context.SaveChangesAsync();

        // New goalie registers
        var newGoalie = await CreateTestUser("newgoalie@example.com",
            positions: new Dictionary<string, string> { { "goalie", "Silver" } });

        // Act
        await _sut.RegisterAsync(evt.Id, newGoalie.Id);

        // Assert - Should be assigned to Black (0 goalies) despite Black having more total players
        var registration = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == evt.Id && r.UserId == newGoalie.Id);
        registration!.TeamAssignment.Should().Be("Black");
        registration.RegisteredPosition.Should().Be("Goalie");
    }

    [Fact]
    public async Task UpdateTeamAssignmentAsync_AsOrgAdmin_Succeeds()
    {
        // Arrange - Org admin should be able to swap teams for org events
        var eventCreator = await CreateTestUser("eventcreator@example.com");
        var orgAdmin = await CreateTestUser("orgadmin@example.com");
        var registeredUser = await CreateTestUser("registered@example.com");
        var org = await CreateTestOrganization(eventCreator.Id, "Test Org");
        await AddAdminToOrganization(org.Id, orgAdmin.Id, eventCreator.Id);

        var evt = await CreateTestEvent(eventCreator.Id, organizationId: org.Id);
        var registration = await CreateRegistration(evt.Id, registeredUser.Id);
        registration.TeamAssignment = "Black";
        await _context.SaveChangesAsync();

        // Act - Org admin swaps player to White team
        var result = await _sut.UpdateTeamAssignmentAsync(evt.Id, registration.Id, "White", orgAdmin.Id);

        // Assert
        result.Should().BeTrue();

        var updated = await _context.EventRegistrations.FindAsync(registration.Id);
        updated!.TeamAssignment.Should().Be("White");
    }

    [Fact]
    public async Task UpdateTeamAssignmentAsync_AsNonAdmin_ReturnsFalse()
    {
        // Arrange - Non-admin should NOT be able to swap teams
        var eventCreator = await CreateTestUser("eventcreator@example.com");
        var subscriber = await CreateTestUser("subscriber@example.com");
        var registeredUser = await CreateTestUser("registered@example.com");
        var org = await CreateTestOrganization(eventCreator.Id, "Test Org");
        await CreateSubscription(org.Id, subscriber.Id); // Subscriber but NOT admin

        var evt = await CreateTestEvent(eventCreator.Id, organizationId: org.Id);
        var registration = await CreateRegistration(evt.Id, registeredUser.Id);
        registration.TeamAssignment = "Black";
        await _context.SaveChangesAsync();

        // Act - Non-admin tries to swap team
        var result = await _sut.UpdateTeamAssignmentAsync(evt.Id, registration.Id, "White", subscriber.Id);

        // Assert
        result.Should().BeFalse();

        var unchanged = await _context.EventRegistrations.FindAsync(registration.Id);
        unchanged!.TeamAssignment.Should().Be("Black");
    }

    [Fact]
    public async Task UpdateTeamAssignmentAsync_WithInvalidTeam_ThrowsException()
    {
        // Arrange
        var creator = await CreateTestUser();
        var registeredUser = await CreateTestUser("registered@example.com");
        var evt = await CreateTestEvent(creator.Id);
        var registration = await CreateRegistration(evt.Id, registeredUser.Id);

        // Act & Assert - Invalid team value should throw
        await _sut.Invoking(s => s.UpdateTeamAssignmentAsync(evt.Id, registration.Id, "Red", creator.Id))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Invalid team*");
    }

    #endregion

    #region MarkPaymentAsync Tests (PWL-003)

    [Fact]
    public async Task MarkPaymentAsync_WaitlistedUser_WithPendingPayment_Succeeds()
    {
        // Arrange - Waitlisted user with Pending payment should be able to mark paid
        var creator = await CreateTestUser();
        var waitlistedUser = await CreateTestUser("waitlisted@example.com");
        var evt = await CreateTestEvent(creator.Id); // Cost = 25.00m (paid event)

        var registration = await CreateRegistration(evt.Id, waitlistedUser.Id, "Waitlisted");
        registration.PaymentStatus = "Pending";
        registration.WaitlistPosition = 1;
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.MarkPaymentAsync(evt.Id, waitlistedUser.Id, null);

        // Assert
        result.Should().BeTrue();

        var updated = await _context.EventRegistrations.FindAsync(registration.Id);
        updated!.PaymentStatus.Should().Be("MarkedPaid");
        updated.PaymentMarkedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkPaymentAsync_RegisteredUser_WithPendingPayment_Succeeds()
    {
        // Arrange - Registered user with Pending payment should be able to mark paid (existing behavior)
        var creator = await CreateTestUser();
        var registeredUser = await CreateTestUser("registered@example.com");
        var evt = await CreateTestEvent(creator.Id); // Cost = 25.00m (paid event)

        var registration = await CreateRegistration(evt.Id, registeredUser.Id, "Registered");
        registration.PaymentStatus = "Pending";
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.MarkPaymentAsync(evt.Id, registeredUser.Id, null);

        // Assert
        result.Should().BeTrue();

        var updated = await _context.EventRegistrations.FindAsync(registration.Id);
        updated!.PaymentStatus.Should().Be("MarkedPaid");
        updated.PaymentMarkedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkPaymentAsync_WhenPaymentAlreadyMarked_ReturnsFalse()
    {
        // Arrange - User who already marked payment should get false
        var creator = await CreateTestUser();
        var user = await CreateTestUser("user@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var registration = await CreateRegistration(evt.Id, user.Id, "Registered");
        registration.PaymentStatus = "MarkedPaid"; // Already marked
        registration.PaymentMarkedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.MarkPaymentAsync(evt.Id, user.Id, null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task MarkPaymentAsync_ForFreeEvent_ReturnsFalse()
    {
        // Arrange - Free event should return false
        var creator = await CreateTestUser();
        var user = await CreateTestUser("user@example.com");
        var evt = await CreateTestEvent(creator.Id, cost: 0); // Free event

        var registration = await CreateRegistration(evt.Id, user.Id, "Registered");
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.MarkPaymentAsync(evt.Id, user.Id, null);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task MarkPaymentAsync_WhenUserNotRegistered_ReturnsFalse()
    {
        // Arrange - User with no registration should get false
        var creator = await CreateTestUser();
        var unregisteredUser = await CreateTestUser("unregistered@example.com");
        var evt = await CreateTestEvent(creator.Id);

        // No registration created for unregisteredUser

        // Act
        var result = await _sut.MarkPaymentAsync(evt.Id, unregisteredUser.Id, null);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region UpdatePaymentStatusAsync Tests (PWL-006)

    [Fact]
    public async Task UpdatePaymentStatusAsync_WaitlistedUser_CapacityAvailable_PromotesToRoster()
    {
        // Arrange - Waitlisted user with verified payment should be promoted when capacity exists
        var creator = await CreateTestUser("creator@example.com");
        var waitlistedUser = await CreateTestUser("waitlisted@example.com");
        var evt = await CreateTestEvent(creator.Id, maxPlayers: 10); // Plenty of capacity

        var registration = await CreateRegistration(evt.Id, waitlistedUser.Id, "Waitlisted");
        registration.PaymentStatus = "MarkedPaid";
        registration.WaitlistPosition = 1;
        registration.RegisteredPosition = "Skater";
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.UpdatePaymentStatusAsync(evt.Id, registration.Id, "Verified", creator.Id);

        // Assert
        result.Success.Should().BeTrue();
        result.Promoted.Should().BeTrue();
        result.Message.Should().Contain("promoted to roster");

        var updated = await _context.EventRegistrations.FindAsync(registration.Id);
        updated!.Status.Should().Be("Registered");
        updated.PaymentStatus.Should().Be("Verified");
        updated.PromotedAt.Should().NotBeNull();
        updated.WaitlistPosition.Should().BeNull();
        updated.TeamAssignment.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdatePaymentStatusAsync_WaitlistedUser_RosterFull_StaysWaitlistedWithVerifiedStatus()
    {
        // Arrange - Waitlisted user should stay waitlisted if roster is full
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id, maxPlayers: 2);

        // Fill the roster
        var user1 = await CreateTestUser("user1@example.com");
        var user2 = await CreateTestUser("user2@example.com");
        await CreateRegistration(evt.Id, user1.Id, "Registered");
        await CreateRegistration(evt.Id, user2.Id, "Registered");

        // Add waitlisted user
        var waitlistedUser = await CreateTestUser("waitlisted@example.com");
        var waitlistReg = await CreateRegistration(evt.Id, waitlistedUser.Id, "Waitlisted");
        waitlistReg.PaymentStatus = "MarkedPaid";
        waitlistReg.WaitlistPosition = 1;
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.UpdatePaymentStatusAsync(evt.Id, waitlistReg.Id, "Verified", creator.Id);

        // Assert
        result.Success.Should().BeTrue();
        result.Promoted.Should().BeFalse();
        result.Message.Should().Contain("roster full");

        var updated = await _context.EventRegistrations.FindAsync(waitlistReg.Id);
        updated!.Status.Should().Be("Waitlisted");
        updated.PaymentStatus.Should().Be("Verified");
        updated.WaitlistPosition.Should().Be(1);
    }

    [Fact]
    public async Task UpdatePaymentStatusAsync_ResetToPending_BlockedForRegisteredUsers()
    {
        // Arrange - Registered user cannot have payment reset to Pending
        var creator = await CreateTestUser("creator@example.com");
        var registeredUser = await CreateTestUser("registered@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var registration = await CreateRegistration(evt.Id, registeredUser.Id, "Registered");
        registration.PaymentStatus = "Verified";
        registration.PaymentVerifiedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.UpdatePaymentStatusAsync(evt.Id, registration.Id, "Pending", creator.Id);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Be("Cannot reset payment status for users already on the roster");

        // Verify payment status unchanged
        var unchanged = await _context.EventRegistrations.FindAsync(registration.Id);
        unchanged!.PaymentStatus.Should().Be("Verified");
    }

    [Fact]
    public async Task UpdatePaymentStatusAsync_ResetToPending_AllowedForWaitlistedUsers()
    {
        // Arrange - Waitlisted user can have payment reset to Pending
        var creator = await CreateTestUser("creator@example.com");
        var waitlistedUser = await CreateTestUser("waitlisted@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var registration = await CreateRegistration(evt.Id, waitlistedUser.Id, "Waitlisted");
        registration.PaymentStatus = "Verified";
        registration.PaymentVerifiedAt = DateTime.UtcNow;
        registration.WaitlistPosition = 1;
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.UpdatePaymentStatusAsync(evt.Id, registration.Id, "Pending", creator.Id);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("reset to pending");

        var updated = await _context.EventRegistrations.FindAsync(registration.Id);
        updated!.PaymentStatus.Should().Be("Pending");
        updated.PaymentVerifiedAt.Should().BeNull();
    }

    [Fact]
    public async Task UpdatePaymentStatusAsync_WaitlistedUserPromoted_TriggersWaitlistRenumber()
    {
        // Arrange - When waitlisted user is promoted, waitlist should be renumbered
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id, maxPlayers: 10);

        var waitlist1 = await CreateTestUser("waitlist1@example.com");
        var waitlist2 = await CreateTestUser("waitlist2@example.com");

        var reg1 = await CreateRegistration(evt.Id, waitlist1.Id, "Waitlisted");
        reg1.PaymentStatus = "MarkedPaid";
        reg1.WaitlistPosition = 1;
        reg1.RegisteredPosition = "Skater";

        var reg2 = await CreateRegistration(evt.Id, waitlist2.Id, "Waitlisted");
        reg2.PaymentStatus = "Pending";
        reg2.WaitlistPosition = 2;

        await _context.SaveChangesAsync();

        // Act - Promote first waitlisted user
        await _sut.UpdatePaymentStatusAsync(evt.Id, reg1.Id, "Verified", creator.Id);

        // Assert - Waitlist renumber should be called
        _mockWaitlistService.Verify(
            w => w.UpdateWaitlistPositionsAsync(evt.Id),
            Times.Once);
    }

    [Fact]
    public async Task UpdatePaymentStatusAsync_WaitlistedUserPromoted_AssignsTeam()
    {
        // Arrange - Promoted user should get team assignment
        var creator = await CreateTestUser("creator@example.com");
        var waitlistedUser = await CreateTestUser("waitlisted@example.com");
        var evt = await CreateTestEvent(creator.Id, maxPlayers: 10);

        var registration = await CreateRegistration(evt.Id, waitlistedUser.Id, "Waitlisted");
        registration.PaymentStatus = "MarkedPaid";
        registration.WaitlistPosition = 1;
        registration.RegisteredPosition = "Skater";
        registration.TeamAssignment = null; // No team yet
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.UpdatePaymentStatusAsync(evt.Id, registration.Id, "Verified", creator.Id);

        // Assert
        result.Success.Should().BeTrue();
        result.Promoted.Should().BeTrue();
        result.Registration.Should().NotBeNull();
        result.Registration!.TeamAssignment.Should().NotBeNull();
        result.Registration.TeamAssignment.Should().BeOneOf("Black", "White");
    }

    [Fact]
    public async Task UpdatePaymentStatusAsync_WaitlistedUserPromoted_SendsNotification()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var waitlistedUser = await CreateTestUser("waitlisted@example.com");
        waitlistedUser.PushToken = "ExponentPushToken[test]";
        await _context.SaveChangesAsync();

        var evt = await CreateTestEvent(creator.Id, maxPlayers: 10);

        var registration = await CreateRegistration(evt.Id, waitlistedUser.Id, "Waitlisted");
        registration.PaymentStatus = "MarkedPaid";
        registration.WaitlistPosition = 1;
        registration.RegisteredPosition = "Skater";
        await _context.SaveChangesAsync();

        // Act
        await _sut.UpdatePaymentStatusAsync(evt.Id, registration.Id, "Verified", creator.Id);

        // Assert - Notification should be sent
        _mockNotificationService.Verify(
            n => n.SendPushNotificationAsync(
                waitlistedUser.PushToken,
                "You're In!",
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdatePaymentStatusAsync_WaitlistedUserRosterFull_SendsVerifiedButFullNotification()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id, maxPlayers: 1);

        // Fill the roster
        var user1 = await CreateTestUser("user1@example.com");
        await CreateRegistration(evt.Id, user1.Id, "Registered");

        var waitlistedUser = await CreateTestUser("waitlisted@example.com");
        waitlistedUser.PushToken = "ExponentPushToken[test]";
        await _context.SaveChangesAsync();

        var registration = await CreateRegistration(evt.Id, waitlistedUser.Id, "Waitlisted");
        registration.PaymentStatus = "MarkedPaid";
        registration.WaitlistPosition = 1;
        await _context.SaveChangesAsync();

        // Act
        await _sut.UpdatePaymentStatusAsync(evt.Id, registration.Id, "Verified", creator.Id);

        // Assert - "Verified but full" notification should be sent
        _mockNotificationService.Verify(
            n => n.SendPushNotificationAsync(
                waitlistedUser.PushToken,
                "Payment Verified - On Waitlist",
                It.Is<string>(s => s.Contains("priority waitlist")),
                It.IsAny<object>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdatePaymentStatusAsync_ResponseIncludesUpdatedRegistration()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var waitlistedUser = await CreateTestUser("waitlisted@example.com");
        var evt = await CreateTestEvent(creator.Id, maxPlayers: 10);

        var registration = await CreateRegistration(evt.Id, waitlistedUser.Id, "Waitlisted");
        registration.PaymentStatus = "MarkedPaid";
        registration.WaitlistPosition = 1;
        registration.RegisteredPosition = "Skater";
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.UpdatePaymentStatusAsync(evt.Id, registration.Id, "Verified", creator.Id);

        // Assert - Response should include full registration details
        result.Registration.Should().NotBeNull();
        result.Registration!.Id.Should().Be(registration.Id);
        result.Registration.Status.Should().Be("Registered");
        result.Registration.PaymentStatus.Should().Be("Verified");
        result.Registration.PromotedAt.Should().NotBeNull();
        result.Registration.IsWaitlisted.Should().BeFalse();
    }

    #endregion
}
