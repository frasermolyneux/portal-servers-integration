using Microsoft.Extensions.Logging;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Clients;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1;

/// <summary>
/// Verifies configuration logging remains independent of stored game-type state.
/// </summary>
[Trait("Category", "Unit")]
public class SourceRconClientConfigurationTests
{
    /// <summary>
    /// Verifies configuration logs the supplied server and endpoint.
    /// </summary>
    [Fact]
    public void Configure_LogsGameTypeServerIdHostnameAndPort()
    {
        var loggerMock = new Mock<ILogger>();
        var client = new SourceRconClient(loggerMock.Object);
        var serverId = Guid.NewGuid();

        client.Configure(GameType.Insurgency, serverId, "insurgency.example.test", 27015, "password");

        Assert.Equal(
            $"[{serverId}] Configuring Source rcon client for Insurgency with endpoint insurgency.example.test:27015",
            GetLoggedMessages(loggerMock).Single());
    }

    /// <summary>
    /// Verifies repeated configuration logs each invocation's arguments.
    /// </summary>
    [Fact]
    public void Configure_LogsArgumentsFromEachInvocation()
    {
        var loggerMock = new Mock<ILogger>();
        var client = new SourceRconClient(loggerMock.Object);
        var firstServerId = Guid.NewGuid();
        var secondServerId = Guid.NewGuid();

        client.Configure(GameType.Insurgency, firstServerId, "insurgency.example.test", 27015, "first-password");
        client.Configure(GameType.Rust, secondServerId, "rust.example.test", 28015, "second-password");

        Assert.Equal(
            [
                $"[{firstServerId}] Configuring Source rcon client for Insurgency with endpoint insurgency.example.test:27015",
                $"[{secondServerId}] Configuring Source rcon client for Rust with endpoint rust.example.test:28015"
            ],
            GetLoggedMessages(loggerMock));
    }

    /// <summary>
    /// Verifies configuration preserves the null-logger failure contract.
    /// </summary>
    [Fact]
    public void Configure_ThrowsArgumentNullExceptionWhenLoggerIsNull()
    {
        var client = new SourceRconClient(null!);

        _ = Assert.Throws<ArgumentNullException>(() =>
            client.Configure(GameType.Insurgency, Guid.NewGuid(), "insurgency.example.test", 27015, "password"));
    }

    private static string[] GetLoggedMessages(Mock<ILogger> loggerMock)
    {
        return [.. loggerMock.Invocations
            .Select(invocation => invocation.Arguments[2]?.ToString())
            .OfType<string>()];
    }
}
