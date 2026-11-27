using System.Runtime.CompilerServices;
using System.Text.Json;
using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
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
/// A body in exactly the shape AMP Vision's exporter (app/league_results.py)
/// writes, with its edge cases (linked and name-only lines, a loaner, an unknown
/// jersey, goalies, estimates held back, assists ruled), is accepted as written:
/// it deserializes the way ASP.NET binds it, passes every validation rule, and
/// publishes. The fixture is SYNTHETIC: invented names, ids and numbers; no real
/// game's data belongs in this repository.
/// </summary>
public class AmpVisionPayloadContractTests
{
    private static string Fixture([CallerFilePath] string path = "") =>
        Path.Combine(Path.GetDirectoryName(path)!, "..", "Fixtures", "synthetic.game_result.json");

    private static GameResultUpsertRequest Load() =>
        JsonSerializer.Deserialize<GameResultUpsertRequest>(File.ReadAllText(Fixture()),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    [Fact]
    public void TheExportedBodyPassesEveryRule()
    {
        var body = Load();
        body.Players.Should().HaveCount(22);
        body.Goals.Should().HaveCount(7);
        body.Players.Should().Contain(p => p.IsLoaner).And.Contain(p => p.Jersey == null)
            .And.Contain(p => p.EstimatesWithheld != null).And.Contain(p => p.UserId == null);
        GameResultService.Validate(body, body.EventId).Should().BeNull();
    }

    [Fact]
    public async Task TheExportedBodyPublishesAndReadsBack()
    {
        var body = Load();
        var ctx = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        var admin = new User { Email = "pub@x.test", FirstName = "P", LastName = "L" };
        ctx.Users.Add(admin);
        foreach (var id in body.Players.Where(p => p.UserId.HasValue).Select(p => p.UserId!.Value))
            ctx.Users.Add(new User { Id = id, Email = $"{id}@x.test", FirstName = "F", LastName = "L" });
        var org = new Organization { Name = "Sample League", CreatorId = admin.Id };
        ctx.Organizations.Add(org);
        ctx.OrganizationAdmins.Add(new OrganizationAdmin { OrganizationId = org.Id, UserId = admin.Id });
        ctx.Events.Add(new Event { Id = body.EventId, OrganizationId = org.Id, CreatorId = admin.Id,
            EventDate = new DateTime(2026, 11, 25, 1, 0, 0, DateTimeKind.Utc) });
        await ctx.SaveChangesAsync();
        var visible = new Mock<IEventVisibility>();
        visible.Setup(v => v.CanSeeAsync(It.IsAny<Guid>(), It.IsAny<Guid?>())).ReturnsAsync(true);
        var sut = new GameResultService(ctx, new OrganizationAdminService(ctx), visible.Object,
            Options.Create(new GameResultOptions()), NullLogger<GameResultService>.Instance);

        var res = await sut.PutResultAsync(body.EventId, body, admin.Id);

        res!.Outcome.Should().Be("created");
        res.UnknownUserIds.Should().BeEmpty();
        var recap = await sut.GetResultAsync(body.EventId, admin.Id);
        recap!.Black.Label.Should().Be("Sample Black");
        recap.White.Score.Should().Be(4);
        recap.Winner.Should().Be("White");
        recap.Goals.Should().OnlyContain(g => g.AssistsRuled);
        (recap.BlackLines.Count + recap.WhiteLines.Count).Should().Be(22);
        (await sut.PutResultAsync(body.EventId, body, admin.Id))!.Outcome.Should().Be("unchanged");
    }
}
