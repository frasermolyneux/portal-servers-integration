using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.Extensions.Logging;
using MX.Api.Abstractions;
using MX.Observability.ApplicationInsights.Auditing;
using Newtonsoft.Json;
using System.Net;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;
using XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Constants;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
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

    private void SetupValidServerAndRconConfig(Guid gameServerId, GameType gameType = GameType.CallOfDuty4x)
    {
        var gameServerJson = JsonConvert.SerializeObject(new { GameServerId = gameServerId, GameType = (int)gameType, Hostname = "127.0.0.1", QueryPort = 28960 });
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
        var result = await ((IRconApi)controller).GetDvar(gameServerId, "unknown_dvar");

        // Assert
        Assert.True(result.IsNotFound);
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

    [Fact]
    public async Task GetDvar_WhenCoD4xValueContainsAlphabeticColorCodes_ReturnsNormalizedValue()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        SetupValidServerAndRconConfig(gameServerId);

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient.Setup(x => x.GetDvar("g_motd")).ReturnsAsync("\"g_motd\" is: \"^aWelcome ^ZHome\"");

        _mockRconClientFactory
            .Setup(x => x.CreateInstance(It.IsAny<XtremeIdiots.Portal.Repository.Abstractions.Constants.V1.GameType>(), gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var controller = CreateController();

        // Act
        var result = await ((IRconApi)controller).GetDvar(gameServerId, "g_motd");

        // Assert
        Assert.NotNull(result.Result?.Data);
        Assert.Equal("Welcome Home", result.Result!.Data!.Value);
    }

    [Fact]
    public async Task TakeScreenshot_WhenValidRequestForCoD4x_ReturnsSuccessAndCallsRcon()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        SetupValidServerAndRconConfig(gameServerId, GameType.CallOfDuty4x);

        var trimmedIdentifier = "2310346615957836592";
        var mockRconClient = new Mock<IRconClient>();
        mockRconClient.Setup(x => x.TakeScreenshot(trimmedIdentifier, It.IsAny<CancellationToken>())).ReturnsAsync("ok");

        _mockRconClientFactory
            .Setup(x => x.CreateInstance(It.IsAny<GameType>(), gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var controller = CreateController();

        // Act
        var result = await ((IRconApi)controller).TakeScreenshot(gameServerId, new TakeScreenshotRequestDto { PlayerIdentifier = $"  {trimmedIdentifier}  " });

        // Assert
        Assert.True(result.IsSuccess);
        mockRconClient.Verify(x => x.TakeScreenshot(trimmedIdentifier, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TakeScreenshot_WhenIdentifierIsInvalid_ReturnsBadRequestWithStableErrorCode()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await ((IRconApi)controller).TakeScreenshot(Guid.NewGuid(), new TakeScreenshotRequestDto { PlayerIdentifier = "abc;drop" });

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(ErrorCodes.INVALID_PLAYER_IDENTIFIER, result.Result?.Errors?.Single().Code);
    }

    [Fact]
    public async Task TakeScreenshot_WhenServerIsNotCoD4x_ReturnsBadRequestWithStableErrorCode()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        SetupValidServerAndRconConfig(gameServerId, GameType.CallOfDuty4);
        var controller = CreateController();

        // Act
        var result = await ((IRconApi)controller).TakeScreenshot(gameServerId, new TakeScreenshotRequestDto { PlayerIdentifier = "2310346615957836592" });

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(ErrorCodes.OPERATION_NOT_SUPPORTED_FOR_GAME_TYPE, result.Result?.Errors?.Single().Code);
    }
}
