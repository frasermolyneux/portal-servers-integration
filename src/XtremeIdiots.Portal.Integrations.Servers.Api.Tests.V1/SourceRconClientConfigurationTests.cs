using Microsoft.Extensions.Logging;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Clients;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1;

[Trait("Category", "Unit")]
public class SourceRconClientConfigurationTests
{
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

    [Fact]
    public void Configure_ThrowsArgumentNullExceptionWhenLoggerIsNull()
    {
        var client = new SourceRconClient(null!);

        Assert.Throws<ArgumentNullException>(() =>
            client.Configure(GameType.Insurgency, Guid.NewGuid(), "insurgency.example.test", 27015, "password"));
    }

    private static string[] GetLoggedMessages(Mock<ILogger> loggerMock)
    {
        return loggerMock.Invocations
            .Select(invocation => invocation.Arguments[2]?.ToString())
            .OfType<string>()
            .ToArray();
    }
}
