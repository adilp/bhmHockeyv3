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
/// Tests for RosterPublishService - protecting roster auto-publish and organizer reminder logic.
/// These tests ensure:
/// - Organizer reminders sent at correct time windows (24h, 8h, 5h)
/// - Reminders not re-sent (tracking fields prevent duplicates)
/// - Auto-publish triggers at 2h before event
/// - Auto-publish sets IsRosterPublished and PublishedAt
/// - Published events don't receive reminders
/// </summary>
public class RosterPublishServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<ILogger<RosterPublishService>> _mockLogger;
    private readonly RosterPublishService _sut;

    public RosterPublishServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);
        _mockNotificationService = new Mock<INotificationService>();
        _mockLogger = new Mock<ILogger<RosterPublishService>>();

        _sut = new RosterPublishService(_context, _mockNotificationService.Object, _mockLogger.Object);
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
        DateTime? eventDate = null,
        bool isRosterPublished = false,
        string status = "Published",
        string name = "Test Event")
    {
        var evt = new Event
        {
            Id = Guid.NewGuid(),
            CreatorId = creatorId,
            Name = name,
            Description = "Test Description",
            EventDate = eventDate ?? DateTime.UtcNow.AddDays(7),
            Duration = 60,
            Venue = "Test Venue",
            MaxPlayers = 20,
            Cost = 25.00m,
            Status = status,
            Visibility = "Public",
            IsRosterPublished = isRosterPublished,
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
        string? teamAssignment = null)
    {
        var registration = new EventRegistration
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            UserId = userId,
            Status = status,
            WaitlistPosition = waitlistPosition,
            TeamAssignment = teamAssignment,
            RegisteredPosition = "Skater",
            RegisteredAt = DateTime.UtcNow
        };

        _context.EventRegistrations.Add(registration);
        await _context.SaveChangesAsync();
        return registration;
    }

    #endregion

    #region SendOrganizerPublishRemindersAsync Tests - 24h Reminder

    [Fact]
    public async Task SendOrganizerPublishRemindersAsync_EventIn24Hours_SendsReminder()
    {
        // Arrange - Event starting 24.5 hours from now (within 24-25h window)
        var creator = await CreateTestUser("creator@example.com", pushToken: "ExponentPushToken[creator]");
        var eventDate = DateTime.UtcNow.AddHours(24.5);
        var evt = await CreateTestEvent(creator.Id, eventDate: eventDate, isRosterPublished: false);

        // Act
        await _sut.SendOrganizerPublishRemindersAsync();

        // Assert - Notification sent to organizer
        _mockNotificationService.Verify(
            n => n.SendPushNotificationAsync(
                "ExponentPushToken[creator]",
                "Roster Not Published",
                It.Is<string>(s => s.Contains("24 hours")),
                It.IsAny<object>(),
                creator.Id,
                "publish_reminder",
                evt.OrganizationId,
                evt.Id),
            Times.Once);

        // Assert - Reminder timestamp set
        var updatedEvent = await _context.Events.FindAsync(evt.Id);
        updatedEvent!.OrganizerPublishReminder24hSentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SendOrganizerPublishRemindersAsync_ReminderAlreadySent_DoesNotResend()
    {
        // Arrange - Event with reminder already sent
        var creator = await CreateTestUser("creator@example.com", pushToken: "ExponentPushToken[creator]");
        var eventDate = DateTime.UtcNow.AddHours(24.5);
        var evt = await CreateTestEvent(creator.Id, eventDate: eventDate, isRosterPublished: false);

        // Mark reminder as already sent
        evt.OrganizerPublishReminder24hSentAt = DateTime.UtcNow.AddMinutes(-30);
        await _context.SaveChangesAsync();

        // Act
        await _sut.SendOrganizerPublishRemindersAsync();

        // Assert - No notification sent
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
    public async Task SendOrganizerPublishRemindersAsync_PublishedRoster_NoReminder()
    {
        // Arrange - Event with roster already published
        var creator = await CreateTestUser("creator@example.com", pushToken: "ExponentPushToken[creator]");
        var eventDate = DateTime.UtcNow.AddHours(24.5);
        var evt = await CreateTestEvent(creator.Id, eventDate: eventDate, isRosterPublished: true);

        // Act
        await _sut.SendOrganizerPublishRemindersAsync();

        // Assert - No notification sent
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
    public async Task SendOrganizerPublishRemindersAsync_EventOutsideWindow_NoReminder()
    {
        // Arrange - Event starting 26 hours from now (outside 24-25h window)
        var creator = await CreateTestUser("creator@example.com", pushToken: "ExponentPushToken[creator]");
        var eventDate = DateTime.UtcNow.AddHours(26);
        await CreateTestEvent(creator.Id, eventDate: eventDate, isRosterPublished: false);

        // Act
        await _sut.SendOrganizerPublishRemindersAsync();

        // Assert - No notification sent (outside window)
        _mockNotificationService.Verify(
            n => n.SendPushNotificationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.Is<string>(s => s.Contains("24 hours")),
                It.IsAny<object>(),
                It.IsAny<Guid?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>()),
            Times.Never);
    }

    [Fact]
    public async Task SendOrganizerPublishRemindersAsync_OrganizerNoPushToken_MarksAsSentWithoutSending()
    {
        // Arrange - Organizer has no push token
        var creator = await CreateTestUser("creator@example.com", pushToken: null);
        var eventDate = DateTime.UtcNow.AddHours(24.5);
        var evt = await CreateTestEvent(creator.Id, eventDate: eventDate, isRosterPublished: false);

        // Act
        await _sut.SendOrganizerPublishRemindersAsync();

        // Assert - No notification sent
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

        // Assert - But reminder timestamp IS set (to prevent re-query)
        var updatedEvent = await _context.Events.FindAsync(evt.Id);
        updatedEvent!.OrganizerPublishReminder24hSentAt.Should().NotBeNull();
    }

    #endregion

    #region SendOrganizerPublishRemindersAsync Tests - Multiple Windows

    [Fact]
    public async Task SendOrganizerPublishRemindersAsync_EventIn8Hours_Sends8hReminder()
    {
        // Arrange - Event starting 8.5 hours from now (within 8-9h window)
        var creator = await CreateTestUser("creator@example.com", pushToken: "ExponentPushToken[creator]");
        var eventDate = DateTime.UtcNow.AddHours(8.5);
        var evt = await CreateTestEvent(creator.Id, eventDate: eventDate, isRosterPublished: false);

        // Act
        await _sut.SendOrganizerPublishRemindersAsync();

        // Assert - 8h reminder sent
        _mockNotificationService.Verify(
            n => n.SendPushNotificationAsync(
                "ExponentPushToken[creator]",
                "Roster Not Published",
                It.Is<string>(s => s.Contains("8 hours")),
                It.IsAny<object>(),
                creator.Id,
                "publish_reminder",
                evt.OrganizationId,
                evt.Id),
            Times.Once);

        // Assert - 8h timestamp set
        var updatedEvent = await _context.Events.FindAsync(evt.Id);
        updatedEvent!.OrganizerPublishReminder8hSentAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SendOrganizerPublishRemindersAsync_EventIn5Hours_Sends5hReminder()
    {
        // Arrange - Event starting 5.5 hours from now (within 5-6h window)
        var creator = await CreateTestUser("creator@example.com", pushToken: "ExponentPushToken[creator]");
        var eventDate = DateTime.UtcNow.AddHours(5.5);
        var evt = await CreateTestEvent(creator.Id, eventDate: eventDate, isRosterPublished: false);

        // Act
        await _sut.SendOrganizerPublishRemindersAsync();

        // Assert - 5h reminder sent
        _mockNotificationService.Verify(
            n => n.SendPushNotificationAsync(
                "ExponentPushToken[creator]",
                "Roster Not Published",
                It.Is<string>(s => s.Contains("5 hours")),
                It.IsAny<object>(),
                creator.Id,
                "publish_reminder",
                evt.OrganizationId,
                evt.Id),
            Times.Once);

        // Assert - 5h timestamp set
        var updatedEvent = await _context.Events.FindAsync(evt.Id);
        updatedEvent!.OrganizerPublishReminder5hSentAt.Should().NotBeNull();
    }

    #endregion

    #region ProcessAutoPublishAsync Tests

    [Fact]
    public async Task ProcessAutoPublishAsync_EventWithin2Hours_PublishesRoster()
    {
        // Arrange - Event starting 1.5 hours from now
        var creator = await CreateTestUser("creator@example.com");
        var eventDate = DateTime.UtcNow.AddHours(1.5);
        var evt = await CreateTestEvent(creator.Id, eventDate: eventDate, isRosterPublished: false);

        // Act
        await _sut.ProcessAutoPublishAsync();

        // Assert - Roster is published
        var updatedEvent = await _context.Events.FindAsync(evt.Id);
        updatedEvent!.IsRosterPublished.Should().BeTrue();
        updatedEvent.PublishedAt.Should().NotBeNull();
        updatedEvent.PublishedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ProcessAutoPublishAsync_EventAlreadyPublished_NoChange()
    {
        // Arrange - Event already published
        var creator = await CreateTestUser("creator@example.com", pushToken: "ExponentPushToken[creator]");
        var eventDate = DateTime.UtcNow.AddHours(1.5);
        var evt = await CreateTestEvent(creator.Id, eventDate: eventDate, isRosterPublished: true);

        var player = await CreateTestUser("player@example.com", pushToken: "ExponentPushToken[player]");
        await CreateRegistration(evt.Id, player.Id, status: "Registered");

        // Act
        await _sut.ProcessAutoPublishAsync();

        // Assert - No notifications sent
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
    public async Task ProcessAutoPublishAsync_EventTooFarOut_NoChange()
    {
        // Arrange - Event starting 3 hours from now (outside 2h window)
        var creator = await CreateTestUser("creator@example.com");
        var eventDate = DateTime.UtcNow.AddHours(3);
        var evt = await CreateTestEvent(creator.Id, eventDate: eventDate, isRosterPublished: false);

        // Act
        await _sut.ProcessAutoPublishAsync();

        // Assert - Roster still unpublished
        var updatedEvent = await _context.Events.FindAsync(evt.Id);
        updatedEvent!.IsRosterPublished.Should().BeFalse();
        updatedEvent.PublishedAt.Should().BeNull();
    }

    [Fact]
    public async Task ProcessAutoPublishAsync_EventAlreadyStarted_NoChange()
    {
        // Arrange - Event already started (past)
        var creator = await CreateTestUser("creator@example.com");
        var eventDate = DateTime.UtcNow.AddMinutes(-30);
        var evt = await CreateTestEvent(creator.Id, eventDate: eventDate, isRosterPublished: false);

        // Act
        await _sut.ProcessAutoPublishAsync();

        // Assert - Roster still unpublished (we don't auto-publish past events)
        var updatedEvent = await _context.Events.FindAsync(evt.Id);
        updatedEvent!.IsRosterPublished.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessAutoPublishAsync_SendsNotificationsToPlayers()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var eventDate = DateTime.UtcNow.AddHours(1.5);
        var evt = await CreateTestEvent(creator.Id, eventDate: eventDate, isRosterPublished: false, name: "Hockey Night");

        var player1 = await CreateTestUser("player1@example.com", pushToken: "ExponentPushToken[player1]");
        var player2 = await CreateTestUser("player2@example.com", pushToken: "ExponentPushToken[player2]");
        await CreateRegistration(evt.Id, player1.Id, status: "Registered", teamAssignment: "Black");
        await CreateRegistration(evt.Id, player2.Id, status: "Registered", teamAssignment: "White");

        // Act
        await _sut.ProcessAutoPublishAsync();

        // Assert - Notifications sent to both players with team assignments
        _mockNotificationService.Verify(
            n => n.SendPushNotificationAsync(
                "ExponentPushToken[player1]",
                "Roster Published",
                It.Is<string>(s => s.Contains("Hockey Night") && s.Contains("Team Black")),
                It.IsAny<object>(),
                player1.Id,
                "roster_auto_published",
                evt.OrganizationId,
                evt.Id),
            Times.Once);

        _mockNotificationService.Verify(
            n => n.SendPushNotificationAsync(
                "ExponentPushToken[player2]",
                "Roster Published",
                It.Is<string>(s => s.Contains("Hockey Night") && s.Contains("Team White")),
                It.IsAny<object>(),
                player2.Id,
                "roster_auto_published",
                evt.OrganizationId,
                evt.Id),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAutoPublishAsync_SendsNotificationsToWaitlistedPlayers()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var eventDate = DateTime.UtcNow.AddHours(1.5);
        var evt = await CreateTestEvent(creator.Id, eventDate: eventDate, isRosterPublished: false, name: "Hockey Night");

        var waitlistedPlayer = await CreateTestUser("waitlisted@example.com", pushToken: "ExponentPushToken[waitlisted]");
        await CreateRegistration(evt.Id, waitlistedPlayer.Id, status: "Waitlisted", waitlistPosition: 3);

        // Act
        await _sut.ProcessAutoPublishAsync();

        // Assert - Notification includes waitlist position
        _mockNotificationService.Verify(
            n => n.SendPushNotificationAsync(
                "ExponentPushToken[waitlisted]",
                "Roster Published",
                It.Is<string>(s => s.Contains("#3") && s.Contains("waitlist")),
                It.IsAny<object>(),
                waitlistedPlayer.Id,
                "roster_auto_published",
                evt.OrganizationId,
                evt.Id),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAutoPublishAsync_SkipsCancelledRegistrations()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var eventDate = DateTime.UtcNow.AddHours(1.5);
        var evt = await CreateTestEvent(creator.Id, eventDate: eventDate, isRosterPublished: false);

        var cancelledPlayer = await CreateTestUser("cancelled@example.com", pushToken: "ExponentPushToken[cancelled]");
        await CreateRegistration(evt.Id, cancelledPlayer.Id, status: "Cancelled");

        // Act
        await _sut.ProcessAutoPublishAsync();

        // Assert - No notification to cancelled player
        _mockNotificationService.Verify(
            n => n.SendPushNotificationAsync(
                "ExponentPushToken[cancelled]",
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
    public async Task ProcessAutoPublishAsync_PlayerNoTeamAssignment_GenericMessage()
    {
        // Arrange
        var creator = await CreateTestUser("creator@example.com");
        var eventDate = DateTime.UtcNow.AddHours(1.5);
        var evt = await CreateTestEvent(creator.Id, eventDate: eventDate, isRosterPublished: false, name: "Hockey Night");

        var player = await CreateTestUser("player@example.com", pushToken: "ExponentPushToken[player]");
        await CreateRegistration(evt.Id, player.Id, status: "Registered", teamAssignment: null);

        // Act
        await _sut.ProcessAutoPublishAsync();

        // Assert - Generic message without team
        _mockNotificationService.Verify(
            n => n.SendPushNotificationAsync(
                "ExponentPushToken[player]",
                "Roster Published",
                It.Is<string>(s => s.Contains("Check app for details") && !s.Contains("Team")),
                It.IsAny<object>(),
                player.Id,
                "roster_auto_published",
                evt.OrganizationId,
                evt.Id),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAutoPublishAsync_DraftStatusEvent_NotProcessed()
    {
        // Arrange - Event in Draft status (not Published)
        var creator = await CreateTestUser("creator@example.com");
        var eventDate = DateTime.UtcNow.AddHours(1.5);
        var evt = await CreateTestEvent(creator.Id, eventDate: eventDate, isRosterPublished: false, status: "Draft");

        // Act
        await _sut.ProcessAutoPublishAsync();

        // Assert - Event not processed
        var updatedEvent = await _context.Events.FindAsync(evt.Id);
        updatedEvent!.IsRosterPublished.Should().BeFalse();
    }

    #endregion
}
