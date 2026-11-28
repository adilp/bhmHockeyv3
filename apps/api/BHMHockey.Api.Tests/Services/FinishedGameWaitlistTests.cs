using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text.RegularExpressions;
using BHMHockey.Api.Controllers;
using BHMHockey.Api.Data;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// Nobody comes off the waitlist onto a game that is over. The organizer's move to
/// roster is refused (EventService.HasEnded, EventService.GameOverMessage), and the
/// automatic promotion (WaitlistService.PromoteFromWaitlistAsync and
/// PromoteNextFromWaitlistAsync) promotes and notifies no one, while the action that
/// freed the spot (a cancel, a removal, an expired payment deadline) still happens.
/// Left on purpose: an organizer verifying a waitlisted player's payment still puts
/// THAT player on the roster (pinned below). All data is invented; dates are relative.
/// </summary>
public class FinishedGameWaitlistTests : IDisposable
{
    /// <summary>Every notification type a promotion off the waitlist sends.</summary>
    private static readonly string[] PromotionTypes =
        { "auto_promoted", "auto_promotion", "promoted_from_waitlist", "moved_to_roster", "spot_available" };

    private readonly AppDbContext _context;
    private readonly Organization _org;
    private readonly User _organizer, _leaver, _waiting, _second;

    public FinishedGameWaitlistTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new AppDbContext(options);

        _organizer = AddUser("Odin", "Organizer", "ExponentPushToken[o]");
        _leaver = AddUser("Lena", "Leaver", "ExponentPushToken[l]");
        _waiting = AddUser("Wes", "Waiting", "ExponentPushToken[w]");
        _second = AddUser("Sal", "Second", "ExponentPushToken[s]");
        _org = new Organization { Name = "Example League", CreatorId = _organizer.Id };
        _context.Organizations.Add(_org);
        _context.OrganizationAdmins.Add(new OrganizationAdmin { OrganizationId = _org.Id, UserId = _organizer.Id });
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

    private WaitlistService Waitlist(INotificationService push) => new(_context, push, NullLogger<WaitlistService>.Instance);

    private EventService Events(INotificationService push) => new(_context, push, new OrganizationAdminService(_context),
        Waitlist(push),
        new OrganizationWaiverService(_context, new OrganizationAdminService(_context), NullLogger<OrganizationWaiverService>.Instance),
        NullLogger<EventService>.Instance);

    private Event Game(DateTime start, int minutes = 60, int max = 1, decimal cost = 0)
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

    private Event Finished(decimal cost = 0) => Game(DateTime.UtcNow.AddHours(-3), 60, 1, cost);
    private Event Playing(decimal cost = 0) => Game(DateTime.UtcNow.AddMinutes(-30), 90, 1, cost);
    private Event Upcoming(decimal cost = 0) => Game(DateTime.UtcNow.AddDays(2), 60, 1, cost);

    private EventRegistration Register(Event evt, User user, string status, string? payment = null,
        int? position = null, DateTime? deadline = null, int minutesAgo = 60)
    {
        var r = new EventRegistration
        {
            EventId = evt.Id, UserId = user.Id, Status = status, RegisteredPosition = "Skater",
            TeamAssignment = status == "Registered" ? "Black" : null, PaymentStatus = payment,
            WaitlistPosition = status == "Waitlisted" ? position ?? 1 : null, PaymentDeadlineAt = deadline,
            RegisteredAt = DateTime.UtcNow.AddMinutes(-minutesAgo),
        };
        _context.EventRegistrations.Add(r);
        _context.SaveChanges();
        return r;
    }

    /// <summary>
    /// A strict push mock: any promotion notification throws (MockException), every other
    /// notification is accepted and its type recorded.
    /// </summary>
    private static Mock<INotificationService> NoPromotionPush(List<string?> sent)
    {
        var push = new Mock<INotificationService>(MockBehavior.Strict);
        push.Setup(p => p.SendPushNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<object?>(), It.IsAny<Guid?>(), It.Is<string?>(t => !PromotionTypes.Contains(t)),
                It.IsAny<Guid?>(), It.IsAny<Guid?>()))
            .Callback<string, string, string, object?, Guid?, string?, Guid?, Guid?>((_, _, _, _, _, t, _, _) => sent.Add(t))
            .Returns(Task.CompletedTask);
        return push;
    }

    /// <summary>A loose push mock that records every notification type.</summary>
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

    private async Task<string> RegistrationSnapshot() => string.Join("|", (await _context.EventRegistrations.AsNoTracking()
            .OrderBy(r => r.Id).ToListAsync())
        .Select(r => $"{r.Id}:{r.Status}:{r.WaitlistPosition}:{r.TeamAssignment}:{r.PaymentStatus}:{r.PromotedAt:O}:{r.PaymentDeadlineAt:O}"));

    // ------------------------------------------------- the organizer's move to roster

    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    public async Task MoveToRosterOnAFinishedGameIsRefusedWritesNothingAndSendsNothing(int cost)
    {
        var evt = Game(DateTime.UtcNow.AddHours(-3), 60, max: 10, cost);   // room on the roster: only the rule refuses
        var waiting = Register(evt, _waiting, "Waitlisted", cost > 0 ? "Verified" : null);
        var push = new Mock<INotificationService>(MockBehavior.Strict);
        var before = await RegistrationSnapshot();

        var res = await Events(push.Object).MoveToRosterAsync(evt.Id, waiting.Id, _organizer.Id);

        res.Success.Should().BeFalse();
        res.Message.Should().Be(EventService.GameOverMessage);
        res.Registration.Should().BeNull();
        (await RegistrationSnapshot()).Should().Be(before);
        push.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MoveToRosterRefusalIsThe400WithAMessageTheInstalledAppShows()
    {
        var evt = Game(DateTime.UtcNow.AddHours(-3), 60, max: 10);
        var waiting = Register(evt, _waiting, "Waitlisted");
        var controller = new EventsController(Events(new Mock<INotificationService>(MockBehavior.Strict).Object),
            Mock.Of<IWaitlistService>(), NullLogger<EventsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, _organizer.Id.ToString()),
                        new Claim(ClaimTypes.Role, "Organizer"),
                    }, "Test")),
                },
            },
        };

        var result = await controller.MoveToRoster(evt.Id, waiting.Id);

        var bad = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value!.GetType().GetProperty("message")!.GetValue(bad.Value).Should().Be(EventService.GameOverMessage);
        (await Reload(waiting)).Status.Should().Be("Waitlisted");
    }

    [Fact]
    public async Task MoveToRosterBySomeoneWhoCannotManageTheGameIsStillForbidden()
    {
        var evt = Finished();
        var waiting = Register(evt, _waiting, "Waitlisted");
        await FluentActions.Awaiting(() => Events(Mock.Of<INotificationService>()).MoveToRosterAsync(evt.Id, waiting.Id, _leaver.Id))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task MoveToRosterOnAGameBeingPlayedOrStillToComeWorksAsBefore()
    {
        foreach (var evt in new[] { Game(DateTime.UtcNow.AddMinutes(-30), 90, 10), Game(DateTime.UtcNow.AddDays(2), 60, 10) })
        {
            var waiting = Register(evt, _waiting, "Waitlisted");
            var sent = new List<string?>();
            var res = await Events(RecordingPush(sent).Object).MoveToRosterAsync(evt.Id, waiting.Id, _organizer.Id);
            res.Success.Should().BeTrue();
            (await Reload(waiting)).Status.Should().Be("Registered");
            sent.Should().Contain("moved_to_roster");
        }
    }

    // ------------------------------------------- automatic promotion: the one function

    [Fact]
    public async Task PromoteFromWaitlistOnAFinishedGamePromotesAndNotifiesNoOne()
    {
        var evt = Finished(cost: 20);
        var waiting = Register(evt, _waiting, "Waitlisted", "Verified");
        var push = new Mock<INotificationService>(MockBehavior.Strict);
        var before = await RegistrationSnapshot();

        foreach (var owns in new[] { false, true })
        {
            var res = await Waitlist(push.Object).PromoteFromWaitlistAsync(evt.Id, spotCount: 3, callerOwnsTransaction: owns);
            res.Promoted.Should().BeEmpty();
            res.PendingNotifications.Should().BeEmpty();
        }
        (await Waitlist(push.Object).PromoteNextFromWaitlistAsync(evt.Id)).Should().BeNull();

        (await RegistrationSnapshot()).Should().Be(before);
        (await Reload(waiting)).Status.Should().Be("Waitlisted");
        push.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PromoteFromWaitlistOnAGameBeingPlayedOrStillToComePromotesAsBefore()
    {
        foreach (var evt in new[] { Playing(20), Upcoming(20) })
        {
            var verified = Register(evt, _waiting, "Waitlisted", "Verified");
            var next = Register(evt, _second, "Waitlisted", "Pending", position: 2);
            var sent = new List<string?>();

            var res = await Waitlist(RecordingPush(sent).Object).PromoteFromWaitlistAsync(evt.Id, 1);
            res.Promoted.Should().ContainSingle(r => r.Id == verified.Id);
            sent.Should().Contain("auto_promoted");

            (await Waitlist(RecordingPush(sent).Object).PromoteNextFromWaitlistAsync(evt.Id))!.Id.Should().Be(next.Id);
            (await Reload(next)).Status.Should().Be("Registered");
        }
    }

    // ------------------------------------------------------- every caller, end to end

    [Fact]
    public async Task APlayerCancellingOnAFinishedGameIsRefusedAndNobodyIsPromoted()
    {
        // Since the owner's later rule a player cannot drop from a finished game at all
        // (FinishedGameDropTests); the waitlist is untouched either way.
        var evt = Finished(cost: 20);
        var leaver = Register(evt, _leaver, "Registered", "Verified");
        var waiting = Register(evt, _waiting, "Waitlisted", "Verified");
        var push = new Mock<INotificationService>(MockBehavior.Strict);

        await FluentActions.Awaiting(() => Events(push.Object).CancelRegistrationAsync(evt.Id, _leaver.Id))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage(EventService.GameOverMessage);

        (await Reload(leaver)).Status.Should().Be("Registered");
        (await Reload(waiting)).Status.Should().Be("Waitlisted");
        (await Reload(waiting)).WaitlistPosition.Should().Be(1);
        push.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AnOrganizerRemovingAPlayerOnAFinishedGameStillRemovesAndNobodyIsPromoted()
    {
        var evt = Finished(cost: 20);
        var gone = Register(evt, _leaver, "Registered", "Verified");
        var waiting = Register(evt, _waiting, "Waitlisted", "Verified");
        var sent = new List<string?>();

        (await Events(NoPromotionPush(sent).Object).RemoveRegistrationAsync(evt.Id, gone.Id, _organizer.Id)).Should().BeTrue();

        (await Reload(gone)).Status.Should().Be("Cancelled");
        (await Reload(waiting)).Status.Should().Be("Waitlisted");
        sent.Should().Equal("removed_from_event");        // the removed player's notice, as today
    }

    [Fact]
    public async Task AnExpiredPaymentDeadlineOnAFinishedGameCancelsNobodyAndPromotesNobody()
    {
        // Since the owner's later rule the job leaves a finished game alone (FinishedGameDropTests)
        var evt = Finished(cost: 20);
        var unpaid = Register(evt, _leaver, "Registered", "Pending", deadline: DateTime.UtcNow.AddMinutes(-5));
        var waiting = Register(evt, _waiting, "Waitlisted", "Verified");
        var push = new Mock<INotificationService>(MockBehavior.Strict);

        await Waitlist(push.Object).ProcessExpiredPaymentDeadlinesAsync();

        (await Reload(unpaid)).Status.Should().Be("Registered");
        (await Reload(waiting)).Status.Should().Be("Waitlisted");
        push.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TheSameCallersOnAGameBeingPlayedOrStillToComeStillPromote()
    {
        foreach (var make in new Func<Event>[] { () => Playing(20), () => Upcoming(20) })
        {
            // a player's cancel
            var a = make();
            Register(a, _leaver, "Registered", "Verified");
            var wa = Register(a, _waiting, "Waitlisted", "Verified");
            var sent = new List<string?>();
            await Events(RecordingPush(sent).Object).CancelRegistrationAsync(a.Id, _leaver.Id);
            (await Reload(wa)).Status.Should().Be("Registered");
            sent.Should().Contain(new[] { "auto_promoted", "auto_promotion" });

            // an organizer's removal
            var b = make();
            var gone = Register(b, _leaver, "Registered", "Verified");
            var wb = Register(b, _waiting, "Waitlisted", "Verified");
            sent.Clear();
            await Events(RecordingPush(sent).Object).RemoveRegistrationAsync(b.Id, gone.Id, _organizer.Id);
            (await Reload(wb)).Status.Should().Be("Registered");
            sent.Should().Contain("auto_promoted");

            // an expired payment deadline
            var c = make();
            Register(c, _leaver, "Registered", "Pending", deadline: DateTime.UtcNow.AddMinutes(-5));
            var wc = Register(c, _waiting, "Waitlisted", "Verified");
            sent.Clear();
            await Waitlist(RecordingPush(sent).Object).ProcessExpiredPaymentDeadlinesAsync();
            (await Reload(wc)).Status.Should().Be("Registered");
            sent.Should().Contain(new[] { "registration_expired", "auto_promoted" });
        }
    }

    // ---------------------------------------------- left on purpose: verifying payment

    [Fact]
    public async Task VerifyingAWaitlistedPlayersPaymentOnAFinishedGameStillPutsThatPlayerAndOnlyThatPlayerOnTheRoster()
    {
        var evt = Game(DateTime.UtcNow.AddHours(-3), 60, max: 10, cost: 20);
        var paid = Register(evt, _waiting, "Waitlisted", "MarkedPaid", position: 1);
        var alreadyVerified = Register(evt, _second, "Waitlisted", "Verified", position: 2);
        var sent = new List<string?>();

        var res = await Events(RecordingPush(sent).Object).UpdatePaymentStatusAsync(evt.Id, paid.Id, "Verified", _organizer.Id);

        res.Success.Should().BeTrue();
        res.Promoted.Should().BeTrue();
        (await Reload(paid)).Status.Should().Be("Registered");
        (await Reload(alreadyVerified)).Status.Should().Be("Waitlisted");   // nobody else moves
        (await Reload(alreadyVerified)).WaitlistPosition.Should().Be(1);     // only renumbered
        sent.Should().Equal("promoted_from_waitlist");
    }

    // ------------------------------------------------- the enumeration: no new path

    private static string ServicesDir([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "BHMHockey.Api", "Services"));

    private static readonly Regex Signature = new(@"^\s*(public|private|protected|internal)\b[^=;]*?\b(\w+)\s*(<[^>]*>)?\s*\(", RegexOptions.Compiled);

    /// <summary>Each (file, enclosing method) where a line matches, in source order.</summary>
    private static List<string> SitesOf(Regex pattern)
    {
        var sites = new List<string>();
        foreach (var path in Directory.GetFiles(ServicesDir(), "*.cs", SearchOption.AllDirectories).OrderBy(p => p))
        {
            var file = Path.GetFileNameWithoutExtension(path);
            if (file.Contains("Tournament")) continue;            // tournaments have their own registrations
            if (Regex.IsMatch(file, "^I[A-Z]")) continue;         // interfaces declare, they do not call
            string? method = null;
            foreach (var line in File.ReadLines(path))
            {
                var sig = Signature.Match(line);
                if (sig.Success) method = sig.Groups[2].Value;
                if (pattern.IsMatch(line) && !Signature.IsMatch(line)) sites.Add($"{file}.{method}");
            }
        }
        return sites.Distinct().ToList();
    }

    [Fact]
    public void EveryAutomaticPromotionGoesThroughTheGuardedFunctions()
    {
        // Callers of the two promotion functions: each has an end-to-end test above.
        SitesOf(new Regex(@"\bPromote(From|Next(From)?)WaitlistAsync\s*\(")).Should().BeEquivalentTo(
            "EventService.CancelRegistrationAsync",            // a player's cancel
            "EventService.RemoveRegistrationAsync",            // an organizer's removal
            "WaitlistService.ProcessExpiredPaymentDeadlinesAsync"); // the background job

        var waitlist = File.ReadAllText(Path.Combine(ServicesDir(), "WaitlistService.cs"));
        foreach (var name in new[] { "PromoteFromWaitlistAsync", "PromoteNextFromWaitlistAsync" })
        {
            var start = Regex.Match(waitlist, $@"public async Task<[^>]+>\??\s+{name}\(").Index;
            start.Should().BeGreaterThan(0);
            var body = waitlist[start..waitlist.IndexOf("\n    }\n", start, StringComparison.Ordinal)];
            body.Should().Contain("EventService.HasEnded(", $"{name} must refuse a game that is over");
        }
    }

    [Fact]
    public void EveryPlaceThatPutsARegistrationOnTheRosterIsKnown()
    {
        // A new place that sets Status = "Registered" must be added here with its reason,
        // and, if it moves someone off the waitlist, refuse a game that is over.
        SitesOf(new Regex(@"\bStatus\s*=\s*""Registered""")).Should().BeEquivalentTo(
            "EventService.ApplyAutoRosterAsync",          // new registrations when a game is created
            "EventService.RegisterAsync",                 // a player's own sign-up: refused once over
            "EventService.UpdatePaymentStatusAsync",      // the verified player himself: kept on purpose
            "EventService.MoveToRosterAsync",             // the organizer's move: refused once over
            "EventService.AddUserToWaitlistAsync",        // the organizer's add player: refused once over
            "EventService.CreateGhostPlayerAsync",        // guests: unchanged, the owner's call
            "WaitlistService.PromoteNextFromWaitlistAsync", // guarded
            "WaitlistService.PromoteFromWaitlistAsync",   // guarded
            "PastGameService.CreatePastGameAsync");       // creates a finished game's roster
    }

    [Fact]
    public void EveryPlaceThatCancelsARegistrationIsKnown()
    {
        // A new place that sets Status = "Cancelled" must be added here with its reason and
        // with what it does on a game that is over (FinishedGameDropTests).
        SitesOf(new Regex(@"\bStatus\s*=\s*""Cancelled""")).Should().BeEquivalentTo(
            "EventService.DeleteAsync",                   // the EVENT's status (cancel a game); its registrations are untouched
            "EventService.CancelRegistrationAsync",       // a player's own drop: refused once over
            "EventService.RemoveRegistrationAsync",       // an organizer's removal: allowed on a finished game, on purpose
            "WaitlistService.ProcessExpiredPaymentDeadlinesAsync"); // unpaid deadline: skips a game that is over
    }
}
