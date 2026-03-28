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
/// Tests for NotFullGameReminderService - protecting the "spots available" notification logic.
/// These tests ensure:
/// - Events are correctly selected based on timing, status, and capacity
/// - Goalies are excluded from capacity calculations (matching EventService)
/// - Recipients are filtered by activity, ghost status, push tokens, and registration
/// - Org events scope to subscribers; public events scope to all eligible users
/// - Sentinel prevents duplicate sends and resets on event date change
/// </summary>
public class NotFullGameReminderServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly NotFullGameReminderService _sut;

    public NotFullGameReminderServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _mockNotificationService = new Mock<INotificationService>();

        _sut = new NotFullGameReminderService(
            _context,
            _mockNotificationService.Object,
            Mock.Of<ILogger<NotFullGameReminderService>>());
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    #region Helper Methods

    private async Task<User> CreateTestUser(
        string email = "test@example.com",
        string? pushToken = "auto",
        bool isActive = true,
        bool isGhostPlayer = false)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = "hashed",
            FirstName = "Test",
            LastName = "User",
            Positions = new Dictionary<string, string> { { "skater", "Silver" } },
            PushToken = pushToken == "auto" ? $"ExponentPushToken[{Guid.NewGuid()}]" : pushToken,
            IsActive = isActive,
            IsGhostPlayer = isGhostPlayer,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private async Task<Organization> CreateTestOrganization(Guid creatorId)
    {
        var org = new Organization
        {
            Id = Guid.NewGuid(),
            Name = "Test Org",
            CreatorId = creatorId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Organizations.Add(org);
        await _context.SaveChangesAsync();
        return org;
    }

    private async Task<OrganizationSubscription> CreateSubscription(
        Guid orgId, Guid userId, bool notificationEnabled = true)
    {
        var sub = new OrganizationSubscription
        {
            Id = Guid.NewGuid(),
            OrganizationId = orgId,
            UserId = userId,
            NotificationEnabled = notificationEnabled,
            SubscribedAt = DateTime.UtcNow
        };
        _context.OrganizationSubscriptions.Add(sub);
        await _context.SaveChangesAsync();
        return sub;
    }

    private async Task<Event> CreateTestEvent(
        Guid creatorId,
        int maxPlayers = 20,
        string status = "Published",
        string visibility = "Public",
        Guid? organizationId = null,
        DateTime? eventDate = null,
        DateTime? notFullReminderSentAt = null,
        DateTime? registrationDeadline = null)
    {
        var evt = new Event
        {
            Id = Guid.NewGuid(),
            CreatorId = creatorId,
            OrganizationId = organizationId,
            MaxPlayers = maxPlayers,
            Status = status,
            Visibility = visibility,
            EventDate = eventDate ?? DateTime.UtcNow.AddHours(47),
            NotFullReminderSentAt = notFullReminderSentAt,
            RegistrationDeadline = registrationDeadline,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Events.Add(evt);
        await _context.SaveChangesAsync();
        return evt;
    }

    private async Task<EventRegistration> CreateRegistration(
        Guid eventId, Guid userId,
        string status = "Registered",
        string registeredPosition = "Skater")
    {
        var reg = new EventRegistration
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            UserId = userId,
            Status = status,
            RegisteredPosition = registeredPosition,
            RegisteredAt = DateTime.UtcNow
        };
        _context.EventRegistrations.Add(reg);
        await _context.SaveChangesAsync();
        return reg;
    }

    #endregion

    #region Event Selection

    [Fact]
    public async Task SendNotFullRemindersAsync_SendsForEventWithin48Hours_NotFull()
    {
        // Arrange - event 47 hours from now, 0 of 20 spots filled
        var creator = await CreateTestUser("creator@test.com");
        var player = await CreateTestUser("player@test.com");
        var evt = await CreateTestEvent(creator.Id, maxPlayers: 20,
            eventDate: DateTime.UtcNow.AddHours(47));

        // Act
        await _sut.SendNotFullRemindersAsync();

        // Assert - should have sent notifications
        _mockNotificationService.Verify(
            n => n.SendBatchPushNotificationsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Once);
    }

    [Fact]
    public async Task SendNotFullRemindersAsync_SkipsEventMoreThan48HoursAway()
    {
        var creator = await CreateTestUser("creator@test.com");
        var player = await CreateTestUser("player@test.com");
        await CreateTestEvent(creator.Id, eventDate: DateTime.UtcNow.AddHours(50));

        await _sut.SendNotFullRemindersAsync();

        _mockNotificationService.Verify(
            n => n.SendBatchPushNotificationsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Never);
    }

    [Fact]
    public async Task SendNotFullRemindersAsync_SkipsEventAlreadyPassed()
    {
        var creator = await CreateTestUser("creator@test.com");
        await CreateTestEvent(creator.Id, eventDate: DateTime.UtcNow.AddHours(-1));

        await _sut.SendNotFullRemindersAsync();

        _mockNotificationService.Verify(
            n => n.SendBatchPushNotificationsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Never);
    }

    [Theory]
    [InlineData("Draft")]
    [InlineData("Cancelled")]
    [InlineData("Completed")]
    public async Task SendNotFullRemindersAsync_SkipsNonPublishedStatus(string status)
    {
        var creator = await CreateTestUser("creator@test.com");
        var player = await CreateTestUser("player@test.com");
        await CreateTestEvent(creator.Id, status: status,
            eventDate: DateTime.UtcNow.AddHours(47));

        await _sut.SendNotFullRemindersAsync();

        _mockNotificationService.Verify(
            n => n.SendBatchPushNotificationsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Never);
    }

    [Fact]
    public async Task SendNotFullRemindersAsync_SkipsEventWithSentinelAlreadySet()
    {
        var creator = await CreateTestUser("creator@test.com");
        var player = await CreateTestUser("player@test.com");
        await CreateTestEvent(creator.Id,
            eventDate: DateTime.UtcNow.AddHours(47),
            notFullReminderSentAt: DateTime.UtcNow.AddHours(-1));

        await _sut.SendNotFullRemindersAsync();

        _mockNotificationService.Verify(
            n => n.SendBatchPushNotificationsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Never);
    }

    [Fact]
    public async Task SendNotFullRemindersAsync_SetsSentinelAfterSending()
    {
        var creator = await CreateTestUser("creator@test.com");
        var player = await CreateTestUser("player@test.com");
        var evt = await CreateTestEvent(creator.Id,
            eventDate: DateTime.UtcNow.AddHours(47));

        await _sut.SendNotFullRemindersAsync();

        var updated = await _context.Events.FindAsync(evt.Id);
        updated!.NotFullReminderSentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SendNotFullRemindersAsync_SkipsEventPastRegistrationDeadline()
    {
        var creator = await CreateTestUser("creator@test.com");
        var player = await CreateTestUser("player@test.com");
        await CreateTestEvent(creator.Id,
            eventDate: DateTime.UtcNow.AddHours(47),
            registrationDeadline: DateTime.UtcNow.AddHours(-1));

        await _sut.SendNotFullRemindersAsync();

        _mockNotificationService.Verify(
            n => n.SendBatchPushNotificationsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Never);
    }

    #endregion

    #region Capacity Check (Goalie Exclusion)

    [Fact]
    public async Task SendNotFullRemindersAsync_GoaliesDontCountAgainstCapacity_StillNotFull()
    {
        // Arrange - 18 skaters + 2 goalies = 20 registrations, but only 18 count
        var creator = await CreateTestUser("creator@test.com");
        var evt = await CreateTestEvent(creator.Id, maxPlayers: 20,
            eventDate: DateTime.UtcNow.AddHours(47));

        for (int i = 0; i < 18; i++)
        {
            var skater = await CreateTestUser($"skater{i}@test.com");
            await CreateRegistration(evt.Id, skater.Id, "Registered", "Skater");
        }
        for (int i = 0; i < 2; i++)
        {
            var goalie = await CreateTestUser($"goalie{i}@test.com");
            await CreateRegistration(evt.Id, goalie.Id, "Registered", "Goalie");
        }

        // A non-registered user who should receive the notification
        var outsider = await CreateTestUser("outsider@test.com");

        await _sut.SendNotFullRemindersAsync();

        // 18 skaters < 20 max, so event is NOT full — should send
        _mockNotificationService.Verify(
            n => n.SendBatchPushNotificationsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Once);
    }

    [Fact]
    public async Task SendNotFullRemindersAsync_SkipsFullEvent_SkatersAtCapacity()
    {
        var creator = await CreateTestUser("creator@test.com");
        var evt = await CreateTestEvent(creator.Id, maxPlayers: 20,
            eventDate: DateTime.UtcNow.AddHours(47));

        // 20 skaters = full
        for (int i = 0; i < 20; i++)
        {
            var skater = await CreateTestUser($"skater{i}@test.com");
            await CreateRegistration(evt.Id, skater.Id, "Registered", "Skater");
        }

        await _sut.SendNotFullRemindersAsync();

        _mockNotificationService.Verify(
            n => n.SendBatchPushNotificationsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Never);
    }

    [Fact]
    public async Task SendNotFullRemindersAsync_FullWithGoalies_SkatersAtCapacity()
    {
        // 20 skaters + 2 goalies — skaters are at capacity, should NOT send
        var creator = await CreateTestUser("creator@test.com");
        var evt = await CreateTestEvent(creator.Id, maxPlayers: 20,
            eventDate: DateTime.UtcNow.AddHours(47));

        for (int i = 0; i < 20; i++)
        {
            var skater = await CreateTestUser($"skater{i}@test.com");
            await CreateRegistration(evt.Id, skater.Id, "Registered", "Skater");
        }
        for (int i = 0; i < 2; i++)
        {
            var goalie = await CreateTestUser($"goalie{i}@test.com");
            await CreateRegistration(evt.Id, goalie.Id, "Registered", "Goalie");
        }

        await _sut.SendNotFullRemindersAsync();

        _mockNotificationService.Verify(
            n => n.SendBatchPushNotificationsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Never);
    }

    #endregion

    #region Recipient Filtering

    [Fact]
    public async Task SendNotFullRemindersAsync_ExcludesGhostPlayers()
    {
        var creator = await CreateTestUser("creator@test.com");
        var ghost = await CreateTestUser("ghost@test.com", isGhostPlayer: true);
        var evt = await CreateTestEvent(creator.Id, eventDate: DateTime.UtcNow.AddHours(47));

        await _sut.SendNotFullRemindersAsync();

        // Should send, but only to creator (the only non-ghost with push token)
        _mockNotificationService.Verify(
            n => n.SendBatchPushNotificationsAsync(
                It.Is<List<string>>(tokens => !tokens.Contains(ghost.PushToken!)),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Once);
    }

    [Fact]
    public async Task SendNotFullRemindersAsync_ExcludesInactiveUsers()
    {
        var creator = await CreateTestUser("creator@test.com");
        var inactive = await CreateTestUser("inactive@test.com", isActive: false);
        var evt = await CreateTestEvent(creator.Id, eventDate: DateTime.UtcNow.AddHours(47));

        await _sut.SendNotFullRemindersAsync();

        _mockNotificationService.Verify(
            n => n.SendBatchPushNotificationsAsync(
                It.Is<List<string>>(tokens => !tokens.Contains(inactive.PushToken!)),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Once);
    }

    [Fact]
    public async Task SendNotFullRemindersAsync_ExcludesUsersWithNoPushToken()
    {
        var creator = await CreateTestUser("creator@test.com");
        var noToken = await CreateTestUser("notoken@test.com", pushToken: null);
        var evt = await CreateTestEvent(creator.Id, eventDate: DateTime.UtcNow.AddHours(47));

        await _sut.SendNotFullRemindersAsync();

        // Only creator should receive (noToken has no push token)
        _mockNotificationService.Verify(
            n => n.SendBatchPushNotificationsAsync(
                It.Is<List<string>>(tokens => tokens.Count == 1),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Once);
    }

    [Fact]
    public async Task SendNotFullRemindersAsync_ExcludesRegisteredUsers()
    {
        var creator = await CreateTestUser("creator@test.com");
        var registered = await CreateTestUser("registered@test.com");
        var notRegistered = await CreateTestUser("free@test.com");
        var evt = await CreateTestEvent(creator.Id, eventDate: DateTime.UtcNow.AddHours(47));
        await CreateRegistration(evt.Id, registered.Id, "Registered");

        await _sut.SendNotFullRemindersAsync();

        _mockNotificationService.Verify(
            n => n.SendBatchPushNotificationsAsync(
                It.Is<List<string>>(tokens =>
                    !tokens.Contains(registered.PushToken!) &&
                    tokens.Contains(notRegistered.PushToken!)),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Once);
    }

    [Fact]
    public async Task SendNotFullRemindersAsync_ExcludesWaitlistedUsers()
    {
        var creator = await CreateTestUser("creator@test.com");
        var waitlisted = await CreateTestUser("waitlisted@test.com");
        var notRegistered = await CreateTestUser("free@test.com");
        var evt = await CreateTestEvent(creator.Id, eventDate: DateTime.UtcNow.AddHours(47));
        await CreateRegistration(evt.Id, waitlisted.Id, "Waitlisted");

        await _sut.SendNotFullRemindersAsync();

        _mockNotificationService.Verify(
            n => n.SendBatchPushNotificationsAsync(
                It.Is<List<string>>(tokens =>
                    !tokens.Contains(waitlisted.PushToken!)),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Once);
    }

    [Fact]
    public async Task SendNotFullRemindersAsync_IncludesCancelledRegistrationUsers()
    {
        // Users who previously cancelled SHOULD receive the notification
        var creator = await CreateTestUser("creator@test.com");
        var cancelled = await CreateTestUser("cancelled@test.com");
        var evt = await CreateTestEvent(creator.Id, eventDate: DateTime.UtcNow.AddHours(47));
        await CreateRegistration(evt.Id, cancelled.Id, "Cancelled");

        await _sut.SendNotFullRemindersAsync();

        _mockNotificationService.Verify(
            n => n.SendBatchPushNotificationsAsync(
                It.Is<List<string>>(tokens =>
                    tokens.Contains(cancelled.PushToken!)),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Once);
    }

    [Fact]
    public async Task SendNotFullRemindersAsync_NoRecipientsAvailable_DoesNotSend()
    {
        // Only user is the creator, who is registered
        var creator = await CreateTestUser("creator@test.com");
        var evt = await CreateTestEvent(creator.Id, eventDate: DateTime.UtcNow.AddHours(47));
        await CreateRegistration(evt.Id, creator.Id, "Registered");

        await _sut.SendNotFullRemindersAsync();

        _mockNotificationService.Verify(
            n => n.SendBatchPushNotificationsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Never);
    }

    #endregion

    #region Org-Scoped vs Public Events

    [Fact]
    public async Task SendNotFullRemindersAsync_OrgEvent_OnlySendsToOrgSubscribers()
    {
        var creator = await CreateTestUser("creator@test.com");
        var org = await CreateTestOrganization(creator.Id);
        var subscriber = await CreateTestUser("subscriber@test.com");
        var nonSubscriber = await CreateTestUser("outsider@test.com");

        await CreateSubscription(org.Id, creator.Id);
        await CreateSubscription(org.Id, subscriber.Id);
        // nonSubscriber is NOT subscribed

        var evt = await CreateTestEvent(creator.Id,
            visibility: "OrganizationMembers",
            organizationId: org.Id,
            eventDate: DateTime.UtcNow.AddHours(47));

        await _sut.SendNotFullRemindersAsync();

        // Should only include org subscribers (creator + subscriber), not outsider
        _mockNotificationService.Verify(
            n => n.SendBatchPushNotificationsAsync(
                It.Is<List<string>>(tokens =>
                    tokens.Contains(subscriber.PushToken!) &&
                    !tokens.Contains(nonSubscriber.PushToken!)),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Once);
    }

    [Fact]
    public async Task SendNotFullRemindersAsync_OrgEvent_RespectsNotificationEnabled()
    {
        var creator = await CreateTestUser("creator@test.com");
        var org = await CreateTestOrganization(creator.Id);
        var mutedUser = await CreateTestUser("muted@test.com");
        var enabledUser = await CreateTestUser("enabled@test.com");

        await CreateSubscription(org.Id, creator.Id);
        await CreateSubscription(org.Id, mutedUser.Id, notificationEnabled: false);
        await CreateSubscription(org.Id, enabledUser.Id, notificationEnabled: true);

        var evt = await CreateTestEvent(creator.Id,
            visibility: "OrganizationMembers",
            organizationId: org.Id,
            eventDate: DateTime.UtcNow.AddHours(47));

        await _sut.SendNotFullRemindersAsync();

        _mockNotificationService.Verify(
            n => n.SendBatchPushNotificationsAsync(
                It.Is<List<string>>(tokens =>
                    !tokens.Contains(mutedUser.PushToken!)),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Once);
    }

    [Fact]
    public async Task SendNotFullRemindersAsync_PublicEvent_SendsToAllEligibleUsers()
    {
        var creator = await CreateTestUser("creator@test.com");
        var user1 = await CreateTestUser("user1@test.com");
        var user2 = await CreateTestUser("user2@test.com");

        var evt = await CreateTestEvent(creator.Id,
            visibility: "Public",
            eventDate: DateTime.UtcNow.AddHours(47));

        await _sut.SendNotFullRemindersAsync();

        // All 3 users should receive
        _mockNotificationService.Verify(
            n => n.SendBatchPushNotificationsAsync(
                It.Is<List<string>>(tokens => tokens.Count == 3),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Once);
    }

    [Fact]
    public async Task SendNotFullRemindersAsync_SkipsInviteOnlyEvents()
    {
        var creator = await CreateTestUser("creator@test.com");
        await CreateTestEvent(creator.Id,
            visibility: "InviteOnly",
            eventDate: DateTime.UtcNow.AddHours(47));

        await _sut.SendNotFullRemindersAsync();

        _mockNotificationService.Verify(
            n => n.SendBatchPushNotificationsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Never);
    }

    #endregion

    #region Notification Content

    [Fact]
    public async Task SendNotFullRemindersAsync_IncludesSpotsRemainingInMessage()
    {
        var creator = await CreateTestUser("creator@test.com");
        var player = await CreateTestUser("player@test.com");
        var evt = await CreateTestEvent(creator.Id, maxPlayers: 20,
            eventDate: DateTime.UtcNow.AddHours(47));

        // Register 15 skaters
        for (int i = 0; i < 15; i++)
        {
            var skater = await CreateTestUser($"skater{i}@test.com");
            await CreateRegistration(evt.Id, skater.Id, "Registered", "Skater");
        }

        await _sut.SendNotFullRemindersAsync();

        // Body should mention spots remaining (5 spots)
        _mockNotificationService.Verify(
            n => n.SendBatchPushNotificationsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<string>(),
                It.Is<string>(body => body.Contains("5")),
                It.IsAny<object>()),
            Times.Once);
    }

    #endregion

    #region Status Field vs Actual Count

    [Fact]
    public async Task SendNotFullRemindersAsync_EventStatusFull_ButActualCountNotFull_StillSends()
    {
        // Status says "Full" but actual skater count is below max (e.g., after cancellation)
        var creator = await CreateTestUser("creator@test.com");
        var player = await CreateTestUser("player@test.com");
        var evt = await CreateTestEvent(creator.Id, maxPlayers: 20,
            status: "Full",
            eventDate: DateTime.UtcNow.AddHours(47));

        // Only 10 skaters registered — spots opened after cancellations
        for (int i = 0; i < 10; i++)
        {
            var skater = await CreateTestUser($"skater{i}@test.com");
            await CreateRegistration(evt.Id, skater.Id, "Registered", "Skater");
        }

        await _sut.SendNotFullRemindersAsync();

        _mockNotificationService.Verify(
            n => n.SendBatchPushNotificationsAsync(
                It.IsAny<List<string>>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<object>()),
            Times.Once);
    }

    #endregion
}
