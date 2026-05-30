using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MX.Api.Abstractions;
using MX.Observability.ApplicationInsights.Auditing;
using Newtonsoft.Json;
using System.Net;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.Configurations;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.GameServers;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Controllers;

[Trait("Category", "Unit")]
public class RconControllerTests
{
    private readonly Mock<ILogger<RconController>> _mockLogger = new();
    private readonly Mock<IRepositoryApiClient> _mockRepositoryApiClient = new() { DefaultValue = DefaultValue.Mock };
    private readonly Mock<IRconClientFactory> _mockRconClientFactory = new();
    private readonly Mock<IAuditLogger> _mockAuditLogger = new();
    private readonly TelemetryClient _telemetryClient;

    public RconControllerTests()
    {
        var telemetryConfig = new TelemetryConfiguration { TelemetryChannel = new Mock<ITelemetryChannel>().Object };
        _telemetryClient = new TelemetryClient(telemetryConfig);
    }

    private RconController CreateController() => new(
        _mockLogger.Object,
        _mockRepositoryApiClient.Object,
        _mockRconClientFactory.Object,
        _telemetryClient,
        _mockAuditLogger.Object);

    private void SetupValidServerAndRconConfig(Guid gameServerId)
    {
        var gameServerJson = JsonConvert.SerializeObject(new { GameServerId = gameServerId, GameType = 4, Hostname = "127.0.0.1", QueryPort = 28960 });
        var gameServerDto = JsonConvert.DeserializeObject<GameServerDto>(gameServerJson)!;
        var gameServerResult = new ApiResult<GameServerDto>(HttpStatusCode.OK, new ApiResponse<GameServerDto>(gameServerDto));

        _mockRepositoryApiClient
            .Setup(x => x.GameServers.V1.GetGameServer(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameServerResult);

        var configJson = JsonConvert.SerializeObject(new { Configuration = "{\"password\":\"secret\"}" });
        var configDto = JsonConvert.DeserializeObject<ConfigurationDto>(configJson)!;
        var configResult = new ApiResult<ConfigurationDto>(HttpStatusCode.OK, new ApiResponse<ConfigurationDto>(configDto));

        _mockRepositoryApiClient
            .Setup(x => x.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon", It.IsAny<CancellationToken>()))
            .ReturnsAsync(configResult);
    }

    [Fact]
    public async Task GetDvar_WhenCoD4xUnknownCvarResponse_ReturnsNotFoundResult()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        SetupValidServerAndRconConfig(gameServerId);

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient.Setup(x => x.GetDvar("unknown_dvar")).ReturnsAsync("^1Bad command or cvar: unknown_dvar");

        _mockRconClientFactory
            .Setup(x => x.CreateInstance(It.IsAny<XtremeIdiots.Portal.Repository.Abstractions.Constants.V1.GameType>(), gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var controller = CreateController();

        // Act
        var result = await controller.GetDvar(gameServerId, "unknown_dvar");

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetDvar_WhenCoD4xValueContainsColorCodes_ReturnsNormalizedValue()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        SetupValidServerAndRconConfig(gameServerId);

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient.Setup(x => x.GetDvar("sv_hostname")).ReturnsAsync("\"sv_hostname\" is: \"^1Xtreme ^7Idiots\"");

        _mockRconClientFactory
            .Setup(x => x.CreateInstance(It.IsAny<XtremeIdiots.Portal.Repository.Abstractions.Constants.V1.GameType>(), gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var controller = CreateController();

        // Act
        var result = await ((IRconApi)controller).GetDvar(gameServerId, "sv_hostname");

        // Assert
        Assert.NotNull(result.Result?.Data);
        Assert.Equal("Xtreme Idiots", result.Result!.Data!.Value);
    }
}
