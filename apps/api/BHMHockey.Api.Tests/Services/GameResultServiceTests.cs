using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Models.Exceptions;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// GameResultService: the write rules (admin gate, played game, validation,
/// revision idempotency, name-only lines) and the reads (recap, player stats,
/// leaderboard, recent results, the hide switch, the off switch).
/// </summary>
public class GameResultServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly Mock<IEventVisibility> _events = new();
    private readonly GameResultOptions _options = new();
    private readonly GameResultService _sut;

    private readonly Organization _org;
    private readonly User _admin, _alice, _bob, _ghost, _outsider, _goalie;
    private readonly Event _game;

    public GameResultServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _context = new AppDbContext(options);
        var admin = new OrganizationAdminService(_context);
        _sut = new GameResultService(_context, admin, _events.Object, Options.Create(_options),
            NullLogger<GameResultService>.Instance);

        _admin = AddUser("Pub", "Lisher");
        _alice = AddUser("Alice", "Able");
        _bob = AddUser("Bob", "Baker");
        _goalie = AddUser("Gary", "Goal");
        _ghost = AddUser("Ghost", "Guest", ghost: true);
        _outsider = AddUser("Olly", "Out");
        _org = new Organization { Name = "Sample League", CreatorId = _admin.Id };
        _context.Organizations.Add(_org);
        _context.OrganizationAdmins.Add(new OrganizationAdmin { OrganizationId = _org.Id, UserId = _admin.Id });
        foreach (var u in new[] { _alice, _bob, _goalie })
            _context.OrganizationSubscriptions.Add(new OrganizationSubscription { OrganizationId = _org.Id, UserId = u.Id });
        _game = AddEvent(new DateTime(2026, 11, 23, 2, 15, 0, DateTimeKind.Utc)); // 11/22 8:15 PM Central
        _context.EventRegistrations.Add(new EventRegistration { EventId = _game.Id, UserId = _alice.Id, Status = "Registered", TeamAssignment = "White" });
        _context.SaveChanges();

        // The event is visible unless a test says otherwise
        VisibleTo(_game.Id);
    }

    public void Dispose() => _context.Dispose();

    // ----------------------------------------------------------------- helpers

    private User AddUser(string first, string last, bool ghost = false)
    {
        var u = new User { Email = $"{first}.{last}@x.test".ToLowerInvariant(), FirstName = first, LastName = last, IsGhostPlayer = ghost };
        _context.Users.Add(u);
        _context.SaveChanges();
        return u;
    }

    private Event AddEvent(DateTime utc, Guid? orgId = null, string status = "Published")
    {
        var e = new Event { OrganizationId = orgId ?? _org.Id, CreatorId = _admin.Id, EventDate = utc, Status = status, Name = "Game" };
        _context.Events.Add(e);
        _context.SaveChanges();
        return e;
    }

    private void VisibleTo(Guid eventId) =>
        _events.Setup(e => e.CanSeeAsync(eventId, It.IsAny<Guid?>())).ReturnsAsync(true);

    private static GamePlayerLineUpsertDto Line(string key, Guid? user, string team, int? goals, int? assists,
        string position = "Skater", int? star = null, int? ga = null, string name = "")
        => new(key, int.TryParse(key.Split(':').Last(), out var n) ? n : null, user, user == null ? "unlinked" : "linked",
            name == "" ? key : name, team, position, "9", false, goals, assists,
            goals.HasValue && assists.HasValue ? goals + assists : null, star, ga, null,
            new Dictionary<string, GameEstimateDto> { ["iceTime"] = new("about 23 min", "about") },
            new Dictionary<string, string> { ["distanceSkated"] = "held back" });

    private static GameGoalUpsertDto Goal(int seq, string team, string? scorer, string[] assists, int b, int w, string src = "person_ruled")
        => new(seq, team, 1, 60 * seq, new Dictionary<string, int> { ["black"] = b, ["white"] = w },
            scorer == null ? null : new PlayerKeyRef(scorer), scorer == null ? null : "person_click",
            assists.Select(a => new PlayerKeyRef(a)).ToList(), src, null);

    /// <summary>White (Home) 2 - Black (Visitors) 1. Alice 1G 1A (star 1), Nameonly 1G (not in app), Bob 1G for Black.</summary>
    private GameResultUpsertRequest Payload(int revision = 1, Guid? eventId = null, string gameKey = "2026-11-22_main",
        Guid? aliceId = null)
    {
        var a = aliceId ?? _alice.Id;
        return new GameResultUpsertRequest(
            GameResultSettings.SchemaVersion,
            new GameResultSourceDto("amp_vision", gameKey, revision, new DateTime(2026, 11, 27, 18, 0, 0, DateTimeKind.Utc), "abc1234"),
            eventId ?? _game.Id,
            new GameResultTeamsDto(new GameResultSideDto("Visitors", 1), new GameResultSideDto("Home", 2)),
            "person", 3, 1020,
            new GameResultVideoDto(null, new List<VideoChapterDto> { new("Period 1", null) }),
            new List<GameGoalUpsertDto>
            {
                Goal(1, "black", "amp:2", Array.Empty<string>(), 1, 0),
                Goal(2, "white", "amp:18", new[] { "amp:64" }, 1, 1),
                Goal(3, "white", "amp:64", new[] { "amp:18" }, 1, 2),
            },
            new List<GameStarUpsertDto> { new(1, "amp:18"), new(2, "amp:64"), new(3, "amp:2") },
            new List<GamePlayerLineUpsertDto>
            {
                Line("amp:18", a, "white", 1, 1, star: 1, name: "Alice Able"),
                Line("amp:64", null, "white", 1, 1, star: 2, name: "Nora Nameonly"),
                Line("amp:2", _bob.Id, "black", 1, 0, star: 3, name: "Bob Baker"),
                Line("amp:5", _goalie.Id, "black", 0, 0, position: "Goalie", ga: 2, name: "Gary Goal"),
            },
            new Dictionary<string, object> { ["savesMeasured"] = false },
            new List<string> { "Saves not measured", "Assists as ruled by a person" },
            "Estimates, not measurements.");
    }

    // ------------------------------------------------------------------ writes

    [Fact]
    public async Task APublishCreatesTheResultWithLinesAndGoals()
    {
        var res = await _sut.PutResultAsync(_game.Id, Payload(), _admin.Id);

        res!.Outcome.Should().Be("created");
        res.NotRegisteredUserIds.Should().BeEquivalentTo(new[] { _bob.Id, _goalie.Id });
        var stored = await _context.GameResults.Include(r => r.PlayerLines).Include(r => r.Goals).SingleAsync();
        stored.OrganizationId.Should().Be(_org.Id);
        stored.BlackScore.Should().Be(1);
        stored.WhiteScore.Should().Be(2);
        stored.PlayerLines.Should().HaveCount(4);
        stored.Goals.Should().HaveCount(3);
        stored.PublishedByUserId.Should().Be(_admin.Id);
        stored.Goals.Single(g => g.Sequence == 3).PrimaryAssistLineId
            .Should().Be(stored.PlayerLines.Single(l => l.SourcePlayerKey == "amp:18").Id);
    }

    [Fact]
    public async Task OnlyAnAdminOfTheEventsOrganizationMayPublish()
    {
        var act = () => _sut.PutResultAsync(_game.Id, Payload(), _alice.Id);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        (await _context.GameResults.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AnUnknownEventIsNull()
    {
        (await _sut.PutResultAsync(Guid.NewGuid(), Payload(), _admin.Id)).Should().BeNull();
    }

    [Fact]
    public async Task AGameNotYetPlayedADraftACancelledOrAnOlderGameIsRefused()
    {
        foreach (var evt in new[]
                 {
                     AddEvent(DateTime.UtcNow.AddDays(2)),
                     AddEvent(new DateTime(2026, 11, 24, 2, 0, 0, DateTimeKind.Utc), status: "Draft"),
                     AddEvent(new DateTime(2026, 11, 24, 2, 0, 0, DateTimeKind.Utc), status: "Cancelled"),
                     AddEvent(new DateTime(2026, 11, 8, 2, 0, 0, DateTimeKind.Utc)), // 11/7: done and posted
                 })
        {
            var act = () => _sut.PutResultAsync(evt.Id, Payload(eventId: evt.Id, gameKey: evt.Id.ToString()), _admin.Id);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }

    [Fact]
    public async Task AnEventWithNoOrganizationIsRefused()
    {
        var standalone = new Event { CreatorId = _admin.Id, EventDate = new DateTime(2026, 11, 24, 2, 0, 0, DateTimeKind.Utc) };
        _context.Events.Add(standalone);
        await _context.SaveChangesAsync();
        var act = () => _sut.PutResultAsync(standalone.Id, Payload(eventId: standalone.Id), _admin.Id);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*organization*");
    }

    [Fact]
    public async Task TheSameRevisionAndBodyAgainIsUnchangedAndWritesNothing()
    {
        await _sut.PutResultAsync(_game.Id, Payload(), _admin.Id);
        var first = await _context.GameResults.AsNoTracking().SingleAsync();

        var again = await _sut.PutResultAsync(_game.Id, Payload(), _admin.Id);

        again!.Outcome.Should().Be("unchanged");
        var after = await _context.GameResults.AsNoTracking().SingleAsync();
        after.UpdatedAt.Should().Be(first.UpdatedAt);
        (await _context.GamePlayerLines.CountAsync()).Should().Be(4);
    }

    [Fact]
    public async Task TheSameRevisionWithAnotherBodyOrAnOlderRevisionIsAConflict()
    {
        await _sut.PutResultAsync(_game.Id, Payload(revision: 2), _admin.Id);

        var changed = Payload(revision: 2) with { Notes = new List<string> { "different" } };
        await FluentActions.Awaiting(() => _sut.PutResultAsync(_game.Id, changed, _admin.Id))
            .Should().ThrowAsync<StaleRevisionException>();
        await FluentActions.Awaiting(() => _sut.PutResultAsync(_game.Id, Payload(revision: 1), _admin.Id))
            .Should().ThrowAsync<StaleRevisionException>().Where(e => e.CurrentRevision == 2);
    }

    [Fact]
    public async Task AHigherRevisionReplacesLinesAndGoalsWhole()
    {
        await _sut.PutResultAsync(_game.Id, Payload(), _admin.Id);
        var oldLineIds = await _context.GamePlayerLines.Select(l => l.Id).ToListAsync();

        // A person re-ruled goal 2: unassisted now
        var p = Payload(revision: 2);
        p.Goals[1] = p.Goals[1] with { Assists = new List<PlayerKeyRef>() };
        p.Players[1] = p.Players[1] with { Assists = 0, Points = 1 };

        var res = await _sut.PutResultAsync(_game.Id, p, _admin.Id);

        res!.Outcome.Should().Be("updated");
        (await _context.GamePlayerLines.CountAsync()).Should().Be(4);
        (await _context.GamePlayerLines.AnyAsync(l => oldLineIds.Contains(l.Id))).Should().BeFalse();
        (await _context.GameGoals.SingleAsync(g => g.Sequence == 2)).PrimaryAssistLineId.Should().BeNull();
        (await _context.GameResults.SingleAsync()).SourceRevision.Should().Be(2);
    }

    [Fact]
    public async Task UnknownAndGhostUserIdsBecomeNameOnlyLinesAndAreReported()
    {
        var p = Payload();
        p.Players[1] = p.Players[1] with { UserId = _ghost.Id };
        p.Players[0] = p.Players[0] with { UserId = Guid.NewGuid() };

        var res = await _sut.PutResultAsync(_game.Id, p, _admin.Id);

        res!.GhostUserIds.Should().Equal(_ghost.Id);
        res.UnknownUserIds.Should().HaveCount(1);
        (await _context.GamePlayerLines.CountAsync(l => l.UserId == null)).Should().Be(2);
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("route")]
    [InlineData("score")]
    [InlineData("crossTeamAssist")]
    [InlineData("unruledAssist")]
    [InlineData("starTwice")]
    [InlineData("qualifier")]
    [InlineData("goalsMismatch")]
    [InlineData("selfAssist")]
    [InlineData("httpVideo")]
    public void InvalidBodiesAreRefusedNamingTheProblem(string fault)
    {
        var p = Payload();
        var bad = fault switch
        {
            "schema" => p with { SchemaVersion = "amp.game_result.v2" },
            "route" => p with { EventId = Guid.NewGuid() },
            "score" => p with { Teams = new GameResultTeamsDto(new("Visitors", 4), new("Home", 5)) },
            "crossTeamAssist" => Swap(p, 1, p.Goals[1] with { Assists = new() { new("amp:2") } }),
            "unruledAssist" => Swap(p, 1, p.Goals[1] with { AssistsSource = "not_ruled" }),
            "starTwice" => p with { Stars = new() { new(1, "amp:18"), new(1, "amp:64") } },
            "qualifier" => SwapLine(p, 0, p.Players[0] with { Estimates = new() { ["iceTime"] = new("23 min", "exactly") } }),
            "goalsMismatch" => SwapLine(p, 0, p.Players[0] with { Goals = 3, Points = 4 }),
            "selfAssist" => Swap(p, 1, p.Goals[1] with { Assists = new() { new("amp:18") } }),
            "httpVideo" => p with { Video = new GameResultVideoDto("http://youtube.com/x", null) },
            _ => p,
        };
        GameResultService.Validate(bad, _game.Id).Should().NotBeNull();
        GameResultService.Validate(p, _game.Id).Should().BeNull();
    }

    private static GameResultUpsertRequest Swap(GameResultUpsertRequest p, int i, GameGoalUpsertDto g)
    {
        var goals = p.Goals.ToList(); goals[i] = g; return p with { Goals = goals };
    }

    private static GameResultUpsertRequest SwapLine(GameResultUpsertRequest p, int i, GamePlayerLineUpsertDto l)
    {
        var lines = p.Players.ToList(); lines[i] = l; return p with { Players = lines };
    }

    [Fact]
    public async Task AGameKeyCannotBePublishedOnTwoEvents()
    {
        await _sut.PutResultAsync(_game.Id, Payload(), _admin.Id);
        var other = AddEvent(new DateTime(2026, 11, 25, 2, 0, 0, DateTimeKind.Utc));
        await FluentActions.Awaiting(() => _sut.PutResultAsync(other.Id, Payload(eventId: other.Id), _admin.Id))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*another event*");
    }

    [Fact]
    public async Task WritesSwitchedOffRefuseEverything()
    {
        _options.WritesEnabled = false;
        await FluentActions.Awaiting(() => _sut.PutResultAsync(_game.Id, Payload(), _admin.Id))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AWithdrawnResultReadsAsNoneAndOnlyAHigherRevisionRepublishes()
    {
        await _sut.PutResultAsync(_game.Id, Payload(), _admin.Id);
        var w = await _sut.WithdrawResultAsync(_game.Id, new WithdrawGameResultRequest("wrong game", 2), _admin.Id);
        w!.Outcome.Should().Be("withdrawn");
        (await _sut.WithdrawResultAsync(_game.Id, new WithdrawGameResultRequest("wrong game", 2), _admin.Id))!
            .Outcome.Should().Be("unchanged");

        (await _sut.GetResultAsync(_game.Id, _alice.Id)).Should().BeNull();
        (await _sut.GetPlayerStatsAsync(_alice.Id, "2026", _alice.Id))!.SeasonLine.Should().BeNull();

        await FluentActions.Awaiting(() => _sut.PutResultAsync(_game.Id, Payload(revision: 2), _admin.Id))
            .Should().ThrowAsync<StaleRevisionException>();
        (await _sut.PutResultAsync(_game.Id, Payload(revision: 3), _admin.Id))!.Outcome.Should().Be("updated");
        (await _sut.GetResultAsync(_game.Id, _alice.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task OnlyAnAdminMayWithdraw()
    {
        await _sut.PutResultAsync(_game.Id, Payload(), _admin.Id);
        await FluentActions.Awaiting(() => _sut.WithdrawResultAsync(_game.Id, new("x", 2), _alice.Id))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    // ------------------------------------------------------------------- reads

    [Fact]
    public async Task TheRecapNamesEveryoneAndLinksOnlyAppAccounts()
    {
        await _sut.PutResultAsync(_game.Id, Payload(), _admin.Id);

        var r = await _sut.GetResultAsync(_game.Id, _alice.Id);

        r!.Winner.Should().Be("White");
        r.White.Label.Should().Be("Home");
        r.Goals.Should().HaveCount(3);
        r.Goals[1].Scorer!.UserId.Should().Be(_alice.Id);
        r.Goals[1].Assists.Single().UserId.Should().BeNull();          // Nameonly: name only
        r.Goals[1].Assists.Single().DisplayName.Should().Be("Nora Nameonly");
        r.Goals[0].AssistsRuled.Should().BeTrue();
        r.Goals[0].Assists.Should().BeEmpty();                          // unassisted, as ruled
        r.Stars.Select(s => s.Star).Should().Equal(1, 2, 3);
        r.WhiteLines.Should().HaveCount(2);
        r.BlackLines.Last().Position.Should().Be("Goalie");
        r.WhiteLines.First().Estimates!["iceTime"].Display.Should().Be("about 23 min");
        r.Notes.Should().Contain("Saves not measured");
    }

    [Fact]
    public async Task TheRecapFollowsTheEventsOwnVisibility()
    {
        await _sut.PutResultAsync(_game.Id, Payload(), _admin.Id);
        _events.Setup(e => e.CanSeeAsync(_game.Id, _outsider.Id)).ReturnsAsync(false);

        (await _sut.GetResultAsync(_game.Id, _outsider.Id)).Should().BeNull();
    }

    [Fact]
    public async Task ReadsSwitchedOffReturnNothingAnywhere()
    {
        await _sut.PutResultAsync(_game.Id, Payload(), _admin.Id);
        _options.ReadsEnabled = false;

        (await _sut.GetResultAsync(_game.Id, _alice.Id)).Should().BeNull();
        (await _sut.GetPlayerStatsAsync(_alice.Id, null, _alice.Id)).Should().BeNull();
        (await _sut.GetLeaderboardAsync(_org.Id, null, null, _alice.Id)).Should().BeNull();
        (await _sut.GetRecentResultsAsync(_org.Id, 5, _alice.Id)).Should().BeNull();
        (await _sut.GetStatsVisibilityAsync(_alice.Id)).Should().BeNull();
    }

    [Fact]
    public async Task PlayerStatsAreTotalledOnReadForTheSeason()
    {
        await _sut.PutResultAsync(_game.Id, Payload(), _admin.Id);

        var s = await _sut.GetPlayerStatsAsync(_alice.Id, "2026", _bob.Id);

        s!.StatsHidden.Should().BeFalse();
        s.SeasonLine!.GamesWithStats.Should().Be(1);
        s.SeasonLine.Points.Should().Be(2);
        s.SeasonLine.Stars.Should().Be(1);
        s.SeasonLine.GoalsAgainst.Should().BeNull();
        s.GameLog.Single().Result.Should().Be("W");
        s.GameLog.Single().OpponentLabel.Should().Be("Visitors");
        s.GameLog.Single().TeamScore.Should().Be(2);
        (await _sut.GetPlayerStatsAsync(_alice.Id, "2027", _bob.Id))!.SeasonLine.Should().BeNull();

        var g = await _sut.GetPlayerStatsAsync(_goalie.Id, "2026", _bob.Id);
        g!.SeasonLine!.GoalsAgainst.Should().Be(2);
        g.GameLog.Single().Result.Should().Be("L");
    }

    [Fact]
    public async Task APlayerWithNoGamesHasAnEmptyLineNotAnError()
    {
        var s = await _sut.GetPlayerStatsAsync(_bob.Id, null, _alice.Id);
        s!.SeasonLine.Should().BeNull();
        s.Career.Should().BeNull();
        s.GameLog.Should().BeEmpty();
    }

    [Fact]
    public async Task GamesRegisteredForWithoutStatsAreListedOnlyWhereTheCallerMaySeeThem()
    {
        await _sut.PutResultAsync(_game.Id, Payload(), _admin.Id);
        Event Registered(DateTime utc, string team = "White", bool rosterPublished = true, string status = "Published",
            Guid? orgId = null, bool visible = true, string regStatus = "Registered")
        {
            var e = AddEvent(utc, orgId, status);
            e.IsRosterPublished = rosterPublished;
            _context.EventRegistrations.Add(new EventRegistration { EventId = e.Id, UserId = _alice.Id, Status = regStatus, TeamAssignment = team });
            _context.SaveChanges();
            if (visible) VisibleTo(e.Id);
            return e;
        }
        var nov7 = Registered(new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc));
        var sep26 = Registered(new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc), team: "Black", rosterPublished: false);
        Registered(new DateTime(2026, 9, 12, 0, 0, 0, DateTimeKind.Utc), visible: false);          // invite-only, Bob not invited
        Registered(new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc), status: "Cancelled");
        Registered(new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc), regStatus: "Cancelled");  // she dropped out
        Registered(new DateTime(2025, 11, 1, 0, 0, 0, DateTimeKind.Utc));                          // another season
        Registered(DateTime.UtcNow.AddDays(3));                                                     // not played yet
        var otherOrg = new Organization { Name = "Elsewhere", CreatorId = _admin.Id };
        _context.Organizations.Add(otherOrg);
        _context.OrganizationSubscriptions.Add(new OrganizationSubscription { OrganizationId = otherOrg.Id, UserId = _alice.Id });
        _context.SaveChanges();
        var elsewhere = Registered(new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc), orgId: otherOrg.Id); // Bob is not a member

        var s = await _sut.GetPlayerStatsAsync(_alice.Id, "2026", _bob.Id);

        s!.GameLog.Single().EventId.Should().Be(_game.Id);              // the game with stats is not repeated
        s.GamesWithoutStats.Select(g => g.EventId).Should().Equal(nov7.Id, sep26.Id);  // newest first
        s.GamesWithoutStats[0].Team.Should().Be("White");
        s.GamesWithoutStats[1].Team.Should().BeNull();                  // roster never published: no team shown

        var mine = await _sut.GetPlayerStatsAsync(_alice.Id, "2026", _alice.Id);
        mine!.GamesWithoutStats.Select(g => g.EventId).Should().Contain(elsewhere.Id);
    }

    [Fact]
    public async Task SomeoneWhoSharesNoOrganizationCannotSeeAPlayer()
    {
        await _sut.PutResultAsync(_game.Id, Payload(), _admin.Id);
        (await _sut.GetPlayerStatsAsync(_alice.Id, null, _outsider.Id)).Should().BeNull();
        (await _sut.GetPlayerStatsAsync(_ghost.Id, null, _alice.Id)).Should().BeNull();
        await FluentActions.Awaiting(() => _sut.GetPlayerStatsAsync(_alice.Id, "26", _bob.Id))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task HidingStatsHidesTheProfileTheBoxLineAndTheLeaderboardRowButNotTheGoal()
    {
        await _sut.PutResultAsync(_game.Id, Payload(), _admin.Id);
        (await _sut.SetStatsVisibilityAsync(_alice.Id, true))!.Hidden.Should().BeTrue();
        (await _sut.GetStatsVisibilityAsync(_alice.Id))!.Hidden.Should().BeTrue();

        var other = await _sut.GetPlayerStatsAsync(_alice.Id, null, _bob.Id);
        other!.StatsHidden.Should().BeTrue();
        other.SeasonLine.Should().BeNull();
        other.GameLog.Should().BeEmpty();
        other.GamesWithoutStats.Should().BeEmpty();
        (await _sut.GetPlayerStatsAsync(_alice.Id, null, _alice.Id))!.SeasonLine.Should().NotBeNull(); // she still sees hers

        var recap = await _sut.GetResultAsync(_game.Id, _bob.Id);
        recap!.WhiteLines.Should().NotContain(l => l.Player.UserId == _alice.Id);
        recap.Goals[1].Scorer!.DisplayName.Should().Be("Alice Able");

        var board = await _sut.GetLeaderboardAsync(_org.Id, "2026", "points", _bob.Id);
        board!.Rows.Should().NotContain(r => r.UserId == _alice.Id);
    }

    [Fact]
    public async Task TheLeaderboardRanksLinkedPlayersOnlyWithTiesSharingARank()
    {
        await _sut.PutResultAsync(_game.Id, Payload(), _admin.Id);

        var b = await _sut.GetLeaderboardAsync(_org.Id, "2026", "goals", _alice.Id);

        b!.GamesWithStats.Should().Be(1);
        b.Rows.Select(r => r.UserId).Should().NotContain(Guid.Empty);
        b.Rows.Should().HaveCount(3);                       // Alice, Bob, Gary - not Nameonly
        b.Rows[0].UserId.Should().Be(_alice.Id);            // 1G, more points than Bob
        b.Rows[1].UserId.Should().Be(_bob.Id);
        b.Rows[1].Rank.Should().Be(1);                      // tied on goals
        b.Rows[2].Rank.Should().Be(3);
    }

    [Fact]
    public async Task TheLeaderboardIsForMembersAndKnownStatsOnly()
    {
        await FluentActions.Awaiting(() => _sut.GetLeaderboardAsync(_org.Id, null, null, _outsider.Id))
            .Should().ThrowAsync<UnauthorizedAccessException>();
        await FluentActions.Awaiting(() => _sut.GetLeaderboardAsync(_org.Id, null, "iceTime", _alice.Id))
            .Should().ThrowAsync<InvalidOperationException>();
        (await _sut.GetLeaderboardAsync(Guid.NewGuid(), null, null, _alice.Id)).Should().BeNull();
        (await _sut.GetLeaderboardAsync(_org.Id, null, null, _admin.Id))!.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task RecentResultsAreNewestFirstAndForMembers()
    {
        await _sut.PutResultAsync(_game.Id, Payload(), _admin.Id);
        var later = AddEvent(new DateTime(2026, 11, 25, 2, 0, 0, DateTimeKind.Utc));
        await _sut.PutResultAsync(later.Id, Payload(eventId: later.Id, gameKey: "2026-11-24_main"), _admin.Id);

        var list = await _sut.GetRecentResultsAsync(_org.Id, 5, _alice.Id);

        list!.Select(r => r.EventId).Should().Equal(later.Id, _game.Id);
        list[0].White.Score.Should().Be(2);
        (await _sut.GetRecentResultsAsync(_org.Id, 1, _alice.Id))!.Should().HaveCount(1);
        await FluentActions.Awaiting(() => _sut.GetRecentResultsAsync(_org.Id, 5, _outsider.Id))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task AGoalWithNoIdentifiedScorerMakesTheTotalsAFloor()
    {
        var p = Payload();
        p = Swap(p, 0, p.Goals[0] with { Scorer = null, ScorerSource = null });
        p = SwapLine(p, 2, p.Players[2] with { Goals = 0, Points = 0 });
        await _sut.PutResultAsync(_game.Id, p, _admin.Id);

        var s = await _sut.GetPlayerStatsAsync(_alice.Id, "2026", _alice.Id);
        s!.SeasonLine!.GamesWithUnknownScorers.Should().Be(1);
        (await _sut.GetResultAsync(_game.Id, _alice.Id))!.Goals[0].Scorer.Should().BeNull();
    }

    [Fact]
    public async Task AStoredJsonBlobThatNoLongerParsesReadsAsAbsent()
    {
        await _sut.PutResultAsync(_game.Id, Payload(), _admin.Id);
        var line = await _context.GamePlayerLines.FirstAsync(l => l.SourcePlayerKey == "amp:18");
        line.EstimatesJson = "{not json";
        var res = await _context.GameResults.SingleAsync();
        res.NotesJson = "[1,";
        await _context.SaveChangesAsync();

        var r = await _sut.GetResultAsync(_game.Id, _alice.Id);
        r!.Notes.Should().BeEmpty();
        r.WhiteLines.Single(l => l.Player.UserId == _alice.Id).Estimates.Should().BeNull();
    }

    [Fact]
    public void TheSeasonIsTheCentralTimeCalendarYear()
    {
        GameResultService.SeasonOf(new DateTime(2027, 1, 1, 3, 0, 0, DateTimeKind.Utc)).Should().Be("2026"); // 9 PM Dec 31 Central
        GameResultService.SeasonOf(new DateTime(2027, 1, 1, 7, 0, 0, DateTimeKind.Utc)).Should().Be("2027");
    }
}
