using BHMHockey.Api.Controllers;
using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// An organizer's "add player" (POST /events/{id}/registrations/add-user) on a game
/// that is over is refused with the same rule (EventService.HasEnded) and the same
/// message as a player's own sign-up. The refusal is narrow on purpose: the tests
/// after the first group pin everything that must keep working on a finished game.
/// All data here is invented; every date is relative to now.
/// </summary>
public class FinishedGameAddPlayerTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Organization _org;
    private readonly User _organizer, _player, _newcomer;

    public FinishedGameAddPlayerTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new AppDbContext(options);

        _organizer = AddUser("Orla", "Organizer");
        _player = AddUser("Pia", "Player", pushToken: "ExponentPushToken[p]");
        _newcomer = AddUser("Nico", "Newcomer", pushToken: "ExponentPushToken[n]");
        _org = new Organization { Name = "Example League", CreatorId = _organizer.Id };
        _context.Organizations.Add(_org);
        _context.OrganizationAdmins.Add(new OrganizationAdmin { OrganizationId = _org.Id, UserId = _organizer.Id });
        _context.SaveChanges();
    }

    public void Dispose() => _context.Dispose();

    // ----------------------------------------------------------------- helpers

    private User AddUser(string first, string last, string? pushToken = null)
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

    private EventService Events(INotificationService push) => new(_context, push, new OrganizationAdminService(_context),
        new WaitlistService(_context, push, NullLogger<WaitlistService>.Instance),
        new OrganizationWaiverService(_context, new OrganizationAdminService(_context), NullLogger<OrganizationWaiverService>.Instance),
        NullLogger<EventService>.Instance);

    private Event Game(DateTime start, int minutes = 60, int max = 10, decimal cost = 0, bool rosterPublished = true)
    {
        var e = new Event
        {
            OrganizationId = _org.Id, CreatorId = _organizer.Id, Name = "Example game", EventDate = start, Duration = minutes,
            MaxPlayers = max, Cost = cost, Status = "Published", Visibility = "Public", IsRosterPublished = rosterPublished,
        };
        _context.Events.Add(e);
        _context.SaveChanges();
        return e;
    }

    /// <summary>A game that started three hours ago and lasted an hour.</summary>
    private Event FinishedGame(int max = 10, decimal cost = 0) => Game(DateTime.UtcNow.AddHours(-3), 60, max, cost);

    private EventRegistration Register(Event evt, User user, string status = "Registered", string? payment = null)
    {
        var r = new EventRegistration
        {
            EventId = evt.Id, UserId = user.Id, Status = status, RegisteredPosition = "Skater",
            TeamAssignment = status == "Registered" ? "Black" : null, PaymentStatus = payment,
            WaitlistPosition = status == "Waitlisted" ? 1 : null,
        };
        _context.EventRegistrations.Add(r);
        _context.SaveChanges();
        return r;
    }

    private async Task<string> RegistrationSnapshot() => string.Join("|", (await _context.EventRegistrations.AsNoTracking()
            .OrderBy(r => r.Id).ToListAsync())
        .Select(r => $"{r.Id}:{r.EventId}:{r.UserId}:{r.Status}:{r.WaitlistPosition}:{r.TeamAssignment}:{r.PaymentStatus}:{r.RegisteredAt:O}"));

    // ---------------------------------------------------------- the refusal itself

    [Theory]
    [InlineData(10, 0)]     // free game, room on the roster
    [InlineData(0, 0)]      // free game, full: would have been a waitlist add
    [InlineData(10, 20)]    // paid game: would have opened a payment
    [InlineData(0, 20)]
    public async Task AddPlayerOnAFinishedGameIsRefusedWritesNothingAndSendsNothing(int max, int cost)
    {
        var evt = FinishedGame(max, cost);
        Register(evt, _player);
        var push = new Mock<INotificationService>(MockBehavior.Strict);
        var before = await RegistrationSnapshot();

        foreach (var position in new[] { "Skater", "Goalie", null })
            await FluentActions.Awaiting(() => Events(push.Object).AddUserToWaitlistAsync(evt.Id, _newcomer.Id, _organizer.Id, position))
                .Should().ThrowAsync<InvalidOperationException>().WithMessage(EventService.GameOverMessage);

        (await RegistrationSnapshot()).Should().Be(before);
        push.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TheMessageIsTheOneAPlayersOwnSignUpGets()
    {
        EventService.GameOverMessage.Should().Be("This game has already been played");
        var evt = FinishedGame();
        await FluentActions.Awaiting(() => Events(Mock.Of<INotificationService>()).RegisterAsync(evt.Id, _newcomer.Id, "Skater"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage(EventService.GameOverMessage);
    }

    [Fact]
    public async Task ACancelledRegistrationOnAFinishedGameIsNotReactivated()
    {
        var evt = FinishedGame();
        var cancelled = Register(evt, _newcomer, status: "Cancelled");
        var before = await RegistrationSnapshot();

        await FluentActions.Awaiting(() => Events(Mock.Of<INotificationService>(MockBehavior.Strict))
                .AddUserToWaitlistAsync(evt.Id, _newcomer.Id, _organizer.Id, "Skater"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage(EventService.GameOverMessage);

        (await RegistrationSnapshot()).Should().Be(before);
        (await _context.EventRegistrations.AsNoTracking().SingleAsync(r => r.Id == cancelled.Id)).Status.Should().Be("Cancelled");
    }

    [Fact]
    public async Task SomeoneWhoCannotManageTheGameIsStillToldNoPermissionNotThatItIsOver()
    {
        var evt = FinishedGame();
        await FluentActions.Awaiting(() => Events(Mock.Of<INotificationService>())
                .AddUserToWaitlistAsync(evt.Id, _newcomer.Id, _player.Id, "Skater"))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task AGameBeingPlayedOrStillToComeTakesAnAddedPlayerAsBefore()
    {
        var playing = Game(DateTime.UtcNow.AddMinutes(-30), 90);   // started, not over
        var upcoming = Game(DateTime.UtcNow.AddDays(2));
        var events = Events(Mock.Of<INotificationService>());

        (await events.AddUserToWaitlistAsync(playing.Id, _newcomer.Id, _organizer.Id, "Skater")).Status.Should().Be("Registered");
        (await events.AddUserToWaitlistAsync(upcoming.Id, _newcomer.Id, _organizer.Id, "Skater")).Status.Should().Be("Registered");
    }

    [Fact]
    public async Task TheRouteAnswersTheSame400WithAMessageTheInstalledAppShows()
    {
        var evt = FinishedGame();
        var controller = new EventsController(Events(Mock.Of<INotificationService>()),
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

        var result = await controller.AddUserToEvent(evt.Id, new AddUserToEventRequest(_newcomer.Id, "Skater"));

        var bad = result.Result.Should().BeOfType<BadRequestObjectResult>().Subject;
        bad.Value!.GetType().GetProperty("message")!.GetValue(bad.Value).Should().Be(EventService.GameOverMessage);
    }

    // ------------------------------------------ what keeps working on a finished game

    [Fact]
    public async Task ThePastGameRouteStillCreatesAFinishedGameWithItsPlayersAndCorrectsItsPayment()
    {
        var pastGames = new PastGameService(_context, new OrganizationAdminService(_context), NullLogger<PastGameService>.Instance);
        var played = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(-6), DateTimeKind.Utc);
        CreatePastGameRequest Request(string payment, bool correct) => new("Example game", null, played, 60, null, null,
            "example_key", new List<PastGameRosterEntry>
            {
                new(_player.Id, "White", "Skater") { PaymentStatus = payment },
                new(_newcomer.Id, "Black", "Goalie") { PaymentStatus = payment },
            }) { Cost = 20m, CorrectPayment = correct };

        var created = await pastGames.CreatePastGameAsync(_org.Id, Request("Pending", false), _organizer.Id);
        created!.Outcome.Should().Be("created");
        created.RegisteredCount.Should().Be(2);

        var corrected = await pastGames.CreatePastGameAsync(_org.Id, Request("Verified", true), _organizer.Id);
        corrected!.Outcome.Should().Be("payment_corrected");
        (await _context.EventRegistrations.AsNoTracking().Where(r => r.EventId == created.EventId).ToListAsync())
            .Should().HaveCount(2).And.OnlyContain(r => r.PaymentStatus == "Verified" && r.Status == "Registered");

        // ...while an organizer's add-player on that same game is refused
        var third = AddUser("Tess", "Third");
        await FluentActions.Awaiting(() => Events(Mock.Of<INotificationService>())
                .AddUserToWaitlistAsync(created.EventId, third.Id, _organizer.Id, "Skater"))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage(EventService.GameOverMessage);
        (await _context.EventRegistrations.CountAsync(r => r.EventId == created.EventId)).Should().Be(2);
    }

    [Fact]
    public async Task AResultIsStillPublishedReadAndWithdrawnOnAFinishedGame()
    {
        var evt = FinishedGame();
        var visibility = new Mock<IEventVisibility>();
        visibility.Setup(v => v.CanSeeAsync(evt.Id, It.IsAny<Guid?>())).ReturnsAsync(true);
        var results = new GameResultService(_context, new OrganizationAdminService(_context), visibility.Object,
            Options.Create(new GameResultOptions()), NullLogger<GameResultService>.Instance);
        var payload = new GameResultUpsertRequest(GameResultSettings.SchemaVersion,
            new GameResultSourceDto("example_system", "example_key", 1, DateTime.UtcNow, null), evt.Id,
            new GameResultTeamsDto(new GameResultSideDto("Dark", 0), new GameResultSideDto("Light", 0)),
            "person", 3, 900, null, new List<GameGoalUpsertDto>(), new List<GameStarUpsertDto>(),
            new List<GamePlayerLineUpsertDto>(), null, null, null);

        (await results.PutResultAsync(evt.Id, payload, _organizer.Id))!.Outcome.Should().Be("created");
        (await results.GetResultAsync(evt.Id, _organizer.Id)).Should().NotBeNull();
        (await results.WithdrawResultAsync(evt.Id, new WithdrawGameResultRequest("example reason", 2), _organizer.Id))!
            .Outcome.Should().Be("withdrawn");
    }

    [Fact]
    public async Task PaymentsAreStillMarkedAndVerifiedOnAFinishedGame()
    {
        var evt = FinishedGame(cost: 20);
        var mine = Register(evt, _player, payment: "Pending");
        var theirs = Register(evt, _newcomer, payment: "Pending");
        var events = Events(Mock.Of<INotificationService>());

        (await events.MarkPaymentAsync(evt.Id, _player.Id, null)).Should().BeTrue();
        (await events.UpdatePaymentStatusAsync(evt.Id, mine.Id, "Verified", _organizer.Id)).Success.Should().BeTrue();
        (await events.UpdatePaymentStatusAsync(evt.Id, theirs.Id, "Verified", _organizer.Id)).Success.Should().BeTrue();

        (await _context.EventRegistrations.AsNoTracking().Where(r => r.EventId == evt.Id).ToListAsync())
            .Should().OnlyContain(r => r.PaymentStatus == "Verified");
    }

    [Fact]
    public async Task APlayerIsStillRemovedAndTeamsStillSwappedOnAFinishedGame()
    {
        var evt = FinishedGame();
        var stays = Register(evt, _player);
        var goes = Register(evt, _newcomer);
        var events = Events(Mock.Of<INotificationService>());

        (await events.UpdateTeamAssignmentAsync(evt.Id, stays.Id, "White", _organizer.Id)).Should().BeTrue();
        (await events.RemoveRegistrationAsync(evt.Id, goes.Id, _organizer.Id)).Should().BeTrue();

        (await _context.EventRegistrations.AsNoTracking().SingleAsync(r => r.Id == goes.Id)).Status.Should().Be("Cancelled");
        (await _context.EventRegistrations.AsNoTracking().SingleAsync(r => r.Id == stays.Id)).TeamAssignment.Should().Be("White");
    }

    [Fact]
    public async Task EverythingReadOnlyStillAnswersOnAFinishedGame()
    {
        var evt = FinishedGame();
        Register(evt, _player);
        var events = Events(Mock.Of<INotificationService>(MockBehavior.Strict));

        (await events.GetByIdAsync(evt.Id, _organizer.Id)).Should().NotBeNull();
        (await events.GetRegistrationsAsync(evt.Id)).Should().ContainSingle(r => r.User.Id == _player.Id);
        (await events.GetWaitlistWithBadgesAsync(evt.Id)).Should().BeEmpty();
        (await events.SearchUsersForEventAsync(evt.Id, _organizer.Id, "Nico")).Should().ContainSingle(u => u.Id == _newcomer.Id);
        (await events.GetByOrganizationAsync(_org.Id, _organizer.Id)).Should().Contain(e => e.Id == evt.Id);
    }
}
