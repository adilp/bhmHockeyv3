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

    // ------------------------------------------------- cost and payment (optional)

    /// <summary>The two registered players, with a stated payment each and a cost.</summary>
    private CreatePastGameRequest Paid(decimal? cost, string? alice = "Verified", string? bob = "Verified", bool correct = false) =>
        new("Home vs Visitors", null, Played, 75, null, null, "sample_game", new List<PastGameRosterEntry>
        {
            new(_alice.Id, "White", "Skater") { PaymentStatus = alice },
            new(_bob.Id, "Black", "Goalie") { PaymentStatus = bob },
        }) { Cost = cost, CorrectPayment = correct };

    [Fact]
    public async Task APaidGameRecordsItsCostAndEachStatedPaymentWithNoDeadline()
    {
        var res = await _sut.CreatePastGameAsync(_org.Id, Paid(20m, "Verified", "Pending"), _admin.Id);

        res!.Outcome.Should().Be("created");
        var evt = await _context.Events.Include(e => e.Registrations).SingleAsync();
        evt.Cost.Should().Be(20m);
        var alice = evt.Registrations.Single(r => r.UserId == _alice.Id);
        alice.PaymentStatus.Should().Be("Verified");
        alice.PaymentVerifiedAt.Should().NotBeNull();
        evt.Registrations.Single(r => r.UserId == _bob.Id).PaymentStatus.Should().Be("Pending");
        evt.Registrations.Should().OnlyContain(r => r.PaymentDeadlineAt == null && r.PaymentMarkedAt == null);
        (await _context.Notifications.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task NothingAboutPaymentIsAssumed()
    {
        foreach (var bad in new[]
                 {
                     Paid(20m, "Verified", null),        // a cost, but one player's payment not stated
                     Paid(20m, "Paid", "Verified"),      // not a status the app knows
                     Paid(20m, "MarkedPaid", "Verified"),// a state that would ask the organizer to verify
                     Paid(null, "Verified", "Verified"), // a payment with no cost
                     Paid(0m, "Verified", "Verified"),
                     Paid(-1m), Paid(20.005m), Paid(5000m),
                 })
        {
            await FluentActions.Awaiting(() => _sut.CreatePastGameAsync(_org.Id, bad, _admin.Id))
                .Should().ThrowAsync<InvalidOperationException>();
        }
        (await _context.Events.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AFreePastGameCanBeCorrectedToPaidOnceAndOnlyItsPaymentChanges()
    {
        var created = await _sut.CreatePastGameAsync(_org.Id, Request(), _admin.Id);    // free, as first sent
        var before = await _context.Events.AsNoTracking().Include(e => e.Registrations).SingleAsync();

        // Without the flag an existing game is untouched, as before
        (await _sut.CreatePastGameAsync(_org.Id, Paid(20m), _admin.Id))!.Outcome.Should().Be("already_exists");
        (await _context.Events.AsNoTracking().SingleAsync()).Cost.Should().Be(0);

        var res = await _sut.CreatePastGameAsync(_org.Id, Paid(20m, correct: true), _admin.Id);
        res!.Outcome.Should().Be("payment_corrected");
        res.EventId.Should().Be(created!.EventId);
        res.RegisteredCount.Should().Be(2);

        var after = await _context.Events.AsNoTracking().Include(e => e.Registrations).SingleAsync();
        after.Cost.Should().Be(20m);
        after.Registrations.Should().OnlyContain(r => r.PaymentStatus == "Verified" && r.PaymentVerifiedAt != null
                                                      && r.PaymentDeadlineAt == null && r.PaymentMarkedAt == null);
        // Nothing but cost and payment moved
        after.Status.Should().Be(before.Status);
        after.EventDate.Should().Be(before.EventDate);
        after.Visibility.Should().Be(before.Visibility);
        after.IsRosterPublished.Should().BeTrue();
        foreach (var r in after.Registrations)
        {
            var b = before.Registrations.Single(x => x.Id == r.Id);
            (r.Status, r.TeamAssignment, r.RegisteredPosition, r.RosterOrder, r.WaitlistPosition, r.RegisteredAt)
                .Should().Be((b.Status, b.TeamAssignment, b.RegisteredPosition, b.RosterOrder, b.WaitlistPosition, b.RegisteredAt));
        }
        (await _context.EventRegistrations.CountAsync()).Should().Be(2);
        (await _context.Notifications.CountAsync()).Should().Be(0);

        // The same correction again writes nothing
        (await _sut.CreatePastGameAsync(_org.Id, Paid(20m, correct: true), _admin.Id))!.Outcome.Should().Be("unchanged");
    }

    [Fact]
    public async Task ACorrectionIsRefusedUnlessItIsExactAndTheGameCameFromThisRoute()
    {
        await _sut.CreatePastGameAsync(_org.Id, Request(), _admin.Id);
        var refused = new[]
        {
            Paid(null, null, null, correct: true),                                     // no cost stated
            new CreatePastGameRequest("x", null, Played, 75, null, null, "sample_game",
                new List<PastGameRosterEntry> { new(_alice.Id, "White", "Skater") { PaymentStatus = "Verified" } })
                { Cost = 20m, CorrectPayment = true },                                 // a registered player left out
            new CreatePastGameRequest("x", null, Played, 75, null, null, "sample_game", new List<PastGameRosterEntry>
                {
                    new(_alice.Id, "White", "Skater") { PaymentStatus = "Verified" },
                    new(_bob.Id, "Black", "Skater") { PaymentStatus = "Verified" },     // he is registered as a goalie
                }) { Cost = 20m, CorrectPayment = true },
        };
        foreach (var r in refused)
            await FluentActions.Awaiting(() => _sut.CreatePastGameAsync(_org.Id, r, _admin.Id))
                .Should().ThrowAsync<InvalidOperationException>();
        await FluentActions.Awaiting(() => _sut.CreatePastGameAsync(_org.Id, Paid(20m, correct: true), _alice.Id))
            .Should().ThrowAsync<UnauthorizedAccessException>();
        (await _context.Events.AsNoTracking().SingleAsync()).Cost.Should().Be(0);
        (await _context.EventRegistrations.AsNoTracking().ToListAsync()).Should().OnlyContain(r => r.PaymentStatus == null);

        // An ordinary event at the same time (made through the normal routes) is never corrected here
        var ctx2 = new PastGameServiceTests();
        var normal = new Event { OrganizationId = ctx2._org.Id, CreatorId = ctx2._admin.Id, EventDate = Played, Status = "Published",
            CreatedAt = Played.AddDays(-3), Cost = 0 };
        ctx2._context.Events.Add(normal);
        ctx2._context.EventRegistrations.Add(new EventRegistration { EventId = normal.Id, UserId = ctx2._alice.Id, Status = "Registered", RegisteredPosition = "Skater" });
        ctx2._context.EventRegistrations.Add(new EventRegistration { EventId = normal.Id, UserId = ctx2._bob.Id, Status = "Registered", RegisteredPosition = "Goalie" });
        await ctx2._context.SaveChangesAsync();
        await FluentActions.Awaiting(() => ctx2._sut.CreatePastGameAsync(ctx2._org.Id, ctx2.Paid(20m, correct: true), ctx2._admin.Id))
            .Should().ThrowAsync<InvalidOperationException>();
        (await ctx2._context.Events.AsNoTracking().SingleAsync()).Cost.Should().Be(0);
        ctx2.Dispose();
    }

    [Fact]
    public async Task NoBackgroundJobMessagesAnyoneAfterAPaymentCorrection()
    {
        await _sut.CreatePastGameAsync(_org.Id, Request(), _admin.Id);
        await _sut.CreatePastGameAsync(_org.Id, Paid(20m, correct: true), _admin.Id);
        await RunEveryJobStrict();
    }

    [Fact]
    public async Task NoBackgroundJobMessagesAnyoneAboutAPaidPastGameEvenWithAPlayerOwing()
    {
        await _sut.CreatePastGameAsync(_org.Id, Paid(20m, "Verified", "Pending"), _admin.Id);
        await RunEveryJobStrict();
    }

    private async Task RunEveryJobStrict()
    {
        var push = new Mock<INotificationService>(MockBehavior.Strict);
        await new EventReminderService(_context, push.Object, NullLogger<EventReminderService>.Instance).SendPlayerRemindersAsync();
        await new EventReminderService(_context, push.Object, NullLogger<EventReminderService>.Instance).SendOrganizerPaymentRemindersAsync();
        await new NotFullGameReminderService(_context, push.Object, NullLogger<NotFullGameReminderService>.Instance).SendNotFullRemindersAsync();
        await new RosterPublishService(_context, push.Object, NullLogger<RosterPublishService>.Instance).SendOrganizerPublishRemindersAsync();
        await new RosterPublishService(_context, push.Object, NullLogger<RosterPublishService>.Instance).ProcessAutoPublishAsync();
        await new WaitlistService(_context, push.Object, NullLogger<WaitlistService>.Instance).ProcessExpiredPaymentDeadlinesAsync();
        push.VerifyNoOtherCalls();
        (await _context.Notifications.CountAsync()).Should().Be(0);
        (await _context.EventRegistrations.CountAsync(r => r.Status != "Registered")).Should().Be(0);
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
