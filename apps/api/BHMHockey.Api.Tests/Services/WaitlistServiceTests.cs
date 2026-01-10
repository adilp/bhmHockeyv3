using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// Tests for WaitlistService - protecting waitlist business logic.
/// These tests ensure:
/// - Waitlist positions are assigned sequentially
/// - Promotion follows FIFO order (first in, first out)
/// - Payment deadlines are set for paid events only
/// - Expired payment deadlines trigger cancellation and next promotion
/// - Waitlist positions are renumbered correctly after changes
/// </summary>
public class WaitlistServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<ILogger<WaitlistService>> _mockLogger;
    private readonly WaitlistService _sut;

    public WaitlistServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _mockNotificationService = new Mock<INotificationService>();
        _mockLogger = new Mock<ILogger<WaitlistService>>();

        _sut = new WaitlistService(_context, _mockNotificationService.Object, _mockLogger.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Helper Methods

    private async Task<User> CreateTestUser(string email = "test@example.com", string? pushToken = null)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = "hashed_password",
            FirstName = "Test",
            LastName = "User",
            Positions = new Dictionary<string, string> { { "skater", "Silver" } },
            Role = "Player",
            IsActive = true,
            PushToken = pushToken,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<Event> CreateTestEvent(
        Guid creatorId,
        decimal cost = 25.00m,
        int maxPlayers = 10,
        string name = "Test Event")
    {
        var evt = new Event
        {
            Id = Guid.NewGuid(),
            CreatorId = creatorId,
            Name = name,
            Description = "Test Description",
            EventDate = DateTime.UtcNow.AddDays(7),
            Duration = 60,
            Venue = "Test Venue",
            MaxPlayers = maxPlayers,
            Cost = cost,
            Status = "Published",
            Visibility = "Public",
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
        string status = "Registered",
        int? waitlistPosition = null,
        string? paymentStatus = null,
        DateTime? paymentDeadlineAt = null,
        string? registeredPosition = "Skater")
    {
        var registration = new EventRegistration
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            UserId = userId,
            Status = status,
            WaitlistPosition = waitlistPosition,
            PaymentStatus = paymentStatus,
            PaymentDeadlineAt = paymentDeadlineAt,
            RegisteredPosition = registeredPosition,
            RegisteredAt = DateTime.UtcNow
        };

        _context.EventRegistrations.Add(registration);
        await _context.SaveChangesAsync();
        return registration;
    }

    #endregion

    #region GetNextWaitlistPositionAsync Tests

    [Fact]
    public async Task GetNextWaitlistPositionAsync_EmptyWaitlist_ReturnsOne()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        // Act
        var result = await _sut.GetNextWaitlistPositionAsync(evt.Id);

        // Assert
        result.Should().Be(1);
    }

    [Fact]
    public async Task GetNextWaitlistPositionAsync_WithExistingWaitlist_ReturnsNextPosition()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        // Add 3 people to waitlist
        var user1 = await CreateTestUser("user1@example.com");
        var user2 = await CreateTestUser("user2@example.com");
        var user3 = await CreateTestUser("user3@example.com");

        await CreateRegistration(evt.Id, user1.Id, status: "Waitlisted", waitlistPosition: 1);
        await CreateRegistration(evt.Id, user2.Id, status: "Waitlisted", waitlistPosition: 2);
        await CreateRegistration(evt.Id, user3.Id, status: "Waitlisted", waitlistPosition: 3);

        // Act
        var result = await _sut.GetNextWaitlistPositionAsync(evt.Id);

        // Assert
        result.Should().Be(4);
    }

    [Fact]
    public async Task GetNextWaitlistPositionAsync_WithRegisteredUsersOnly_ReturnsOne()
    {
        // Arrange - Event has registered users but no waitlisted users
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var user1 = await CreateTestUser("user1@example.com");
        var user2 = await CreateTestUser("user2@example.com");

        await CreateRegistration(evt.Id, user1.Id, status: "Registered");
        await CreateRegistration(evt.Id, user2.Id, status: "Registered");

        // Act
        var result = await _sut.GetNextWaitlistPositionAsync(evt.Id);

        // Assert - Should be 1 since no one is waitlisted
        result.Should().Be(1);
    }

    #endregion

    #region PromoteNextFromWaitlistAsync Tests

    [Fact]
    public async Task PromoteNextFromWaitlistAsync_EmptyWaitlist_ReturnsNull()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        // Act
        var result = await _sut.PromoteNextFromWaitlistAsync(evt.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task PromoteNextFromWaitlistAsync_WithWaitlist_PromotesFirstInLine()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var user1 = await CreateTestUser("user1@example.com");
        var user2 = await CreateTestUser("user2@example.com");
        var user3 = await CreateTestUser("user3@example.com");

        await CreateRegistration(evt.Id, user1.Id, status: "Waitlisted", waitlistPosition: 1);
        await CreateRegistration(evt.Id, user2.Id, status: "Waitlisted", waitlistPosition: 2);
        await CreateRegistration(evt.Id, user3.Id, status: "Waitlisted", waitlistPosition: 3);

        // Act
        var result = await _sut.PromoteNextFromWaitlistAsync(evt.Id);

        // Assert
        result.Should().NotBeNull();
        result!.UserId.Should().Be(user1.Id);
        result.Status.Should().Be("Registered");
        result.WaitlistPosition.Should().BeNull();
        result.PromotedAt.Should().NotBeNull();
        result.PromotedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task PromoteNextFromWaitlistAsync_PaidEvent_SetsPaymentDeadline()
    {
        // Arrange - Event with cost > 0
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id, cost: 25.00m);

        var user = await CreateTestUser("user@example.com");
        await CreateRegistration(evt.Id, user.Id, status: "Waitlisted", waitlistPosition: 1);

        // Act
        var result = await _sut.PromoteNextFromWaitlistAsync(evt.Id);

        // Assert
        result.Should().NotBeNull();
        result!.PaymentDeadlineAt.Should().NotBeNull();
        result.PaymentDeadlineAt.Should().BeCloseTo(DateTime.UtcNow.AddHours(2), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task PromoteNextFromWaitlistAsync_FreeEvent_NoPaymentDeadline()
    {
        // Arrange - Free event (cost = 0)
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id, cost: 0);

        var user = await CreateTestUser("user@example.com");
        await CreateRegistration(evt.Id, user.Id, status: "Waitlisted", waitlistPosition: 1);

        // Act
        var result = await _sut.PromoteNextFromWaitlistAsync(evt.Id);

        // Assert
        result.Should().NotBeNull();
        result!.PaymentDeadlineAt.Should().BeNull();
    }

    [Fact]
    public async Task PromoteNextFromWaitlistAsync_AssignsTeam()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var user = await CreateTestUser("user@example.com");
        await CreateRegistration(evt.Id, user.Id, status: "Waitlisted", waitlistPosition: 1, registeredPosition: "Skater");

        // Act
        var result = await _sut.PromoteNextFromWaitlistAsync(evt.Id);

        // Assert
        result.Should().NotBeNull();
        result!.TeamAssignment.Should().BeOneOf("Black", "White");
    }

    [Fact]
    public async Task PromoteNextFromWaitlistAsync_RenumbersRemainingWaitlist()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var user1 = await CreateTestUser("user1@example.com");
        var user2 = await CreateTestUser("user2@example.com");
        var user3 = await CreateTestUser("user3@example.com");

        await CreateRegistration(evt.Id, user1.Id, status: "Waitlisted", waitlistPosition: 1);
        await CreateRegistration(evt.Id, user2.Id, status: "Waitlisted", waitlistPosition: 2);
        await CreateRegistration(evt.Id, user3.Id, status: "Waitlisted", waitlistPosition: 3);

        // Act
        await _sut.PromoteNextFromWaitlistAsync(evt.Id);

        // Assert - Remaining waitlist should be renumbered
        var user2Reg = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == evt.Id && r.UserId == user2.Id);
        var user3Reg = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == evt.Id && r.UserId == user3.Id);

        user2Reg!.WaitlistPosition.Should().Be(1);
        user3Reg!.WaitlistPosition.Should().Be(2);
    }

    [Fact]
    public async Task PromoteNextFromWaitlistAsync_SendsNotification_WhenUserHasPushToken()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id, cost: 25.00m);

        var user = await CreateTestUser("user@example.com", pushToken: "ExponentPushToken[xxxxx]");
        await CreateRegistration(evt.Id, user.Id, status: "Waitlisted", waitlistPosition: 1);

        // Act
        await _sut.PromoteNextFromWaitlistAsync(evt.Id);

        // Assert
        _mockNotificationService.Verify(
            n => n.SendPushNotificationAsync(
                "ExponentPushToken[xxxxx]",
                "You're In!",
                It.Is<string>(s => s.Contains("spot opened up") && s.Contains("2 hours")),
                It.IsAny<object>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>()),
            Times.Once);
    }

    [Fact]
    public async Task PromoteNextFromWaitlistAsync_DoesNotSendNotification_WhenUserHasNoPushToken()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var user = await CreateTestUser("user@example.com", pushToken: null);
        await CreateRegistration(evt.Id, user.Id, status: "Waitlisted", waitlistPosition: 1);

        // Act
        await _sut.PromoteNextFromWaitlistAsync(evt.Id);

        // Assert
        _mockNotificationService.Verify(
            n => n.SendPushNotificationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>()),
            Times.Never);
    }

    #endregion

    #region UpdateWaitlistPositionsAsync Tests

    [Fact]
    public async Task UpdateWaitlistPositionsAsync_RenumbersSequentially()
    {
        // Arrange - Waitlist with gaps (e.g., positions 1, 3, 5 after some promotions)
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var user1 = await CreateTestUser("user1@example.com");
        var user2 = await CreateTestUser("user2@example.com");
        var user3 = await CreateTestUser("user3@example.com");

        await CreateRegistration(evt.Id, user1.Id, status: "Waitlisted", waitlistPosition: 1);
        await CreateRegistration(evt.Id, user2.Id, status: "Waitlisted", waitlistPosition: 3);
        await CreateRegistration(evt.Id, user3.Id, status: "Waitlisted", waitlistPosition: 5);

        // Act
        await _sut.UpdateWaitlistPositionsAsync(evt.Id);

        // Assert
        var registrations = await _context.EventRegistrations
            .Where(r => r.EventId == evt.Id && r.Status == "Waitlisted")
            .OrderBy(r => r.WaitlistPosition)
            .ToListAsync();

        registrations.Should().HaveCount(3);
        registrations[0].WaitlistPosition.Should().Be(1);
        registrations[1].WaitlistPosition.Should().Be(2);
        registrations[2].WaitlistPosition.Should().Be(3);
    }

    [Fact]
    public async Task UpdateWaitlistPositionsAsync_PreservesOrder()
    {
        // Arrange - Ensure original order is preserved after renumbering
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var user1 = await CreateTestUser("user1@example.com");
        var user2 = await CreateTestUser("user2@example.com");
        var user3 = await CreateTestUser("user3@example.com");

        await CreateRegistration(evt.Id, user1.Id, status: "Waitlisted", waitlistPosition: 2);
        await CreateRegistration(evt.Id, user2.Id, status: "Waitlisted", waitlistPosition: 5);
        await CreateRegistration(evt.Id, user3.Id, status: "Waitlisted", waitlistPosition: 10);

        // Act
        await _sut.UpdateWaitlistPositionsAsync(evt.Id);

        // Assert - Order should be preserved (user1 first, then user2, then user3)
        var user1Reg = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == evt.Id && r.UserId == user1.Id);
        var user2Reg = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == evt.Id && r.UserId == user2.Id);
        var user3Reg = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == evt.Id && r.UserId == user3.Id);

        user1Reg!.WaitlistPosition.Should().Be(1);
        user2Reg!.WaitlistPosition.Should().Be(2);
        user3Reg!.WaitlistPosition.Should().Be(3);
    }

    [Fact]
    public async Task UpdateWaitlistPositionsAsync_EmptyWaitlist_DoesNotThrow()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        // Act & Assert - Should not throw
        await _sut.Invoking(s => s.UpdateWaitlistPositionsAsync(evt.Id))
            .Should().NotThrowAsync();
    }

    #endregion

    #region GetWaitlistAsync Tests

    [Fact]
    public async Task GetWaitlistAsync_ReturnsOrderedByPosition()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var user1 = await CreateTestUser("user1@example.com");
        var user2 = await CreateTestUser("user2@example.com");
        var user3 = await CreateTestUser("user3@example.com");

        // Add in non-sequential order
        await CreateRegistration(evt.Id, user3.Id, status: "Waitlisted", waitlistPosition: 3);
        await CreateRegistration(evt.Id, user1.Id, status: "Waitlisted", waitlistPosition: 1);
        await CreateRegistration(evt.Id, user2.Id, status: "Waitlisted", waitlistPosition: 2);

        // Act
        var result = await _sut.GetWaitlistAsync(evt.Id);

        // Assert
        result.Should().HaveCount(3);
        result[0].UserId.Should().Be(user1.Id);
        result[1].UserId.Should().Be(user2.Id);
        result[2].UserId.Should().Be(user3.Id);
    }

    [Fact]
    public async Task GetWaitlistAsync_ExcludesRegisteredUsers()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var registeredUser = await CreateTestUser("registered@example.com");
        var waitlistedUser = await CreateTestUser("waitlisted@example.com");

        await CreateRegistration(evt.Id, registeredUser.Id, status: "Registered");
        await CreateRegistration(evt.Id, waitlistedUser.Id, status: "Waitlisted", waitlistPosition: 1);

        // Act
        var result = await _sut.GetWaitlistAsync(evt.Id);

        // Assert
        result.Should().HaveCount(1);
        result[0].UserId.Should().Be(waitlistedUser.Id);
    }

    [Fact]
    public async Task GetWaitlistAsync_IncludesUserDetails()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var user = await CreateTestUser("waitlisted@example.com");
        await CreateRegistration(evt.Id, user.Id, status: "Waitlisted", waitlistPosition: 1);

        // Act
        var result = await _sut.GetWaitlistAsync(evt.Id);

        // Assert - User navigation property should be included
        result.Should().HaveCount(1);
        result[0].User.Should().NotBeNull();
        result[0].User.Email.Should().Be("waitlisted@example.com");
    }

    [Fact]
    public async Task GetWaitlistAsync_EmptyWaitlist_ReturnsEmptyList()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        // Act
        var result = await _sut.GetWaitlistAsync(evt.Id);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Notification Type Tests

    [Fact]
    public async Task PromoteNextFromWaitlistAsync_SendsAutoPromotedNotification_WithCorrectType()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com", pushToken: "ExponentPushToken[creator]");
        var evt = await CreateTestEvent(creator.Id, cost: 25.00m);

        var user = await CreateTestUser("user@example.com", pushToken: "ExponentPushToken[user]");
        await CreateRegistration(evt.Id, user.Id, status: "Waitlisted", waitlistPosition: 1);

        // Act
        await _sut.PromoteNextFromWaitlistAsync(evt.Id);

        // Assert - User notification with auto_promoted type
        _mockNotificationService.Verify(
            n => n.SendPushNotificationAsync(
                "ExponentPushToken[user]",
                "You're In!",
                It.IsAny<string>(),
                It.IsAny<object>(),
                user.Id,
                "auto_promoted",
                evt.OrganizationId,
                evt.Id),
            Times.Once);
    }

    [Fact]
    public async Task PromoteNextFromWaitlistAsync_SendsOrganizerAutoPromotionNotification_WithCorrectType()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com", pushToken: "ExponentPushToken[creator]");
        var evt = await CreateTestEvent(creator.Id, cost: 25.00m);

        var user = await CreateTestUser("user@example.com", pushToken: "ExponentPushToken[user]");
        await CreateRegistration(evt.Id, user.Id, status: "Waitlisted", waitlistPosition: 1);

        // Act
        await _sut.PromoteNextFromWaitlistAsync(evt.Id);

        // Assert - Organizer notification with auto_promotion type
        _mockNotificationService.Verify(
            n => n.SendPushNotificationAsync(
                "ExponentPushToken[creator]",
                "Auto-Promotion",
                It.Is<string>(s => s.Contains("auto-promoted from the waitlist")),
                It.IsAny<object>(),
                creator.Id,
                "auto_promotion",
                evt.OrganizationId,
                evt.Id),
            Times.Once);
    }

    [Fact]
    public async Task PromoteNextFromWaitlistAsync_SendsBothNotifications_WhenBothHavePushTokens()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com", pushToken: "ExponentPushToken[creator]");
        var evt = await CreateTestEvent(creator.Id, cost: 25.00m);

        var user = await CreateTestUser("user@example.com", pushToken: "ExponentPushToken[user]");
        await CreateRegistration(evt.Id, user.Id, status: "Waitlisted", waitlistPosition: 1);

        // Act
        await _sut.PromoteNextFromWaitlistAsync(evt.Id);

        // Assert - Both notifications sent
        _mockNotificationService.Verify(
            n => n.SendPushNotificationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task PromoteNextFromWaitlistAsync_SkipsOrganizerNotification_WhenOrganizerHasNoPushToken()
    {
        // Arrange - Creator has no push token
        var creator = await CreateTestUser("creator@example.com", pushToken: null);
        var evt = await CreateTestEvent(creator.Id, cost: 25.00m);

        var user = await CreateTestUser("user@example.com", pushToken: "ExponentPushToken[user]");
        await CreateRegistration(evt.Id, user.Id, status: "Waitlisted", waitlistPosition: 1);

        // Act
        await _sut.PromoteNextFromWaitlistAsync(evt.Id);

        // Assert - Only user notification sent (not organizer)
        _mockNotificationService.Verify(
            n => n.SendPushNotificationAsync(
                "ExponentPushToken[user]",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>()),
            Times.Once);

        _mockNotificationService.Verify(
            n => n.SendPushNotificationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>()),
            Times.Once); // Total only once
    }

    [Fact]
    public async Task ProcessExpiredPaymentDeadlinesAsync_SendsRegistrationExpiredNotification()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id, cost: 25.00m);

        var user = await CreateTestUser("user@example.com", pushToken: "ExponentPushToken[user]");
        // Create registration with expired payment deadline
        await CreateRegistration(
            evt.Id,
            user.Id,
            status: "Registered",
            paymentStatus: "Pending",
            paymentDeadlineAt: DateTime.UtcNow.AddHours(-1)); // Expired 1 hour ago

        // Act
        await _sut.ProcessExpiredPaymentDeadlinesAsync();

        // Assert - Registration expired notification sent
        _mockNotificationService.Verify(
            n => n.SendPushNotificationAsync(
                "ExponentPushToken[user]",
                "Registration Expired",
                It.Is<string>(s => s.Contains("cancelled due to missed payment deadline")),
                It.IsAny<object>(),
                user.Id,
                "registration_expired",
                evt.OrganizationId,
                evt.Id),
            Times.Once);
    }

    #endregion

    #region PromoteFromWaitlistAsync Tests (Priority Queue)

    [Fact]
    public async Task PromoteFromWaitlistAsync_VerifiedUsersPromotedFirst_OrderedByRegisteredAt()
    {
        // Arrange - 2 verified users (one registered earlier), 1 unverified user at position 1
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id, cost: 25.00m, maxPlayers: 10);

        var verifiedUser1 = await CreateTestUser("verified1@example.com");
        var verifiedUser2 = await CreateTestUser("verified2@example.com");
        var unverifiedUser = await CreateTestUser("unverified@example.com");

        // Unverified user is position 1 (joined first by waitlist position)
        var unverifiedReg = new EventRegistration
        {
            Id = Guid.NewGuid(),
            EventId = evt.Id,
            UserId = unverifiedUser.Id,
            Status = "Waitlisted",
            WaitlistPosition = 1,
            PaymentStatus = "Pending",
            RegisteredPosition = "Skater",
            RegisteredAt = DateTime.UtcNow.AddHours(-3) // Earliest
        };
        _context.EventRegistrations.Add(unverifiedReg);

        // Verified user 1 registered earlier than verified user 2
        var verifiedReg1 = new EventRegistration
        {
            Id = Guid.NewGuid(),
            EventId = evt.Id,
            UserId = verifiedUser1.Id,
            Status = "Waitlisted",
            WaitlistPosition = 2,
            PaymentStatus = "Verified",
            RegisteredPosition = "Skater",
            RegisteredAt = DateTime.UtcNow.AddHours(-2) // Second earliest
        };
        _context.EventRegistrations.Add(verifiedReg1);

        var verifiedReg2 = new EventRegistration
        {
            Id = Guid.NewGuid(),
            EventId = evt.Id,
            UserId = verifiedUser2.Id,
            Status = "Waitlisted",
            WaitlistPosition = 3,
            PaymentStatus = "Verified",
            RegisteredPosition = "Skater",
            RegisteredAt = DateTime.UtcNow.AddHours(-1) // Most recent
        };
        _context.EventRegistrations.Add(verifiedReg2);

        await _context.SaveChangesAsync();

        // Act - Promote 1 spot
        var result = await _sut.PromoteFromWaitlistAsync(evt.Id, spotCount: 1);

        // Assert - Verified user 1 should be promoted (earliest RegisteredAt among verified)
        result.Should().NotBeNull();
        result.Promoted.Should().HaveCount(1);
        result.Promoted[0].UserId.Should().Be(verifiedUser1.Id);
        result.Promoted[0].Status.Should().Be("Registered");

        // Unverified user should NOT be promoted (verified users have priority)
        var unverifiedRegAfter = await _context.EventRegistrations
            .FirstAsync(r => r.Id == unverifiedReg.Id);
        unverifiedRegAfter.Status.Should().Be("Waitlisted");
    }

    [Fact]
    public async Task PromoteFromWaitlistAsync_TieBreaksById_WhenSameRegisteredAt()
    {
        // Arrange - 2 verified users with same RegisteredAt, different IDs
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id, cost: 25.00m, maxPlayers: 10);

        var verifiedUser1 = await CreateTestUser("verified1@example.com");
        var verifiedUser2 = await CreateTestUser("verified2@example.com");

        var sameTime = DateTime.UtcNow.AddHours(-1);

        // Create IDs where user2's ID is smaller (should be promoted first)
        var smallerId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var largerId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

        var verifiedReg1 = new EventRegistration
        {
            Id = largerId,
            EventId = evt.Id,
            UserId = verifiedUser1.Id,
            Status = "Waitlisted",
            WaitlistPosition = 1,
            PaymentStatus = "Verified",
            RegisteredPosition = "Skater",
            RegisteredAt = sameTime
        };
        _context.EventRegistrations.Add(verifiedReg1);

        var verifiedReg2 = new EventRegistration
        {
            Id = smallerId,
            EventId = evt.Id,
            UserId = verifiedUser2.Id,
            Status = "Waitlisted",
            WaitlistPosition = 2,
            PaymentStatus = "Verified",
            RegisteredPosition = "Skater",
            RegisteredAt = sameTime // Same time
        };
        _context.EventRegistrations.Add(verifiedReg2);

        await _context.SaveChangesAsync();

        // Act - Promote 1 spot
        var result = await _sut.PromoteFromWaitlistAsync(evt.Id, spotCount: 1);

        // Assert - User with smaller ID should be promoted first
        result.Promoted.Should().HaveCount(1);
        result.Promoted[0].Id.Should().Be(smallerId);
    }

    [Fact]
    public async Task PromoteFromWaitlistAsync_NotifiesUnverifiedUsers_WhenSpotsRemain()
    {
        // Arrange - 1 verified user, 3 unverified users, 3 spots open
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id, cost: 25.00m, maxPlayers: 10);

        var verifiedUser = await CreateTestUser("verified@example.com", pushToken: "ExponentPushToken[verified]");
        var unverifiedUser1 = await CreateTestUser("unverified1@example.com", pushToken: "ExponentPushToken[unverified1]");
        var unverifiedUser2 = await CreateTestUser("unverified2@example.com", pushToken: "ExponentPushToken[unverified2]");
        var unverifiedUser3 = await CreateTestUser("unverified3@example.com", pushToken: "ExponentPushToken[unverified3]");

        // Verified user
        var verifiedReg = new EventRegistration
        {
            Id = Guid.NewGuid(),
            EventId = evt.Id,
            UserId = verifiedUser.Id,
            Status = "Waitlisted",
            WaitlistPosition = 4, // Later position doesn't matter for verified
            PaymentStatus = "Verified",
            RegisteredPosition = "Skater",
            RegisteredAt = DateTime.UtcNow.AddHours(-1)
        };
        _context.EventRegistrations.Add(verifiedReg);

        // Unverified users in waitlist order
        var unverifiedReg1 = new EventRegistration
        {
            Id = Guid.NewGuid(),
            EventId = evt.Id,
            UserId = unverifiedUser1.Id,
            Status = "Waitlisted",
            WaitlistPosition = 1,
            PaymentStatus = "Pending",
            RegisteredPosition = "Skater",
            RegisteredAt = DateTime.UtcNow.AddHours(-3)
        };
        _context.EventRegistrations.Add(unverifiedReg1);

        var unverifiedReg2 = new EventRegistration
        {
            Id = Guid.NewGuid(),
            EventId = evt.Id,
            UserId = unverifiedUser2.Id,
            Status = "Waitlisted",
            WaitlistPosition = 2,
            PaymentStatus = "Pending",
            RegisteredPosition = "Skater",
            RegisteredAt = DateTime.UtcNow.AddHours(-2)
        };
        _context.EventRegistrations.Add(unverifiedReg2);

        var unverifiedReg3 = new EventRegistration
        {
            Id = Guid.NewGuid(),
            EventId = evt.Id,
            UserId = unverifiedUser3.Id,
            Status = "Waitlisted",
            WaitlistPosition = 3,
            PaymentStatus = "Pending",
            RegisteredPosition = "Skater",
            RegisteredAt = DateTime.UtcNow
        };
        _context.EventRegistrations.Add(unverifiedReg3);

        await _context.SaveChangesAsync();

        // Act - Promote 3 spots (1 verified + 2 unverified notifications)
        var result = await _sut.PromoteFromWaitlistAsync(evt.Id, spotCount: 3);

        // Assert
        // 1 verified user promoted
        result.Promoted.Should().HaveCount(1);
        result.Promoted[0].UserId.Should().Be(verifiedUser.Id);

        // 3 notifications: 1 auto-promoted + 2 spot-available (not 3 because only 2 remaining spots)
        result.PendingNotifications.Should().HaveCount(3);
        result.PendingNotifications.Count(n => n.Type == NotificationType.AutoPromoted).Should().Be(1);
        result.PendingNotifications.Count(n => n.Type == NotificationType.SpotAvailable).Should().Be(2);

        // Verify unverified users 1 and 2 got notified (in waitlist order)
        var spotAvailableNotifications = result.PendingNotifications
            .Where(n => n.Type == NotificationType.SpotAvailable)
            .ToList();
        spotAvailableNotifications.Should().Contain(n => n.User.Id == unverifiedUser1.Id);
        spotAvailableNotifications.Should().Contain(n => n.User.Id == unverifiedUser2.Id);
        spotAvailableNotifications.Should().NotContain(n => n.User.Id == unverifiedUser3.Id);
    }

    [Fact]
    public async Task PromoteFromWaitlistAsync_CallerOwnsTransaction_DoesNotSendNotifications()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com", pushToken: "ExponentPushToken[creator]");
        var evt = await CreateTestEvent(creator.Id, cost: 25.00m, maxPlayers: 10);

        var user = await CreateTestUser("user@example.com", pushToken: "ExponentPushToken[user]");
        var reg = new EventRegistration
        {
            Id = Guid.NewGuid(),
            EventId = evt.Id,
            UserId = user.Id,
            Status = "Waitlisted",
            WaitlistPosition = 1,
            PaymentStatus = "Verified",
            RegisteredPosition = "Skater",
            RegisteredAt = DateTime.UtcNow.AddHours(-1)
        };
        _context.EventRegistrations.Add(reg);
        await _context.SaveChangesAsync();

        // Act - callerOwnsTransaction = true
        var result = await _sut.PromoteFromWaitlistAsync(evt.Id, spotCount: 1, callerOwnsTransaction: true);

        // Assert - Notifications should be pending but NOT sent
        result.Promoted.Should().HaveCount(1);
        result.PendingNotifications.Should().HaveCount(1);

        // Verify no notifications were actually sent
        _mockNotificationService.Verify(
            n => n.SendPushNotificationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>()),
            Times.Never);
    }

    [Fact]
    public async Task PromoteFromWaitlistAsync_MethodOwnsTransaction_SendsNotifications()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com", pushToken: "ExponentPushToken[creator]");
        var evt = await CreateTestEvent(creator.Id, cost: 25.00m, maxPlayers: 10);

        var user = await CreateTestUser("user@example.com", pushToken: "ExponentPushToken[user]");
        var reg = new EventRegistration
        {
            Id = Guid.NewGuid(),
            EventId = evt.Id,
            UserId = user.Id,
            Status = "Waitlisted",
            WaitlistPosition = 1,
            PaymentStatus = "Verified",
            RegisteredPosition = "Skater",
            RegisteredAt = DateTime.UtcNow.AddHours(-1)
        };
        _context.EventRegistrations.Add(reg);
        await _context.SaveChangesAsync();

        // Act - callerOwnsTransaction = false (default)
        var result = await _sut.PromoteFromWaitlistAsync(evt.Id, spotCount: 1, callerOwnsTransaction: false);

        // Assert - Notifications should be sent
        result.Promoted.Should().HaveCount(1);
        result.PendingNotifications.Should().HaveCount(1);

        // Verify notifications were sent (user + organizer = 2 calls)
        _mockNotificationService.Verify(
            n => n.SendPushNotificationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>()),
            Times.Exactly(2)); // User + Organizer
    }

    [Fact]
    public async Task PromoteFromWaitlistAsync_MultipleSpots_PromotesMultipleVerifiedUsers()
    {
        // Arrange - 3 verified users, promote 2
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id, cost: 25.00m, maxPlayers: 10);

        var user1 = await CreateTestUser("user1@example.com");
        var user2 = await CreateTestUser("user2@example.com");
        var user3 = await CreateTestUser("user3@example.com");

        var baseTime = DateTime.UtcNow.AddHours(-3);

        var reg1 = new EventRegistration
        {
            Id = Guid.NewGuid(),
            EventId = evt.Id,
            UserId = user1.Id,
            Status = "Waitlisted",
            WaitlistPosition = 1,
            PaymentStatus = "Verified",
            RegisteredPosition = "Skater",
            RegisteredAt = baseTime
        };
        _context.EventRegistrations.Add(reg1);

        var reg2 = new EventRegistration
        {
            Id = Guid.NewGuid(),
            EventId = evt.Id,
            UserId = user2.Id,
            Status = "Waitlisted",
            WaitlistPosition = 2,
            PaymentStatus = "Verified",
            RegisteredPosition = "Skater",
            RegisteredAt = baseTime.AddMinutes(30)
        };
        _context.EventRegistrations.Add(reg2);

        var reg3 = new EventRegistration
        {
            Id = Guid.NewGuid(),
            EventId = evt.Id,
            UserId = user3.Id,
            Status = "Waitlisted",
            WaitlistPosition = 3,
            PaymentStatus = "Verified",
            RegisteredPosition = "Skater",
            RegisteredAt = baseTime.AddMinutes(60)
        };
        _context.EventRegistrations.Add(reg3);

        await _context.SaveChangesAsync();

        // Act - Promote 2 spots
        var result = await _sut.PromoteFromWaitlistAsync(evt.Id, spotCount: 2);

        // Assert - First 2 by RegisteredAt should be promoted
        result.Promoted.Should().HaveCount(2);
        result.Promoted.Select(p => p.UserId).Should().Contain(user1.Id);
        result.Promoted.Select(p => p.UserId).Should().Contain(user2.Id);
        result.Promoted.Select(p => p.UserId).Should().NotContain(user3.Id);

        // User3 should still be waitlisted
        var user3Reg = await _context.EventRegistrations.FirstAsync(r => r.UserId == user3.Id);
        user3Reg.Status.Should().Be("Waitlisted");
    }

    [Fact]
    public async Task PromoteFromWaitlistAsync_EmptyWaitlist_ReturnsEmptyResult()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        // Act
        var result = await _sut.PromoteFromWaitlistAsync(evt.Id, spotCount: 1);

        // Assert
        result.Should().NotBeNull();
        result.Promoted.Should().BeEmpty();
        result.PendingNotifications.Should().BeEmpty();
    }

    [Fact]
    public async Task SendPendingNotificationsAsync_SendsAutoPromotedNotification()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com", pushToken: "ExponentPushToken[creator]");
        var evt = await CreateTestEvent(creator.Id);
        var user = await CreateTestUser("user@example.com", pushToken: "ExponentPushToken[user]");

        var notifications = new List<PendingNotification>
        {
            new PendingNotification
            {
                User = user,
                Event = evt,
                Organizer = creator,
                Type = NotificationType.AutoPromoted
            }
        };

        // Act
        await _sut.SendPendingNotificationsAsync(notifications);

        // Assert - User notification sent
        _mockNotificationService.Verify(
            n => n.SendPushNotificationAsync(
                "ExponentPushToken[user]",
                "You're In!",
                It.Is<string>(s => s.Contains("promoted from the waitlist")),
                It.IsAny<object>(),
                user.Id,
                "auto_promoted",
                evt.OrganizationId,
                evt.Id),
            Times.Once);

        // Assert - Organizer notification sent
        _mockNotificationService.Verify(
            n => n.SendPushNotificationAsync(
                "ExponentPushToken[creator]",
                "Auto-Promotion",
                It.Is<string>(s => s.Contains("auto-promoted from the waitlist")),
                It.IsAny<object>(),
                creator.Id,
                "auto_promotion",
                evt.OrganizationId,
                evt.Id),
            Times.Once);
    }

    [Fact]
    public async Task SendPendingNotificationsAsync_SendsSpotAvailableNotification()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);
        var user = await CreateTestUser("user@example.com", pushToken: "ExponentPushToken[user]");

        var notifications = new List<PendingNotification>
        {
            new PendingNotification
            {
                User = user,
                Event = evt,
                Organizer = creator,
                Type = NotificationType.SpotAvailable
            }
        };

        // Act
        await _sut.SendPendingNotificationsAsync(notifications);

        // Assert
        _mockNotificationService.Verify(
            n => n.SendPushNotificationAsync(
                "ExponentPushToken[user]",
                "Spot Available!",
                It.Is<string>(s => s.Contains("Complete payment")),
                It.IsAny<object>(),
                user.Id,
                "spot_available",
                evt.OrganizationId,
                evt.Id),
            Times.Once);
    }

    #endregion

    #region ReorderWaitlistAsync Tests

    [Fact]
    public async Task ReorderWaitlistAsync_ReordersThreeUsers_PositionsUpdatedCorrectly()
    {
        // Arrange - 3 users on waitlist
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var user1 = await CreateTestUser("user1@example.com");
        var user2 = await CreateTestUser("user2@example.com");
        var user3 = await CreateTestUser("user3@example.com");

        var reg1 = await CreateRegistration(evt.Id, user1.Id, status: "Waitlisted", waitlistPosition: 1);
        var reg2 = await CreateRegistration(evt.Id, user2.Id, status: "Waitlisted", waitlistPosition: 2);
        var reg3 = await CreateRegistration(evt.Id, user3.Id, status: "Waitlisted", waitlistPosition: 3);

        // Reorder: user3 -> 1, user1 -> 2, user2 -> 3
        var reorderItems = new List<WaitlistReorderItem>
        {
            new WaitlistReorderItem(reg3.Id, 1),
            new WaitlistReorderItem(reg1.Id, 2),
            new WaitlistReorderItem(reg2.Id, 3)
        };

        // Act
        var result = await _sut.ReorderWaitlistAsync(evt.Id, reorderItems);

        // Assert
        result.Should().BeTrue();

        var updatedReg1 = await _context.EventRegistrations.FirstAsync(r => r.Id == reg1.Id);
        var updatedReg2 = await _context.EventRegistrations.FirstAsync(r => r.Id == reg2.Id);
        var updatedReg3 = await _context.EventRegistrations.FirstAsync(r => r.Id == reg3.Id);

        updatedReg3.WaitlistPosition.Should().Be(1);
        updatedReg1.WaitlistPosition.Should().Be(2);
        updatedReg2.WaitlistPosition.Should().Be(3);
    }

    [Fact]
    public async Task ReorderWaitlistAsync_MissingUser_ThrowsInvalidOperationException()
    {
        // Arrange - 3 users on waitlist
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var user1 = await CreateTestUser("user1@example.com");
        var user2 = await CreateTestUser("user2@example.com");
        var user3 = await CreateTestUser("user3@example.com");

        var reg1 = await CreateRegistration(evt.Id, user1.Id, status: "Waitlisted", waitlistPosition: 1);
        var reg2 = await CreateRegistration(evt.Id, user2.Id, status: "Waitlisted", waitlistPosition: 2);
        await CreateRegistration(evt.Id, user3.Id, status: "Waitlisted", waitlistPosition: 3);

        // Reorder with only 2 of 3 users (missing user3)
        var reorderItems = new List<WaitlistReorderItem>
        {
            new WaitlistReorderItem(reg1.Id, 1),
            new WaitlistReorderItem(reg2.Id, 2)
        };

        // Act & Assert
        await _sut.Invoking(s => s.ReorderWaitlistAsync(evt.Id, reorderItems))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("All waitlisted users must be included");
    }

    [Fact]
    public async Task ReorderWaitlistAsync_PositionsWithGaps_ThrowsInvalidOperationException()
    {
        // Arrange - 3 users on waitlist
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var user1 = await CreateTestUser("user1@example.com");
        var user2 = await CreateTestUser("user2@example.com");
        var user3 = await CreateTestUser("user3@example.com");

        var reg1 = await CreateRegistration(evt.Id, user1.Id, status: "Waitlisted", waitlistPosition: 1);
        var reg2 = await CreateRegistration(evt.Id, user2.Id, status: "Waitlisted", waitlistPosition: 2);
        var reg3 = await CreateRegistration(evt.Id, user3.Id, status: "Waitlisted", waitlistPosition: 3);

        // Reorder with gaps: 1, 2, 5 instead of 1, 2, 3
        var reorderItems = new List<WaitlistReorderItem>
        {
            new WaitlistReorderItem(reg1.Id, 1),
            new WaitlistReorderItem(reg2.Id, 2),
            new WaitlistReorderItem(reg3.Id, 5)
        };

        // Act & Assert
        await _sut.Invoking(s => s.ReorderWaitlistAsync(evt.Id, reorderItems))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Positions must be sequential starting from 1");
    }

    [Fact]
    public async Task ReorderWaitlistAsync_DuplicatePositions_ThrowsInvalidOperationException()
    {
        // Arrange - 3 users on waitlist
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var user1 = await CreateTestUser("user1@example.com");
        var user2 = await CreateTestUser("user2@example.com");
        var user3 = await CreateTestUser("user3@example.com");

        var reg1 = await CreateRegistration(evt.Id, user1.Id, status: "Waitlisted", waitlistPosition: 1);
        var reg2 = await CreateRegistration(evt.Id, user2.Id, status: "Waitlisted", waitlistPosition: 2);
        var reg3 = await CreateRegistration(evt.Id, user3.Id, status: "Waitlisted", waitlistPosition: 3);

        // Reorder with duplicates: 1, 2, 2 instead of 1, 2, 3
        var reorderItems = new List<WaitlistReorderItem>
        {
            new WaitlistReorderItem(reg1.Id, 1),
            new WaitlistReorderItem(reg2.Id, 2),
            new WaitlistReorderItem(reg3.Id, 2)
        };

        // Act & Assert
        await _sut.Invoking(s => s.ReorderWaitlistAsync(evt.Id, reorderItems))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Positions must be sequential starting from 1");
    }

    [Fact]
    public async Task ReorderWaitlistAsync_PositionsNotStartingFromOne_ThrowsInvalidOperationException()
    {
        // Arrange - 3 users on waitlist
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var user1 = await CreateTestUser("user1@example.com");
        var user2 = await CreateTestUser("user2@example.com");
        var user3 = await CreateTestUser("user3@example.com");

        var reg1 = await CreateRegistration(evt.Id, user1.Id, status: "Waitlisted", waitlistPosition: 1);
        var reg2 = await CreateRegistration(evt.Id, user2.Id, status: "Waitlisted", waitlistPosition: 2);
        var reg3 = await CreateRegistration(evt.Id, user3.Id, status: "Waitlisted", waitlistPosition: 3);

        // Reorder starting from 0 instead of 1
        var reorderItems = new List<WaitlistReorderItem>
        {
            new WaitlistReorderItem(reg1.Id, 0),
            new WaitlistReorderItem(reg2.Id, 1),
            new WaitlistReorderItem(reg3.Id, 2)
        };

        // Act & Assert
        await _sut.Invoking(s => s.ReorderWaitlistAsync(evt.Id, reorderItems))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Positions must be sequential starting from 1");
    }

    [Fact]
    public async Task ReorderWaitlistAsync_EmptyWaitlist_ReturnsTrue()
    {
        // Arrange - Event with no waitlist
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        // Empty items list for empty waitlist
        var reorderItems = new List<WaitlistReorderItem>();

        // Act
        var result = await _sut.ReorderWaitlistAsync(evt.Id, reorderItems);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ReorderWaitlistAsync_RegisteredUsersUnaffected()
    {
        // Arrange - Mix of registered and waitlisted users
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var registeredUser = await CreateTestUser("registered@example.com");
        var waitlistUser1 = await CreateTestUser("waitlist1@example.com");
        var waitlistUser2 = await CreateTestUser("waitlist2@example.com");

        var regRegistered = await CreateRegistration(evt.Id, registeredUser.Id, status: "Registered");
        var regWaitlist1 = await CreateRegistration(evt.Id, waitlistUser1.Id, status: "Waitlisted", waitlistPosition: 1);
        var regWaitlist2 = await CreateRegistration(evt.Id, waitlistUser2.Id, status: "Waitlisted", waitlistPosition: 2);

        // Reorder only waitlisted users
        var reorderItems = new List<WaitlistReorderItem>
        {
            new WaitlistReorderItem(regWaitlist2.Id, 1),
            new WaitlistReorderItem(regWaitlist1.Id, 2)
        };

        // Act
        var result = await _sut.ReorderWaitlistAsync(evt.Id, reorderItems);

        // Assert
        result.Should().BeTrue();

        // Verify registered user is unaffected
        var updatedRegistered = await _context.EventRegistrations.FirstAsync(r => r.Id == regRegistered.Id);
        updatedRegistered.Status.Should().Be("Registered");
        updatedRegistered.WaitlistPosition.Should().BeNull();

        // Verify waitlisted users were reordered
        var updatedWaitlist1 = await _context.EventRegistrations.FirstAsync(r => r.Id == regWaitlist1.Id);
        var updatedWaitlist2 = await _context.EventRegistrations.FirstAsync(r => r.Id == regWaitlist2.Id);
        updatedWaitlist2.WaitlistPosition.Should().Be(1);
        updatedWaitlist1.WaitlistPosition.Should().Be(2);
    }

    [Fact]
    public async Task ReorderWaitlistAsync_ExtraRegistrationId_ThrowsInvalidOperationException()
    {
        // Arrange - 2 users on waitlist
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var user1 = await CreateTestUser("user1@example.com");
        var user2 = await CreateTestUser("user2@example.com");

        var reg1 = await CreateRegistration(evt.Id, user1.Id, status: "Waitlisted", waitlistPosition: 1);
        var reg2 = await CreateRegistration(evt.Id, user2.Id, status: "Waitlisted", waitlistPosition: 2);

        // Reorder with extra non-existent registration ID
        var reorderItems = new List<WaitlistReorderItem>
        {
            new WaitlistReorderItem(reg1.Id, 1),
            new WaitlistReorderItem(reg2.Id, 2),
            new WaitlistReorderItem(Guid.NewGuid(), 3)  // Extra ID not on waitlist
        };

        // Act & Assert
        await _sut.Invoking(s => s.ReorderWaitlistAsync(evt.Id, reorderItems))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("All waitlisted users must be included");
    }

    #endregion
}
