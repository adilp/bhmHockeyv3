using BHMHockey.Api.Controllers;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using Xunit;

namespace BHMHockey.Api.Tests.Controllers;

/// <summary>
/// Tests for EmailDiagnosticsController - admin-only, and only ever mails the
/// caller's own address.
/// </summary>
public class EmailDiagnosticsControllerTests
{
    private readonly Mock<ISmtpDiagnostics> _mockDiagnostics = new();
    private readonly Mock<IUserService> _mockUserService = new();
    private readonly EmailDiagnosticsController _sut;
    private readonly Guid _userId = Guid.NewGuid();

    public EmailDiagnosticsControllerTests()
    {
        _sut = new EmailDiagnosticsController(
            _mockDiagnostics.Object,
            _mockUserService.Object,
            Mock.Of<ILogger<EmailDiagnosticsController>>());
    }

    private void SetupCaller(string role)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, _userId.ToString()),
            new Claim(ClaimTypes.Role, role),
        }, "TestAuth");

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
    }

    private static UserDto UserWithEmail(Guid id, string email) => new(
        id, email, "Test", "Admin", null, null, null, "Admin", DateTime.UtcNow);

    private static SmtpTestResponse Result(string to) => new(
        true, to, null, null, null, null, 120,
        new SmtpSettingsReport("host", 587, "from@example.com",
            new SmtpCredentialReport(true, 20, false, "AKIA…2345", null),
            new SmtpCredentialReport(true, 44, false, null, null)));

    [Theory]
    [InlineData("Player")]
    [InlineData("Organizer")]
    public async Task SendTestEmail_NonAdmin_IsForbiddenAndSendsNothing(string role)
    {
        SetupCaller(role);

        var result = await _sut.SendTestEmail(CancellationToken.None);

        result.Result.Should().BeOfType<ForbidResult>();
        _mockDiagnostics.Verify(d => d.SendTestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendTestEmail_Admin_SendsToTheirOwnAddressAndReturnsTheResult()
    {
        SetupCaller("Admin");
        _mockUserService.Setup(s => s.GetUserByIdAsync(_userId))
            .ReturnsAsync(UserWithEmail(_userId, "me@example.com"));
        _mockDiagnostics.Setup(d => d.SendTestAsync("me@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result("me@example.com"));

        var result = await _sut.SendTestEmail(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeOfType<SmtpTestResponse>().Which.To.Should().Be("me@example.com");
        _mockDiagnostics.Verify(d => d.SendTestAsync("me@example.com", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendTestEmail_AdminWithNoAccount_ReturnsNotFound()
    {
        SetupCaller("Admin");
        _mockUserService.Setup(s => s.GetUserByIdAsync(_userId)).ReturnsAsync((UserDto?)null);

        var result = await _sut.SendTestEmail(CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
        _mockDiagnostics.Verify(d => d.SendTestAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
