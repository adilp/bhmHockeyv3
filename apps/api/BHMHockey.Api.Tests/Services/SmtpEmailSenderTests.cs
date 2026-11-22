using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using BHMHockey.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace BHMHockey.Api.Tests.Services;

/// <summary>
/// Tests for SmtpEmailSender's admin diagnostics and its unconfigured-sender
/// behaviour. No test here reaches a real mail server.
/// </summary>
public class SmtpEmailSenderTests
{
    private const string To = "admin@example.com";
    private const string SmtpPassword = "BCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopq12"; // 44 chars, SES SMTP shape
    private const string SmtpUsername = "AKIAEXAMPLEUSER12345";

    private static SmtpEmailSender CreateSender(Dictionary<string, string?> smtp, string environment = "Production")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(smtp.ToDictionary(kv => $"Smtp:{kv.Key}", kv => kv.Value))
            .Build();
        var env = Mock.Of<IHostEnvironment>(e => e.EnvironmentName == environment);
        return new SmtpEmailSender(config, env, NullLogger<SmtpEmailSender>.Instance);
    }

    private static Dictionary<string, string?> Configured(int port, string? username = SmtpUsername, string? password = SmtpPassword) => new()
    {
        ["Host"] = "127.0.0.1",
        ["Port"] = port.ToString(),
        ["Username"] = username,
        ["Password"] = password,
        ["FromAddress"] = "admin@bhmhockey.com",
    };

    /// <summary>A local port with nothing listening on it.</summary>
    private static int ClosedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    #region SendTestAsync - stages

    [Fact]
    public async Task SendTestAsync_NotConfigured_ReportsConfigurationStage()
    {
        var sut = CreateSender(new());

        var result = await sut.SendTestAsync(To);

        result.Sent.Should().BeFalse();
        result.FailedStage.Should().Be("configuration");
        result.Settings.Username.Present.Should().BeFalse();
        result.Settings.Password.Present.Should().BeFalse();
    }

    [Fact]
    public async Task SendTestAsync_NotConfiguredInDevelopment_StillReportsFailure()
    {
        // Real sends quietly log instead in Development; the test must not, or
        // it would report success for an email that never left
        var sut = CreateSender(new(), environment: "Development");

        var result = await sut.SendTestAsync(To);

        result.Sent.Should().BeFalse();
        result.FailedStage.Should().Be("configuration");
    }

    [Fact]
    public async Task SendTestAsync_NothingListening_FailsAtConnectWithoutThrowing()
    {
        var sut = CreateSender(Configured(ClosedPort()));

        var result = await sut.SendTestAsync(To);

        result.Sent.Should().BeFalse();
        result.FailedStage.Should().Be("connect");
        result.ErrorType.Should().NotBeNullOrEmpty();
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SendTestAsync_ServerWithoutStartTls_StopsBeforeSendingCredentials()
    {
        // A server that never offers STARTTLS must not be handed the login in
        // plain text: the attempt has to stop at connect, not reach authenticate
        using var server = new TcpListener(IPAddress.Loopback, 0);
        server.Start();
        var port = ((IPEndPoint)server.LocalEndpoint).Port;
        var received = new StringBuilder();

        var serverTask = Task.Run(async () =>
        {
            using var client = await server.AcceptTcpClientAsync();
            using var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII);
            using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
            await writer.WriteLineAsync("220 test ESMTP");
            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                received.AppendLine(line);
                if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("250-test");
                    await writer.WriteLineAsync("250 AUTH LOGIN PLAIN");  // no STARTTLS
                }
                else if (line.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteLineAsync("221 bye");
                    break;
                }
                else
                {
                    await writer.WriteLineAsync("250 ok");
                }
            }
        });

        var sut = CreateSender(Configured(port));

        var result = await sut.SendTestAsync(To);
        server.Stop();
        await Task.WhenAny(serverTask, Task.Delay(2000));

        result.Sent.Should().BeFalse();
        result.FailedStage.Should().Be("connect");
        received.ToString().Should().NotContain("AUTH");
    }

    #endregion

    #region SendTestAsync - credential report

    [Fact]
    public async Task SendTestAsync_NeverRevealsThePassword()
    {
        var sut = CreateSender(Configured(ClosedPort()));

        var result = await sut.SendTestAsync(To);
        var json = JsonSerializer.Serialize(result);

        json.Should().NotContain(SmtpPassword);
        json.Should().NotContain(SmtpPassword[..8]);
        result.Settings.Password.Preview.Should().BeNull();
        result.Settings.Password.Length.Should().Be(44);
        result.Settings.Password.Hint.Should().BeNull();
    }

    [Fact]
    public async Task SendTestAsync_UsernamePreviewShowsOnlyTheEnds()
    {
        var sut = CreateSender(Configured(ClosedPort()));

        var result = await sut.SendTestAsync(To);

        result.Settings.Username.Preview.Should().Be("AKIA…2345");
        JsonSerializer.Serialize(result).Should().NotContain(SmtpUsername);
    }

    [Fact]
    public async Task SendTestAsync_IamSecretKeyAsPassword_IsFlagged()
    {
        var iamSecret = new string('x', 40);
        var sut = CreateSender(Configured(ClosedPort(), password: iamSecret));

        var result = await sut.SendTestAsync(To);

        result.Settings.Password.Hint.Should().Contain("IAM secret access key");
    }

    [Fact]
    public async Task SendTestAsync_PlaceholderCredentials_AreFlagged()
    {
        var sut = CreateSender(Configured(ClosedPort(),
            username: "SET_IN_DO_CONSOLE_SES_SMTP_USERNAME",
            password: "SET_IN_DO_CONSOLE_SES_SMTP_PASSWORD"));

        var result = await sut.SendTestAsync(To);

        result.Settings.Username.Hint.Should().Contain("placeholder");
        result.Settings.Password.Hint.Should().Contain("placeholder");
    }

    [Fact]
    public async Task SendTestAsync_PastedWhitespace_IsFlagged()
    {
        var sut = CreateSender(Configured(ClosedPort(), username: SmtpUsername + " ", password: SmtpPassword + "\n"));

        var result = await sut.SendTestAsync(To);

        result.Settings.Username.HasSurroundingWhitespace.Should().BeTrue();
        result.Settings.Password.HasSurroundingWhitespace.Should().BeTrue();
        result.Settings.Password.Hint.Should().BeNull();  // length is judged on the trimmed value
    }

    #endregion

    #region SendAsync - unchanged behaviour

    [Fact]
    public async Task SendAsync_NotConfiguredInProduction_Throws()
    {
        var sut = CreateSender(new());

        var act = () => sut.SendAsync(To, "s", "t", "<p>h</p>");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("SMTP is not configured");
    }

    [Fact]
    public async Task SendAsync_NotConfiguredInDevelopment_LogsInsteadOfThrowing()
    {
        var sut = CreateSender(new(), environment: "Development");

        var act = () => sut.SendAsync(To, "s", "t", "<p>h</p>");

        await act.Should().NotThrowAsync();
    }

    #endregion
}
