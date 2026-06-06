using System.Net;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MX.Api.Abstractions;
using Newtonsoft.Json;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Maps;
using XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Constants;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.GameServers;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Controllers;

[Trait("Category", "Unit")]
public class MapsControllerTests
{
    private readonly Mock<ILogger<MapsController>> _mockLogger = new();
    private readonly Mock<IRepositoryApiClient> _mockRepositoryApiClient = new() { DefaultValue = DefaultValue.Mock };
    private readonly Mock<IGameServerFileTransportFactory> _mockFileTransportFactory = new();
    private readonly Mock<IHttpClientFactory> _mockHttpClientFactory = new();
    private readonly TelemetryClient _telemetryClient;

    public MapsControllerTests()
    {
        var telemetryConfig = new TelemetryConfiguration { TelemetryChannel = new Mock<ITelemetryChannel>().Object };
        _telemetryClient = new TelemetryClient(telemetryConfig);
    }

    private MapsController CreateController() => new(
        _mockLogger.Object,
        _mockRepositoryApiClient.Object,
        _mockFileTransportFactory.Object,
        _telemetryClient,
        _mockHttpClientFactory.Object);

    [Fact]
    public async Task GetLoadedServerMapsFromHost_WhenSessionConnectionFails_ReturnsConnectionError()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        var gameServer = CreateGameServerDto();
        var failedResult = new ApiResult<IGameServerFileTransportSession>(
            HttpStatusCode.InternalServerError,
            new ApiResponse<IGameServerFileTransportSession>(new ApiError("FTP_CONNECTION_FAILED", "Connection failed")));

        _mockRepositoryApiClient
            .Setup(x => x.GameServers.V1.GetGameServer(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<GameServerDto>(HttpStatusCode.OK, new ApiResponse<GameServerDto>(gameServer)));

        _mockFileTransportFactory
            .Setup(x => x.CreateSession(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedResult);

        var controller = CreateController();

        // Act
        var result = await controller.GetLoadedServerMapsFromHost(gameServerId);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        var response = Assert.IsType<ApiResponse<ServerMapsCollectionDto>>(objectResult.Value);
        Assert.Equal("FTP_CONNECTION_FAILED", response.Errors?.FirstOrDefault()?.Code);
    }

    [Fact]
    public async Task GetLoadedServerMapsFromHost_WhenMapsRootPathConfigured_ListsFromRootUsermaps()
    {
        var gameServerId = Guid.NewGuid();
        var gameServer = CreateGameServerDto();

        _mockRepositoryApiClient
            .Setup(x => x.GameServers.V1.GetGameServer(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<GameServerDto>(HttpStatusCode.OK, new ApiResponse<GameServerDto>(gameServer)));

        var session = new Mock<IGameServerFileTransportSession>();
        session.SetupGet(x => x.Transport)
            .Returns(new ResolvedFileTransport(
                FileTransportType.Sftp,
                "sftp",
                new FileTransportCredentials("sftp-host", 22, "demo", "secret", "aa:bb", "/customer-a/server1")));
        session.Setup(x => x.GetListing("/customer-a/server1/usermaps", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _mockFileTransportFactory
            .Setup(x => x.CreateSession(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<IGameServerFileTransportSession>(HttpStatusCode.OK, new ApiResponse<IGameServerFileTransportSession>(session.Object)));

        var controller = CreateController();

        var result = await controller.GetLoadedServerMapsFromHost(gameServerId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        session.Verify(x => x.GetListing("/customer-a/server1/usermaps", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetLoadedServerMapsFromHost_WhenMapsRootPathAlreadyEndsWithUsermaps_DoesNotAppendTwice()
    {
        var gameServerId = Guid.NewGuid();
        var gameServer = CreateGameServerDto();

        _mockRepositoryApiClient
            .Setup(x => x.GameServers.V1.GetGameServer(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<GameServerDto>(HttpStatusCode.OK, new ApiResponse<GameServerDto>(gameServer)));

        var session = new Mock<IGameServerFileTransportSession>();
        session.SetupGet(x => x.Transport)
            .Returns(new ResolvedFileTransport(
                FileTransportType.Sftp,
                "sftp",
                new FileTransportCredentials("sftp-host", 22, "demo", "secret", "aa:bb", "/customer-a/server1/usermaps")));
        session.Setup(x => x.GetListing("/customer-a/server1/usermaps", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _mockFileTransportFactory
            .Setup(x => x.CreateSession(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<IGameServerFileTransportSession>(HttpStatusCode.OK, new ApiResponse<IGameServerFileTransportSession>(session.Object)));

        var controller = CreateController();

        var result = await controller.GetLoadedServerMapsFromHost(gameServerId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        session.Verify(x => x.GetListing("/customer-a/server1/usermaps", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetLoadedServerMapsFromHost_WhenMapsRootPathContainsTraversal_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();
        var gameServer = CreateGameServerDto();

        _mockRepositoryApiClient
            .Setup(x => x.GameServers.V1.GetGameServer(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<GameServerDto>(HttpStatusCode.OK, new ApiResponse<GameServerDto>(gameServer)));

        var session = new Mock<IGameServerFileTransportSession>();
        session.SetupGet(x => x.Transport)
            .Returns(new ResolvedFileTransport(
                FileTransportType.Sftp,
                "sftp",
                new FileTransportCredentials("sftp-host", 22, "demo", "secret", "aa:bb", "/customer-a/../server1")));

        _mockFileTransportFactory
            .Setup(x => x.CreateSession(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<IGameServerFileTransportSession>(HttpStatusCode.OK, new ApiResponse<IGameServerFileTransportSession>(session.Object)));

        var controller = CreateController();

        var result = await controller.GetLoadedServerMapsFromHost(gameServerId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, objectResult.StatusCode);
        var response = Assert.IsType<ApiResponse<ServerMapsCollectionDto>>(objectResult.Value);
        Assert.Equal(ErrorCodes.INVALID_REQUEST, response.Errors?.FirstOrDefault()?.Code);
        session.Verify(x => x.GetListing(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PushServerMapToHost_WhenMapNameContainsPathSeparators_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();
        var controller = CreateController();

        var result = await controller.PushServerMapToHost(gameServerId, "../bad-map");

        var statusCodeResult = Assert.IsAssignableFrom<StatusCodeResult>(result);
        Assert.Equal(400, statusCodeResult.StatusCode);

        _mockRepositoryApiClient.Verify(x => x.GameServers.V1.GetGameServer(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PushServerMapToHost_WhenMapNameIsDot_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();
        var controller = CreateController();

        var result = await controller.PushServerMapToHost(gameServerId, ".");

        var statusCodeResult = Assert.IsAssignableFrom<StatusCodeResult>(result);
        Assert.Equal(400, statusCodeResult.StatusCode);

        _mockRepositoryApiClient.Verify(x => x.GameServers.V1.GetGameServer(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static GameServerDto CreateGameServerDto()
    {
        var json = JsonConvert.SerializeObject(new
        {
            GameServerId = Guid.NewGuid(),
            Title = "Server",
            GameType = GameType.CallOfDuty4,
            Hostname = "localhost",
            QueryPort = 28960,
            FileTransportEnabled = true,
            FileTransportType = FileTransportType.Ftp,
            FtpEnabled = true,
            RconEnabled = true,
        });

        return JsonConvert.DeserializeObject<GameServerDto>(json)!;
    }
}
