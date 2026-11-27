using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// The owner: 2026-11-22 "does not exist in the app as an event because it was
/// invite only at the time. Please add it with the historic data" - with
/// nobody notified, billed or confused. These tests prove the writer touches
/// only Events + EventRegistrations, and that every background job that could
/// message a member passes the new event by.
/// </summary>
public class PastGameServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly PastGameService _sut;
    private readonly Organization _org;
    private readonly User _admin, _alice, _bob, _ghost;
    private static readonly DateTime Played = new(2026, 11, 23, 2, 15, 0, DateTimeKind.Utc); // 11/22 8:15 PM Central

    public PastGameServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new AppDbContext(options);
        _sut = new PastGameService(_context, new OrganizationAdminService(_context), NullLogger<PastGameService>.Instance);

        _admin = Add(new User { Email = "a@x.test", FirstName = "Pub", LastName = "Lisher" });
        _alice = Add(new User { Email = "alice@x.test", FirstName = "Alice", LastName = "Able", PushToken = "ExponentPushToken[a]" });
        _bob = Add(new User { Email = "bob@x.test", FirstName = "Bob", LastName = "Baker", PushToken = "ExponentPushToken[b]" });
        _ghost = Add(new User { Email = "g@x.test", FirstName = "Ghost", LastName = "Guest", IsGhostPlayer = true });
        _org = new Organization { Name = "Sample League", CreatorId = _admin.Id };
        _context.Organizations.Add(_org);
        _context.OrganizationAdmins.Add(new OrganizationAdmin { OrganizationId = _org.Id, UserId = _admin.Id });
        _context.OrganizationSubscriptions.Add(new OrganizationSubscription { OrganizationId = _org.Id, UserId = _alice.Id, NotificationEnabled = true });
        _context.OrganizationSubscriptions.Add(new OrganizationSubscription { OrganizationId = _org.Id, UserId = _bob.Id, NotificationEnabled = true });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    private User Add(User u) { _context.Users.Add(u); _context.SaveChanges(); return u; }

    private CreatePastGameRequest Request(DateTime? when = null) => new(
        "Home vs Visitors", "Club game, invite only at the time", when ?? Played, 75, "Sample Rink",
        null, "2026-11-22_main",
        new List<PastGameRosterEntry>
        {
            new(_alice.Id, "White", "Skater"),
            new(_bob.Id, "black", "goalie"),
            new(_ghost.Id, "Black", "Skater"),
            new(Guid.NewGuid(), "Black", "Skater"),
        });

    [Fact]
    public async Task ItCreatesAPublishedPastEventWithItsRosterAndNothingElse()
    {
        var res = await _sut.CreatePastGameAsync(_org.Id, Request(), _admin.Id);

        res!.Outcome.Should().Be("created");
        res.RegisteredCount.Should().Be(2);
        res.SkippedUserIds.Should().HaveCount(2);                     // the ghost and the unknown id
        var evt = await _context.Events.Include(e => e.Registrations).SingleAsync();
        evt.Status.Should().Be("Published");
        evt.IsRosterPublished.Should().BeTrue();
        evt.Visibility.Should().Be("OrganizationMembers");
        evt.Cost.Should().Be(0);
        evt.EventDate.Should().Be(Played);
        evt.Registrations.Should().OnlyContain(r => r.Status == "Registered" && r.PaymentStatus == null
                                                     && r.WaitlistPosition == null && r.PaymentDeadlineAt == null);
        evt.Registrations.Single(r => r.UserId == _bob.Id).RegisteredPosition.Should().Be("Goalie");
        evt.Registrations.Single(r => r.UserId == _bob.Id).TeamAssignment.Should().Be("Black");

        (await _context.Notifications.CountAsync()).Should().Be(0);
        (await _context.UserBadges.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ASecondCallForTheSameGameWritesNothing()
    {
        var first = await _sut.CreatePastGameAsync(_org.Id, Request(), _admin.Id);
        var second = await _sut.CreatePastGameAsync(_org.Id, Request(), _admin.Id);

        second!.Outcome.Should().Be("already_exists");
        second.EventId.Should().Be(first!.EventId);
        (await _context.Events.CountAsync()).Should().Be(1);
        (await _context.EventRegistrations.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task OnlyAnOrganizationAdminMayAddAPastGame()
    {
        await FluentActions.Awaiting(() => _sut.CreatePastGameAsync(_org.Id, Request(), _alice.Id))
            .Should().ThrowAsync<UnauthorizedAccessException>();
        (await _sut.CreatePastGameAsync(Guid.NewGuid(), Request(), _admin.Id)).Should().BeNull();
    }

    [Fact]
    public async Task AFutureGameAnOlderGameOrAZonelessTimeIsRefused()
    {
        foreach (var when in new[]
                 {
                     DateTime.UtcNow.AddDays(1),
                     new DateTime(2026, 11, 8, 2, 0, 0, DateTimeKind.Utc),
                     new DateTime(2026, 11, 22, 20, 15, 0, DateTimeKind.Unspecified),
                 })
        {
            await FluentActions.Awaiting(() => _sut.CreatePastGameAsync(_org.Id, Request(when), _admin.Id))
                .Should().ThrowAsync<InvalidOperationException>();
        }
        (await _context.Events.CountAsync()).Should().Be(0);
    }

    [Fact]
    public void TheWriterCannotReachAnyServiceThatMessagesOrBills()
    {
        // Structural: its only collaborators are the database and the admin check.
        var deps = typeof(PastGameService).GetConstructors().Single().GetParameters().Select(p => p.ParameterType).ToList();
        deps.Should().BeEquivalentTo(new[]
        {
            typeof(AppDbContext), typeof(IOrganizationAdminService), typeof(Microsoft.Extensions.Logging.ILogger<PastGameService>),
        });
    }

    [Fact]
    public async Task NoBackgroundJobMessagesAnyoneAboutTheNewPastGame()
    {
        await _sut.CreatePastGameAsync(_org.Id, Request(), _admin.Id);
        var push = new Mock<INotificationService>(MockBehavior.Strict);

        await new EventReminderService(_context, push.Object, NullLogger<EventReminderService>.Instance).SendPlayerRemindersAsync();
        await new EventReminderService(_context, push.Object, NullLogger<EventReminderService>.Instance).SendOrganizerPaymentRemindersAsync();
        await new NotFullGameReminderService(_context, push.Object, NullLogger<NotFullGameReminderService>.Instance).SendNotFullRemindersAsync();
        await new RosterPublishService(_context, push.Object, NullLogger<RosterPublishService>.Instance).SendOrganizerPublishRemindersAsync();
        await new RosterPublishService(_context, push.Object, NullLogger<RosterPublishService>.Instance).ProcessAutoPublishAsync();
        await new WaitlistService(_context, push.Object, NullLogger<WaitlistService>.Instance).ProcessExpiredPaymentDeadlinesAsync();

        push.VerifyNoOtherCalls();                                  // Strict: any call would have thrown
        (await _context.Notifications.CountAsync()).Should().Be(0);
    }
}
