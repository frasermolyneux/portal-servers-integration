using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MX.Api.Abstractions;
using Newtonsoft.Json;
using System.Net;
using XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.GameServers;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Controllers;

[Trait("Category", "Unit")]
public class QueryControllerTests
{
    private readonly Mock<ILogger<QueryController>> _mockLogger = new();
    private readonly Mock<IRepositoryApiClient> _mockRepositoryApiClient = new() { DefaultValue = DefaultValue.Mock };
    private readonly Mock<IQueryClientFactory> _mockQueryClientFactory = new();
    private readonly TelemetryClient _telemetryClient;
    private readonly IMemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions());

    public QueryControllerTests()
    {
        var telemetryConfig = new TelemetryConfiguration { TelemetryChannel = new Mock<ITelemetryChannel>().Object };
        _telemetryClient = new TelemetryClient(telemetryConfig);
    }

    private QueryController CreateController() => new(
        _mockLogger.Object,
        _mockRepositoryApiClient.Object,
        _mockQueryClientFactory.Object,
        _telemetryClient,
        _memoryCache);

    [Fact]
    public async Task GetServerStatus_WhenGameServerNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        var notFoundResult = new ApiResult<GameServerDto>(HttpStatusCode.NotFound, null);

        _mockRepositoryApiClient
            .Setup(x => x.GameServers.V1.GetGameServer(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notFoundResult);

        var controller = CreateController();

        // Act
        var result = await controller.GetServerStatus(gameServerId);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetServerStatus_WhenQuerySucceeds_ReturnsOkResult()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        var json = JsonConvert.SerializeObject(new { GameServerId = gameServerId, GameType = 4, Hostname = "127.0.0.1", QueryPort = 28960 });
        var gameServerDto = JsonConvert.DeserializeObject<GameServerDto>(json)!;
        var apiResponse = new ApiResponse<GameServerDto>(gameServerDto);
        var apiResult = new ApiResult<GameServerDto>(HttpStatusCode.OK, apiResponse);

        _mockRepositoryApiClient
            .Setup(x => x.GameServers.V1.GetGameServer(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResult);

        var mockQueryResponse = new Mock<IQueryResponse>();
        mockQueryResponse.Setup(x => x.ServerName).Returns("Test Server");
        mockQueryResponse.Setup(x => x.Map).Returns("mp_test");
        mockQueryResponse.Setup(x => x.Mod).Returns("default");
        mockQueryResponse.Setup(x => x.MaxPlayers).Returns(24);
        mockQueryResponse.Setup(x => x.PlayerCount).Returns(10);
        mockQueryResponse.Setup(x => x.ServerParams).Returns(new Dictionary<string, string>());
        mockQueryResponse.Setup(x => x.Players).Returns(new List<IQueryPlayer>());

        var mockQueryClient = new Mock<IQueryClient>();
        mockQueryClient.Setup(x => x.GetServerStatus()).ReturnsAsync(mockQueryResponse.Object);

        _mockQueryClientFactory
            .Setup(x => x.CreateInstance(It.IsAny<XtremeIdiots.Portal.Repository.Abstractions.Constants.V1.GameType>(), "127.0.0.1", 28960))
            .Returns(mockQueryClient.Object);

        var controller = CreateController();

        // Act
        var result = await controller.GetServerStatus(gameServerId);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetServerStatus_WhenQueryThrows_ReturnsErrorResult()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        var json = JsonConvert.SerializeObject(new { GameServerId = gameServerId, GameType = 4, Hostname = "127.0.0.1", QueryPort = 28960 });
        var gameServerDto = JsonConvert.DeserializeObject<GameServerDto>(json)!;
        var apiResponse = new ApiResponse<GameServerDto>(gameServerDto);
        var apiResult = new ApiResult<GameServerDto>(HttpStatusCode.OK, apiResponse);

        _mockRepositoryApiClient
            .Setup(x => x.GameServers.V1.GetGameServer(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResult);

        var mockQueryClient = new Mock<IQueryClient>();
        mockQueryClient.Setup(x => x.GetServerStatus()).ThrowsAsync(new Exception("Connection failed"));

        _mockQueryClientFactory
            .Setup(x => x.CreateInstance(It.IsAny<XtremeIdiots.Portal.Repository.Abstractions.Constants.V1.GameType>(), "127.0.0.1", 28960))
            .Returns(mockQueryClient.Object);

        var controller = CreateController();

        // Act
        var result = await controller.GetServerStatus(gameServerId);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetServerStatus_WhenCached_DoesNotCallQueryClient()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        var json = JsonConvert.SerializeObject(new { GameServerId = gameServerId, GameType = 4, Hostname = "127.0.0.1", QueryPort = 28960 });
        var gameServerDto = JsonConvert.DeserializeObject<GameServerDto>(json)!;
        var apiResponse = new ApiResponse<GameServerDto>(gameServerDto);
        var apiResult = new ApiResult<GameServerDto>(HttpStatusCode.OK, apiResponse);

        _mockRepositoryApiClient
            .Setup(x => x.GameServers.V1.GetGameServer(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResult);

        var mockQueryResponse = new Mock<IQueryResponse>();
        mockQueryResponse.Setup(x => x.ServerName).Returns("Cached Server");
        mockQueryResponse.Setup(x => x.Map).Returns("mp_cached");
        mockQueryResponse.Setup(x => x.Mod).Returns("default");
        mockQueryResponse.Setup(x => x.MaxPlayers).Returns(24);
        mockQueryResponse.Setup(x => x.PlayerCount).Returns(5);
        mockQueryResponse.Setup(x => x.ServerParams).Returns(new Dictionary<string, string>());
        mockQueryResponse.Setup(x => x.Players).Returns(new List<IQueryPlayer>());

        _memoryCache.Set($"{gameServerId}-query-status", (IQueryResponse)mockQueryResponse.Object);

        var mockQueryClient = new Mock<IQueryClient>();

        _mockQueryClientFactory
            .Setup(x => x.CreateInstance(It.IsAny<XtremeIdiots.Portal.Repository.Abstractions.Constants.V1.GameType>(), "127.0.0.1", 28960))
            .Returns(mockQueryClient.Object);

        var controller = CreateController();

        // Act
        var result = await controller.GetServerStatus(gameServerId);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        mockQueryClient.Verify(x => x.GetServerStatus(), Times.Never);
    }
}
