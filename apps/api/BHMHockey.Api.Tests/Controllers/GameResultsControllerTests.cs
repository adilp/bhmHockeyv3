using System.Security.Claims;
using BHMHockey.Api.Controllers;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Exceptions;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace BHMHockey.Api.Tests.Controllers;

/// <summary>
/// Status codes of the game-results endpoints. The app logs a user out on 401,
/// so no read may ever answer 401 to a signed-in caller: absence is 404,
/// refusal 403.
/// </summary>
public class GameResultsControllerTests
{
    private readonly Mock<IGameResultService> _results = new();
    private readonly Mock<IPastGameService> _past = new();
    private readonly GameResultsController _sut;
    private readonly Guid _me = Guid.NewGuid();

    public GameResultsControllerTests()
    {
        _sut = new GameResultsController(_results.Object, _past.Object, Mock.Of<ILogger<GameResultsController>>());
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, _me.ToString()) }, "Test")),
            },
        };
    }

    private static GameResultUpsertRequest Body() => new("amp.game_result.v1",
        new GameResultSourceDto("amp_vision", "k", 1, DateTime.UtcNow, null), Guid.Empty,
        new GameResultTeamsDto(new(null, 0), new(null, 0)), "person", null, null, null,
        new(), new(), new(), null, null, null);

    [Fact]
    public async Task PutMapsEachOutcomeToItsStatus()
    {
        var id = Guid.NewGuid();
        _results.Setup(s => s.PutResultAsync(id, It.IsAny<GameResultUpsertRequest>(), _me))
            .ReturnsAsync(new GameResultUpsertResponse("created", 1, new(), new(), new()));
        (await _sut.PutResult(id, Body())).Result.Should().BeOfType<OkObjectResult>();

        _results.Setup(s => s.PutResultAsync(id, It.IsAny<GameResultUpsertRequest>(), _me)).ReturnsAsync((GameResultUpsertResponse?)null);
        (await _sut.PutResult(id, Body())).Result.Should().BeOfType<NotFoundObjectResult>();

        _results.Setup(s => s.PutResultAsync(id, It.IsAny<GameResultUpsertRequest>(), _me)).ThrowsAsync(new UnauthorizedAccessException());
        (await _sut.PutResult(id, Body())).Result.Should().BeOfType<ForbidResult>();

        _results.Setup(s => s.PutResultAsync(id, It.IsAny<GameResultUpsertRequest>(), _me)).ThrowsAsync(new StaleRevisionException("old", 3));
        (await _sut.PutResult(id, Body())).Result.Should().BeOfType<ConflictObjectResult>();

        _results.Setup(s => s.PutResultAsync(id, It.IsAny<GameResultUpsertRequest>(), _me)).ThrowsAsync(new InvalidOperationException("bad"));
        (await _sut.PutResult(id, Body())).Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ReadsAnswer404WhenThereIsNothingAndNever401()
    {
        var id = Guid.NewGuid();
        (await _sut.GetResult(id)).Result.Should().BeOfType<NotFoundObjectResult>();
        (await _sut.GetPlayerStats(id, null)).Result.Should().BeOfType<NotFoundObjectResult>();
        (await _sut.GetLeaderboard(id, null, null)).Result.Should().BeOfType<NotFoundObjectResult>();
        (await _sut.GetRecentResults(id)).Result.Should().BeOfType<NotFoundObjectResult>();
        (await _sut.GetStatsVisibility()).Result.Should().BeOfType<NotFoundObjectResult>();

        _results.Setup(s => s.GetLeaderboardAsync(id, null, null, _me)).ThrowsAsync(new UnauthorizedAccessException());
        (await _sut.GetLeaderboard(id, null, null)).Result.Should().BeOfType<ForbidResult>();
        _results.Setup(s => s.GetRecentResultsAsync(id, 5, _me)).ThrowsAsync(new UnauthorizedAccessException());
        (await _sut.GetRecentResults(id)).Result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task ABadSeasonOrStatIs400()
    {
        var id = Guid.NewGuid();
        _results.Setup(s => s.GetPlayerStatsAsync(id, "x", _me)).ThrowsAsync(new InvalidOperationException("season"));
        (await _sut.GetPlayerStats(id, "x")).Result.Should().BeOfType<BadRequestObjectResult>();
        _results.Setup(s => s.GetLeaderboardAsync(id, null, "x", _me)).ThrowsAsync(new InvalidOperationException("stat"));
        (await _sut.GetLeaderboard(id, null, "x")).Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PastGameMapsAdminRefusalAndBadInput()
    {
        var org = Guid.NewGuid();
        var req = new CreatePastGameRequest(null, null, DateTime.UtcNow, null, null, null, "k", new());
        _past.Setup(s => s.CreatePastGameAsync(org, req, _me)).ThrowsAsync(new UnauthorizedAccessException());
        (await _sut.CreatePastGame(org, req)).Result.Should().BeOfType<ForbidResult>();
        _past.Setup(s => s.CreatePastGameAsync(org, req, _me)).ThrowsAsync(new InvalidOperationException("x"));
        (await _sut.CreatePastGame(org, req)).Result.Should().BeOfType<BadRequestObjectResult>();
        _past.Setup(s => s.CreatePastGameAsync(org, req, _me)).ReturnsAsync((CreatePastGameResponse?)null);
        (await _sut.CreatePastGame(org, req)).Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void EveryGameResultsEndpointRequiresSignIn()
    {
        typeof(GameResultsController).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false)
            .Should().NotBeEmpty();
        typeof(GameResultsController).GetMethods()
            .Where(m => m.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), false).Any())
            .Should().BeEmpty();
    }
}
