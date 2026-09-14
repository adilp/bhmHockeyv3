using System.Text.RegularExpressions;
using BHMHockey.Api.Data;
using BHMHockey.Api.Models.DTOs;
using BHMHockey.Api.Models.Entities;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// Tests for self-service password reset. These guard an account-takeover path,
/// so they check the failure cases as carefully as the happy path.
/// </summary>
public class PasswordResetServiceTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly FakeEmailSender _email = new();
    private readonly PasswordResetService _sut;

    public PasswordResetServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _context = new AppDbContext(options);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PasswordReset:LinkBaseUrl"] = "https://bhmhockey.com/reset",
                ["PasswordReset:ExpiryMinutes"] = "30"
            })
            .Build();

        _sut = new PasswordResetService(_context, _email, config, NullLogger<PasswordResetService>.Instance);
    }

    public void Dispose() => _context.Dispose();

    #region Helpers

    private sealed class FakeEmailSender : IEmailSender
    {
        public List<(string To, string Subject, string Text, string Html)> Sent { get; } = new();
        public bool Throw { get; set; }

        public Task SendAsync(string toAddress, string subject, string textBody, string htmlBody, CancellationToken cancellationToken = default)
        {
            if (Throw) throw new InvalidOperationException("SMTP down");
            Sent.Add((toAddress, subject, textBody, htmlBody));
            return Task.CompletedTask;
        }
    }

    private async Task<User> CreateUser(string email = "player@example.com", bool active = true, bool ghost = false)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("old-password"),
            FirstName = "Britt",
            LastName = "Guimond",
            Role = "Player",
            IsActive = active,
            IsGhostPlayer = ghost,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    private static string TokenFrom(string text) =>
        Uri.UnescapeDataString(Regex.Match(text, @"token=([^\s]+)").Groups[1].Value);

    private static string CodeFrom(string text) =>
        Regex.Match(text, @"code in the app: (\d{6})").Groups[1].Value;

    private async Task<(string Token, string Code)> RequestFor(string email)
    {
        await _sut.RequestResetAsync(email);
        var text = _email.Sent.Last().Text;
        return (TokenFrom(text), CodeFrom(text));
    }

    private async Task<bool> PasswordIs(Guid userId, string password)
    {
        var user = await _context.Users.AsNoTracking().SingleAsync(u => u.Id == userId);
        return BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
    }

    #endregion

    #region Request

    [Fact]
    public async Task RequestResetAsync_UnknownEmail_SendsNothing()
    {
        await _sut.RequestResetAsync("nobody@example.com");

        _email.Sent.Should().BeEmpty();
        _context.PasswordResetTokens.Should().BeEmpty();
    }

    [Fact]
    public async Task RequestResetAsync_SendsLinkAndCode_AndStoresOnlyHashes()
    {
        var user = await CreateUser();

        var (token, code) = await RequestFor(user.Email);

        _email.Sent.Should().ContainSingle().Which.To.Should().Be(user.Email);
        token.Should().MatchRegex("^[A-Za-z0-9_-]{32,128}$");   // same shape the Cloudflare page accepts
        code.Should().MatchRegex(@"^\d{6}$");
        _email.Sent[0].Html.Should().Contain("https://bhmhockey.com/reset?token=");

        var row = await _context.PasswordResetTokens.SingleAsync();
        row.TokenHash.Should().NotContain(token);
        row.CodeHash.Should().NotContain(code);
        row.ExpiresAt.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(30), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task RequestResetAsync_MatchesEmailIgnoringCase()
    {
        await CreateUser("Britt@Example.com");

        await _sut.RequestResetAsync("  britt@example.COM ");

        _email.Sent.Should().ContainSingle();
    }

    [Theory]
    [InlineData(false, false)]   // deleted account
    [InlineData(true, true)]     // guest placeholder
    public async Task RequestResetAsync_DeletedOrGhostAccount_SendsNothing(bool active, bool ghost)
    {
        var user = await CreateUser(active: active, ghost: ghost);

        await _sut.RequestResetAsync(user.Email);

        _email.Sent.Should().BeEmpty();
    }

    [Fact]
    public async Task RequestResetAsync_RateLimitedAfterThreeInAWindow()
    {
        var user = await CreateUser();

        for (var i = 0; i < 5; i++)
        {
            await _sut.RequestResetAsync(user.Email);
        }

        _email.Sent.Should().HaveCount(3);
    }

    [Fact]
    public async Task RequestResetAsync_EmailFailure_DoesNotThrow()
    {
        // A failure must look the same to the caller as success, or it reveals the account exists
        var user = await CreateUser();
        _email.Throw = true;

        var act = async () => await _sut.RequestResetAsync(user.Email);

        await act.Should().NotThrowAsync();
    }

    #endregion

    #region Confirm

    [Fact]
    public async Task ConfirmResetAsync_WithLinkToken_ResetsPassword()
    {
        var user = await CreateUser();
        var (token, _) = await RequestFor(user.Email);

        await _sut.ConfirmResetAsync(new ConfirmPasswordResetRequest("new-password", Token: token));

        (await PasswordIs(user.Id, "new-password")).Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmResetAsync_WithEmailAndCode_ResetsPassword()
    {
        var user = await CreateUser();
        var (_, code) = await RequestFor(user.Email);

        await _sut.ConfirmResetAsync(new ConfirmPasswordResetRequest("new-password", Email: "PLAYER@example.com", Code: code));

        (await PasswordIs(user.Id, "new-password")).Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmResetAsync_TokenWorksOnlyOnce_AndUsingItKillsTheCode()
    {
        var user = await CreateUser();
        var (token, code) = await RequestFor(user.Email);
        await _sut.ConfirmResetAsync(new ConfirmPasswordResetRequest("new-password", Token: token));

        var again = async () => await _sut.ConfirmResetAsync(new ConfirmPasswordResetRequest("another-one", Token: token));
        var byCode = async () => await _sut.ConfirmResetAsync(new ConfirmPasswordResetRequest("another-one", Email: user.Email, Code: code));

        await again.Should().ThrowAsync<InvalidOperationException>();
        await byCode.Should().ThrowAsync<InvalidOperationException>();
        (await PasswordIs(user.Id, "new-password")).Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmResetAsync_NewerRequestInvalidatesTheOlderEmail()
    {
        var user = await CreateUser();
        var (oldToken, _) = await RequestFor(user.Email);
        await RequestFor(user.Email);

        var act = async () => await _sut.ConfirmResetAsync(new ConfirmPasswordResetRequest("new-password", Token: oldToken));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(PasswordResetService.InvalidMessage);
        (await PasswordIs(user.Id, "old-password")).Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmResetAsync_FiveWrongCodes_KillTheCodeEvenForTheRightOne()
    {
        var user = await CreateUser();
        var (_, code) = await RequestFor(user.Email);
        var wrong = code == "000000" ? "111111" : "000000";

        for (var i = 0; i < 5; i++)
        {
            var guess = async () => await _sut.ConfirmResetAsync(new ConfirmPasswordResetRequest("new-password", Email: user.Email, Code: wrong));
            await guess.Should().ThrowAsync<InvalidOperationException>();
        }

        var right = async () => await _sut.ConfirmResetAsync(new ConfirmPasswordResetRequest("new-password", Email: user.Email, Code: code));
        await right.Should().ThrowAsync<InvalidOperationException>();
        (await PasswordIs(user.Id, "old-password")).Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmResetAsync_ExpiredToken_Fails()
    {
        var user = await CreateUser();
        var (token, _) = await RequestFor(user.Email);
        var row = await _context.PasswordResetTokens.SingleAsync();
        row.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await _context.SaveChangesAsync();

        var act = async () => await _sut.ConfirmResetAsync(new ConfirmPasswordResetRequest("new-password", Token: token));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ConfirmResetAsync_ShortPassword_FailsWithoutUsingTheToken()
    {
        var user = await CreateUser();
        var (token, _) = await RequestFor(user.Email);

        var shortOne = async () => await _sut.ConfirmResetAsync(new ConfirmPasswordResetRequest("12345", Token: token));
        await shortOne.Should().ThrowAsync<InvalidOperationException>().WithMessage("*at least 6*");

        // the player can fix their typo and still use the same email
        await _sut.ConfirmResetAsync(new ConfirmPasswordResetRequest("long-enough", Token: token));
        (await PasswordIs(user.Id, "long-enough")).Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmResetAsync_WithNeitherTokenNorCode_Fails()
    {
        var act = async () => await _sut.ConfirmResetAsync(new ConfirmPasswordResetRequest("new-password"));

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ConfirmResetAsync_BogusToken_Fails()
    {
        await CreateUser();

        var act = async () => await _sut.ConfirmResetAsync(new ConfirmPasswordResetRequest("new-password", Token: "not-a-real-token-at-all-xxxxxxxxxxxxxxxx"));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(PasswordResetService.InvalidMessage);
    }

    #endregion
}
