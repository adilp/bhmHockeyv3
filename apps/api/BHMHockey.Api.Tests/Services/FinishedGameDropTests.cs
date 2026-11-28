using System.Security.Claims;
using BHMHockey.Api.Controllers;
using BHMHockey.Api.Data;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// The owner: unpaid players are not cancelled after the game, and players cannot
/// drop from a finished game. A player's own cancel (DELETE /events/{id}/register)
/// is refused once EventService.HasEnded, with EventService.GameOverMessage, and the
/// expired-payment job leaves a finished game alone. Pinned as unchanged on a
/// finished game: an organizer's removal, an organizer's payment verify, leaving an
/// organization, guests. (The past-game route, the result routes, payments and the
/// reads are pinned in FinishedGameAddPlayerTests.) Invented data, relative dates.
/// </summary>
public class FinishedGameDropTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Organization _org;
    private readonly User _organizer, _player, _other;

    public FinishedGameDropTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new AppDbContext(options);

        _organizer = AddUser("Orla", "Organizer", "ExponentPushToken[o]");
        _player = AddUser("Pia", "Player", "ExponentPushToken[p]");
        _other = AddUser("Gus", "Other", "ExponentPushToken[m]");
        _org = new Organization { Name = "Example League", CreatorId = _organizer.Id };
        _context.Organizations.Add(_org);
        _context.OrganizationAdmins.Add(new OrganizationAdmin { OrganizationId = _org.Id, UserId = _organizer.Id });
        _context.OrganizationSubscriptions.Add(new OrganizationSubscription { OrganizationId = _org.Id, UserId = _player.Id });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    // ----------------------------------------------------------------- helpers

    private User AddUser(string first, string last, string? pushToken)
    {
        var u = new User
        {
            Email = $"{first}.{last}@example.test".ToLowerInvariant(), FirstName = first, LastName = last,
            PushToken = pushToken, Role = "Player", IsActive = true,
            Positions = new Dictionary<string, string> { ["skater"] = "Silver" },
        };
        _context.Users.Add(u);
        _context.SaveChanges();
        return u;
    }

    private WaitlistService Waitlist(INotificationService push, ILogger<WaitlistService>? log = null) =>
        new(_context, push, log ?? NullLogger<WaitlistService>.Instance);

    private EventService Events(INotificationService push) => new(_context, push, new OrganizationAdminService(_context),
        Waitlist(push),
        new OrganizationWaiverService(_context, new OrganizationAdminService(_context), NullLogger<OrganizationWaiverService>.Instance),
        NullLogger<EventService>.Instance);

    private Event Game(DateTime start, int minutes = 60, int max = 10, decimal cost = 0)
    {
        var e = new Event
        {
            OrganizationId = _org.Id, CreatorId = _organizer.Id, Name = "Example game", EventDate = start, Duration = minutes,
            MaxPlayers = max, Cost = cost, Status = "Published", Visibility = "Public", IsRosterPublished = true,
        };
        _context.Events.Add(e);
        _context.SaveChanges();
        return e;
    }

    private Event Finished(decimal cost = 0) => Game(DateTime.UtcNow.AddHours(-3), 60, 10, cost);
    private Event Playing(decimal cost = 0) => Game(DateTime.UtcNow.AddMinutes(-30), 90, 10, cost);
    private Event Upcoming(decimal cost = 0) => Game(DateTime.UtcNow.AddDays(2), 60, 10, cost);

    private EventRegistration Register(Event evt, User user, string status, string? payment = null, DateTime? deadline = null)
    {
        var r = new EventRegistration
        {
            EventId = evt.Id, UserId = user.Id, Status = status, RegisteredPosition = "Skater",
            TeamAssignment = status == "Registered" ? "Black" : null, PaymentStatus = payment,
            WaitlistPosition = status == "Waitlisted" ? 1 : null, PaymentDeadlineAt = deadline,
            RegisteredAt = DateTime.UtcNow.AddHours(-5),
        };
        _context.EventRegistrations.Add(r);
        _context.SaveChanges();
        return r;
    }

    private static Mock<INotificationService> RecordingPush(List<string?> sent)
    {
        var push = new Mock<INotificationService>();
        push.Setup(p => p.SendPushNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<object?>(), It.IsAny<Guid?>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<Guid?>()))
            .Callback<string, string, string, object?, Guid?, string?, Guid?, Guid?>((_, _, _, _, _, t, _, _) => sent.Add(t))
            .Returns(Task.CompletedTask);
        return push;
    }

    private async Task<EventRegistration> Reload(EventRegistration r) =>
        await _context.EventRegistrations.AsNoTracking().SingleAsync(x => x.Id == r.Id);

    private async Task<string> Snapshot() => string.Join("|", (await _context.EventRegistrations.AsNoTracking()
            .OrderBy(r => r.Id).ToListAsync())
        .Select(r => $"{r.Id}:{r.Status}:{r.WaitlistPosition}:{r.TeamAssignment}:{r.PaymentStatus}:{r.PaymentDeadlineAt:O}:{r.PromotedAt:O}"));

    private EventsController Controller(INotificationService push, Guid caller) =>
        new(Events(push), Mock.Of<IWaitlistService>(), NullLogger<EventsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, caller.ToString()),
                        new Claim(ClaimTypes.Role, "Player"),
                    }, "Test")),
                },
            },
        };

    /// <summary>Records the level of every log line.</summary>
    private sealed class LevelLog<T> : ILogger<T>
    {
        public readonly List<LogLevel> Levels = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Levels.Add(logLevel);
    }

    // ------------------------------------------------- a player's own drop: refused

    [Theory]
    [InlineData("Registered", 0, null)]
    [InlineData("Registered", 20, "Pending")]
    [InlineData("Registered", 20, "MarkedPaid")]
    [InlineData("Registered", 20, "Verified")]
    [InlineData("Waitlisted", 0, null)]
    [InlineData("Waitlisted", 20, "Pending")]     // an unverified player who played sits here
    [InlineData("Waitlisted", 20, "Verified")]
    public async Task APlayerCannotDropFromAFinishedGameAndNothingIsWrittenOrSent(string status, int cost, string? payment)
    {
        var evt = Finished(cost);
        Register(evt, _player, status, payment);
        Register(evt, _other, "Waitlisted", "Verified");               // would have been promoted
        var push = new Mock<INotificationService>(MockBehavior.Strict); // no "Player Dropped", nothing
        var before = await Snapshot();

        await FluentActions.Awaiting(() => Events(push.Object).CancelRegistrationAsync(evt.Id, _player.Id))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage(EventService.GameOverMessage);

        (await Snapshot()).Should().Be(before);
        push.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TheDropRefusalIsA400WithTheMessageAndAMissingRegistrationIsStill404()
    {
        var evt = Finished();
        var mine = Register(evt, _player, "Registered");

        var refused = await Controller(new Mock<INotificationService>(MockBehavior.Strict).Object, _player.Id).CancelRegistration(evt.Id);
        var bad = refused.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value!.GetType().GetProperty("message")!.GetValue(bad.Value).Should().Be(EventService.GameOverMessage);
        (await Reload(mine)).Status.Should().Be("Registered");

        var missing = await Controller(Mock.Of<INotificationService>(), _other.Id).CancelRegistration(evt.Id);
        missing.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task OnlyTheGameOverRefusalBecomesA400EveryOtherExceptionSurfacesUnchanged()
    {
        var eventId = Guid.NewGuid();
        var service = new Mock<IEventService>();
        var controller = new EventsController(service.Object, Mock.Of<IWaitlistService>(), NullLogger<EventsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, _player.Id.ToString()),
                        new Claim(ClaimTypes.Role, "Player"),
                    }, "Test")),
                },
            },
        };

        // e.g. what the database provider raises for a transient failure: not a refusal
        foreach (var other in new Exception[]
                 {
                     new InvalidOperationException("An exception has been raised that is likely due to a transient failure."),
                     new InvalidOperationException(EventService.GameOverMessage + " "),
                     new TimeoutException("example"),
                 })
        {
            service.Setup(s => s.CancelRegistrationAsync(eventId, _player.Id)).ThrowsAsync(other);
            (await FluentActions.Awaiting(() => controller.CancelRegistration(eventId)).Should().ThrowAsync<Exception>())
                .Which.Should().BeSameAs(other);
        }

        service.Setup(s => s.CancelRegistrationAsync(eventId, _player.Id))
            .ThrowsAsync(new InvalidOperationException(EventService.GameOverMessage));
        var bad = (await controller.CancelRegistration(eventId)).Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value!.GetType().GetProperty("message")!.GetValue(bad.Value).Should().Be(EventService.GameOverMessage);
    }

    [Fact]
    public async Task AnAlreadyCancelledRegistrationOnAFinishedGameAnswersAsBefore()
    {
        var evt = Finished();
        var gone = Register(evt, _player, "Cancelled");
        var push = new Mock<INotificationService>(MockBehavior.Strict);

        (await Events(push.Object).CancelRegistrationAsync(evt.Id, _player.Id)).Should().BeTrue();
        (await Reload(gone)).Status.Should().Be("Cancelled");
        push.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task APlayerCanStillDropFromAGameBeingPlayedOrStillToCome()
    {
        foreach (var make in new Func<Event>[] { () => Playing(), () => Upcoming() })
        {
            foreach (var status in new[] { "Registered", "Waitlisted" })
            {
                var evt = make();
                var mine = Register(evt, _player, status);
                var sent = new List<string?>();

                (await Events(RecordingPush(sent).Object).CancelRegistrationAsync(evt.Id, _player.Id)).Should().BeTrue();

                (await Reload(mine)).Status.Should().Be("Cancelled");
                if (status == "Registered") sent.Should().Contain("player_dropped");
                (await Controller(Mock.Of<INotificationService>(), _player.Id).CancelRegistration(evt.Id))
                    .Should().BeOfType<NoContentResult>();     // a second cancel answers as before
            }
        }
    }

    // ------------------------------------------- the expired-payment job: left alone

    [Fact]
    public async Task TheExpiredPaymentJobLeavesAFinishedGameExactlyAsItIsAndStaysQuiet()
    {
        var evt = Finished(cost: 20);
        var unpaid = Register(evt, _player, "Registered", "Pending", deadline: DateTime.UtcNow.AddMinutes(-90));
        var unmarked = Register(evt, _other, "Registered", null, deadline: DateTime.UtcNow.AddMinutes(-90));
        var push = new Mock<INotificationService>(MockBehavior.Strict);   // no "Registration Expired"
        var log = new LevelLog<WaitlistService>();
        var before = await Snapshot();

        for (var pass = 0; pass < 3; pass++)
            await Waitlist(push.Object, log).ProcessExpiredPaymentDeadlinesAsync();

        (await Snapshot()).Should().Be(before);
        push.VerifyNoOtherCalls();
        log.Levels.Should().OnlyContain(l => l <= LogLevel.Debug, "a skipped registration is not logged loudly on every pass");

        // ...and the organizer can still verify the payment or remove the player
        var events = Events(Mock.Of<INotificationService>());
        (await events.UpdatePaymentStatusAsync(evt.Id, unpaid.Id, "Verified", _organizer.Id)).Success.Should().BeTrue();
        (await events.RemoveRegistrationAsync(evt.Id, unmarked.Id, _organizer.Id)).Should().BeTrue();
        (await Reload(unpaid)).PaymentStatus.Should().Be("Verified");
        (await Reload(unmarked)).Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task TheExpiredPaymentJobStillCancelsOnAGameBeingPlayedOrStillToCome()
    {
        foreach (var evt in new[] { Playing(20), Upcoming(20) })
        {
            var unpaid = Register(evt, _player, "Registered", "Pending", deadline: DateTime.UtcNow.AddMinutes(-5));
            var sent = new List<string?>();

            await Waitlist(RecordingPush(sent).Object).ProcessExpiredPaymentDeadlinesAsync();

            (await Reload(unpaid)).Status.Should().Be("Cancelled");
            (await Reload(unpaid)).PaymentDeadlineAt.Should().BeNull();
            sent.Should().Contain("registration_expired");
        }
    }

    [Fact]
    public async Task OneJobPassHandlesAFinishedAndAnOpenGameEachByItsOwnRule()
    {
        var over = Finished(cost: 20);
        var open = Upcoming(cost: 20);
        var kept = Register(over, _player, "Registered", "Pending", deadline: DateTime.UtcNow.AddMinutes(-5));
        var expired = Register(open, _player, "Registered", "Pending", deadline: DateTime.UtcNow.AddMinutes(-5));
        var sent = new List<string?>();

        await Waitlist(RecordingPush(sent).Object).ProcessExpiredPaymentDeadlinesAsync();

        (await Reload(kept)).Status.Should().Be("Registered");
        (await Reload(kept)).PaymentDeadlineAt.Should().NotBeNull();
        (await Reload(expired)).Status.Should().Be("Cancelled");
        sent.Should().Equal("registration_expired");
    }

    // ------------------------------------------ unchanged on purpose, on a finished game

    [Fact]
    public async Task AnOrganizerStillRemovesAPlayerFromAFinishedGame()
    {
        var evt = Finished(cost: 20);
        var gone = Register(evt, _player, "Waitlisted", "Pending");
        var sent = new List<string?>();

        (await Events(RecordingPush(sent).Object).RemoveRegistrationAsync(evt.Id, gone.Id, _organizer.Id)).Should().BeTrue();

        (await Reload(gone)).Status.Should().Be("Cancelled");
        sent.Should().Equal("removed_from_event");
    }

    [Fact]
    public async Task AnOrganizerStillVerifiesAndResetsAPaymentAndAPlayerStillMarksOneOnAFinishedGame()
    {
        var evt = Finished(cost: 20);
        var mine = Register(evt, _player, "Registered", "Pending");
        var events = Events(Mock.Of<INotificationService>());

        (await events.MarkPaymentAsync(evt.Id, _player.Id, null)).Should().BeTrue();
        (await events.UpdatePaymentStatusAsync(evt.Id, mine.Id, "Verified", _organizer.Id)).Success.Should().BeTrue();
        (await events.UpdatePaymentStatusAsync(evt.Id, mine.Id, "Pending", _organizer.Id)).Success.Should().BeTrue();
        (await Reload(mine)).PaymentStatus.Should().Be("Pending");
    }

    [Fact]
    public async Task LeavingAnOrganizationStillOnlyTouchesGamesThatHaveNotStarted()
    {
        var over = Finished();
        var playing = Playing();
        var upcoming = Upcoming();
        var onOver = Register(over, _player, "Registered");
        var onPlaying = Register(playing, _player, "Registered");
        var onUpcoming = Register(upcoming, _player, "Waitlisted");
        var events = Events(Mock.Of<INotificationService>());
        var orgs = new OrganizationService(_context, new OrganizationAdminService(_context),
            new OrganizationWaiverService(_context, new OrganizationAdminService(_context), NullLogger<OrganizationWaiverService>.Instance),
            events, Mock.Of<IOrganizationJoinRequestService>(), NullLogger<OrganizationService>.Instance);

        (await orgs.LeaveAsync(_org.Id, _player.Id)).Should().BeTrue();

        (await Reload(onOver)).Status.Should().Be("Registered");
        (await Reload(onPlaying)).Status.Should().Be("Registered");
        (await Reload(onUpcoming)).Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task AGuestIsStillAddedToAFinishedGame()
    {
        var evt = Finished();
        var guest = await Events(Mock.Of<INotificationService>())
            .CreateGhostPlayerAsync(evt.Id, _organizer.Id, "Greta", "Guest", "Skater", "Bronze");
        guest.Status.Should().Be("Registered");
    }
}
