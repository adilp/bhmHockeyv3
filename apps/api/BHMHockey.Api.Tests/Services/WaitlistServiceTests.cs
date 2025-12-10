using BHMHockey.Api.Data;
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
                It.IsAny<object>()),
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
                It.IsAny<object>()),
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

    #region ProcessExpiredPaymentDeadlinesAsync Tests

    [Fact]
    public async Task ProcessExpiredPaymentDeadlinesAsync_ExpiredDeadline_CancelsRegistration()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var user = await CreateTestUser("user@example.com");
        var registration = await CreateRegistration(
            evt.Id,
            user.Id,
            status: "Registered",
            paymentStatus: "Pending",
            paymentDeadlineAt: DateTime.UtcNow.AddHours(-1)); // Expired 1 hour ago

        // Act
        await _sut.ProcessExpiredPaymentDeadlinesAsync();

        // Assert
        var updatedReg = await _context.EventRegistrations.FindAsync(registration.Id);
        updatedReg!.Status.Should().Be("Cancelled");
        updatedReg.PaymentDeadlineAt.Should().BeNull();
    }

    [Fact]
    public async Task ProcessExpiredPaymentDeadlinesAsync_ExpiredDeadline_PromotesNext()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var expiredUser = await CreateTestUser("expired@example.com");
        var waitlistedUser = await CreateTestUser("waitlisted@example.com");

        await CreateRegistration(
            evt.Id,
            expiredUser.Id,
            status: "Registered",
            paymentStatus: "Pending",
            paymentDeadlineAt: DateTime.UtcNow.AddHours(-1)); // Expired

        await CreateRegistration(
            evt.Id,
            waitlistedUser.Id,
            status: "Waitlisted",
            waitlistPosition: 1);

        // Act
        await _sut.ProcessExpiredPaymentDeadlinesAsync();

        // Assert - Waitlisted user should be promoted
        var waitlistedReg = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == evt.Id && r.UserId == waitlistedUser.Id);
        waitlistedReg!.Status.Should().Be("Registered");
        waitlistedReg.WaitlistPosition.Should().BeNull();
    }

    [Fact]
    public async Task ProcessExpiredPaymentDeadlinesAsync_NotExpired_DoesNotCancel()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var user = await CreateTestUser("user@example.com");
        var registration = await CreateRegistration(
            evt.Id,
            user.Id,
            status: "Registered",
            paymentStatus: "Pending",
            paymentDeadlineAt: DateTime.UtcNow.AddHours(1)); // Not expired yet

        // Act
        await _sut.ProcessExpiredPaymentDeadlinesAsync();

        // Assert - Should still be registered
        var updatedReg = await _context.EventRegistrations.FindAsync(registration.Id);
        updatedReg!.Status.Should().Be("Registered");
        updatedReg.PaymentDeadlineAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessExpiredPaymentDeadlinesAsync_AlreadyPaid_DoesNotCancel()
    {
        // Arrange - User has expired deadline but already paid
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var user = await CreateTestUser("user@example.com");
        var registration = await CreateRegistration(
            evt.Id,
            user.Id,
            status: "Registered",
            paymentStatus: "Verified", // Already paid!
            paymentDeadlineAt: DateTime.UtcNow.AddHours(-1)); // Expired but doesn't matter

        // Act
        await _sut.ProcessExpiredPaymentDeadlinesAsync();

        // Assert - Should still be registered
        var updatedReg = await _context.EventRegistrations.FindAsync(registration.Id);
        updatedReg!.Status.Should().Be("Registered");
    }

    [Fact]
    public async Task ProcessExpiredPaymentDeadlinesAsync_MarkedPaid_DoesNotCancel()
    {
        // Arrange - User has expired deadline but marked paid (pending verification)
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var user = await CreateTestUser("user@example.com");
        var registration = await CreateRegistration(
            evt.Id,
            user.Id,
            status: "Registered",
            paymentStatus: "MarkedPaid", // Marked as paid!
            paymentDeadlineAt: DateTime.UtcNow.AddHours(-1)); // Expired but doesn't matter

        // Act
        await _sut.ProcessExpiredPaymentDeadlinesAsync();

        // Assert - Should still be registered
        var updatedReg = await _context.EventRegistrations.FindAsync(registration.Id);
        updatedReg!.Status.Should().Be("Registered");
    }

    [Fact]
    public async Task ProcessExpiredPaymentDeadlinesAsync_SendsNotification_WhenCancelling()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id, name: "Test Game");

        var user = await CreateTestUser("user@example.com", pushToken: "ExponentPushToken[xxxxx]");
        await CreateRegistration(
            evt.Id,
            user.Id,
            status: "Registered",
            paymentStatus: "Pending",
            paymentDeadlineAt: DateTime.UtcNow.AddHours(-1));

        // Act
        await _sut.ProcessExpiredPaymentDeadlinesAsync();

        // Assert
        _mockNotificationService.Verify(
            n => n.SendPushNotificationAsync(
                "ExponentPushToken[xxxxx]",
                "Registration Expired",
                It.Is<string>(s => s.Contains("cancelled") && s.Contains("payment deadline")),
                It.IsAny<object>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessExpiredPaymentDeadlinesAsync_MultipleExpired_ProcessesAll()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var evt = await CreateTestEvent(creator.Id);

        var user1 = await CreateTestUser("user1@example.com");
        var user2 = await CreateTestUser("user2@example.com");
        var waitlisted1 = await CreateTestUser("waitlisted1@example.com");
        var waitlisted2 = await CreateTestUser("waitlisted2@example.com");

        await CreateRegistration(evt.Id, user1.Id, status: "Registered",
            paymentStatus: "Pending", paymentDeadlineAt: DateTime.UtcNow.AddHours(-2));
        await CreateRegistration(evt.Id, user2.Id, status: "Registered",
            paymentStatus: "Pending", paymentDeadlineAt: DateTime.UtcNow.AddHours(-1));
        await CreateRegistration(evt.Id, waitlisted1.Id, status: "Waitlisted", waitlistPosition: 1);
        await CreateRegistration(evt.Id, waitlisted2.Id, status: "Waitlisted", waitlistPosition: 2);

        // Act
        await _sut.ProcessExpiredPaymentDeadlinesAsync();

        // Assert - Both expired registrations should be cancelled
        var user1Reg = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == evt.Id && r.UserId == user1.Id);
        var user2Reg = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == evt.Id && r.UserId == user2.Id);

        user1Reg!.Status.Should().Be("Cancelled");
        user2Reg!.Status.Should().Be("Cancelled");

        // Both waitlisted users should be promoted
        var waitlisted1Reg = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == evt.Id && r.UserId == waitlisted1.Id);
        var waitlisted2Reg = await _context.EventRegistrations
            .FirstOrDefaultAsync(r => r.EventId == evt.Id && r.UserId == waitlisted2.Id);

        waitlisted1Reg!.Status.Should().Be("Registered");
        waitlisted2Reg!.Status.Should().Be("Registered");
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
}
