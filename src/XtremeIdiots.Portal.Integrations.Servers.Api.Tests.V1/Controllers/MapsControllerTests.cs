using System.Net;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
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
using XtremeIdiots.Redirect.Api.Abstractions.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Controllers;

[Trait("Category", "Unit")]
public class MapsControllerTests
{
    private readonly Mock<ILogger<MapsController>> _mockLogger = new();
    private readonly Mock<IRepositoryApiClient> _mockRepositoryApiClient = new() { DefaultValue = DefaultValue.Mock };
    private readonly Mock<IGameServerFileTransportFactory> _mockFileTransportFactory = new();
    private readonly Mock<IRedirectApiClient> _mockRedirectApiClient = new();
    private readonly Mock<IRedirectDownloadClient> _mockRedirectDownloadClient = new();
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
        _mockRedirectApiClient.Object,
        _mockRedirectDownloadClient.Object);

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

    [Fact]
    public async Task PushServerMapToHost_RefreshesExistingFoldersFromCurrentVersionPinnedNestedFiles()
    {
        var gameServerId = Guid.NewGuid();
        var session = ConfigureMapTransfer(gameServerId,
            CreateRedirectFile("mp_custom.ff", 3), CreateRedirectFile("nested/assets.iwd", 2),
            CreateRedirectFile("readme?.txt", 1));
        session.Setup(x => x.GetListing("usermaps/mp_custom", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new FileTransportEntry("mp_custom.ff", "usermaps/mp_custom/mp_custom.ff", false, 3, null)]);
        session.Setup(x => x.GetListing("usermaps/mp_custom/nested", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new FileTransportEntry("assets.iwd", "usermaps/mp_custom/nested/assets.iwd", false, 2, null)]);
        var uploaded = new Dictionary<string, byte[]>();
        session.Setup(x => x.UploadStream(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(async (string path, Stream stream, CancellationToken cancellationToken) =>
            {
                using var copy = new MemoryStream();
                await stream.CopyToAsync(copy, cancellationToken);
                uploaded[path] = copy.ToArray();
            });
        _mockRedirectDownloadClient.Setup(x => x.OpenFile(It.IsAny<ContentEntry>(), 0, It.IsAny<CancellationToken>()))
            .Returns((ContentEntry file, long offset, CancellationToken cancellationToken) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(Enumerable.Repeat((byte)42, checked((int)file.Bytes)).ToArray())
                }));

        var result = await CreateController().PushServerMapToHost(gameServerId, "mp_custom");

        Assert.Equal(200, Assert.IsAssignableFrom<StatusCodeResult>(result).StatusCode);
        Assert.Equal(2, uploaded.Count);
        Assert.Equal(3, uploaded["usermaps/mp_custom/mp_custom.ff"].Length);
        Assert.Equal(2, uploaded["usermaps/mp_custom/nested/assets.iwd"].Length);
        _mockRedirectDownloadClient.Verify(x => x.OpenFile(
            It.Is<ContentEntry>(file => file.Version == "current-version"), 0, It.IsAny<CancellationToken>()), Times.Exactly(2));
        _mockRepositoryApiClient.Verify(x => x.Maps.V1.GetMap(It.IsAny<GameType>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("../outside.ff")]
    [InlineData("nested\\outside.ff")]
    [InlineData("folder./outside.ff")]
    public async Task PushServerMapToHost_RejectsUnsafeManifestBeforeAnyRemoteWrites(string relativePath)
    {
        var gameServerId = Guid.NewGuid();
        var session = ConfigureMapTransfer(gameServerId, CreateRedirectFile(relativePath, 3));

        var result = await CreateController().PushServerMapToHost(gameServerId, "mp_custom");

        Assert.Equal(502, Assert.IsAssignableFrom<StatusCodeResult>(result).StatusCode);
        session.Verify(x => x.CreateDirectory(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        session.Verify(x => x.UploadStream(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockRedirectDownloadClient.Verify(x => x.OpenFile(It.IsAny<ContentEntry>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PushServerMapToHost_ReportsFailedStreamInsteadOfSuccessfulFolderPresence()
    {
        var gameServerId = Guid.NewGuid();
        var session = ConfigureMapTransfer(gameServerId, CreateRedirectFile("mp_custom.ff", 3));
        _mockRedirectDownloadClient.Setup(x => x.OpenFile(It.IsAny<ContentEntry>(), 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });
        session.Setup(x => x.UploadStream(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Transfer stream failed."));

        var result = await CreateController().PushServerMapToHost(gameServerId, "mp_custom");

        Assert.Equal(502, Assert.IsAssignableFrom<StatusCodeResult>(result).StatusCode);
        session.Verify(x => x.UploadStream("usermaps/mp_custom/mp_custom.ff",
            It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task PushServerMapToHost_RejectsFileDirectoryCollisionsBeforeRemoteWrites(bool ancestorFirst, bool mixedCase)
    {
        var gameServerId = Guid.NewGuid();
        var ancestor = CreateRedirectFile("assets.ff", 3);
        var descendant = CreateRedirectFile(mixedCase ? "ASSETS.FF/nested.iwd" : "assets.ff/nested.iwd", 2);
        var session = ConfigureMapTransfer(gameServerId,
            ancestorFirst ? [ancestor, descendant] : [descendant, ancestor]);

        var result = await CreateController().PushServerMapToHost(gameServerId, "mp_custom");

        Assert.Equal(502, Assert.IsAssignableFrom<StatusCodeResult>(result).StatusCode);
        session.Verify(x => x.CreateDirectory(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        session.Verify(x => x.UploadStream(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockRedirectDownloadClient.Verify(x => x.OpenFile(It.IsAny<ContentEntry>(), It.IsAny<long>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false, 404)]
    [InlineData(true, 502)]
    public async Task PushServerMapToHost_DistinguishesMissingManifestFromMissingDownload(bool missingDownload, int expectedStatus)
    {
        var gameServerId = Guid.NewGuid();
        var session = ConfigureMapTransfer(gameServerId, CreateRedirectFile("map.ff", 3));
        if (missingDownload)
        {
            _mockRedirectDownloadClient.Setup(x => x.OpenFile(It.IsAny<ContentEntry>(), 0, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new HttpRequestException("The manifest file is missing.", null, HttpStatusCode.NotFound));
        }
        else
        {
            _mockRedirectApiClient.Setup(x => x.Manifest("cod4", "mp_custom", null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ApiResult<MapManifest>(HttpStatusCode.NotFound));
        }

        var result = await CreateController().PushServerMapToHost(gameServerId, "mp_custom");

        Assert.Equal(expectedStatus, Assert.IsAssignableFrom<StatusCodeResult>(result).StatusCode);
        session.Verify(x => x.UploadStream(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(3, true)]
    public async Task VerifyServerMaps_ChecksFilesAndSizesInsteadOfDirectoryExistence(long remoteBytes, bool complete)
    {
        var gameServerId = Guid.NewGuid();
        var session = ConfigureMapTransfer(gameServerId, CreateRedirectFile("mp_custom.ff", 3));
        session.Setup(x => x.GetListing("usermaps/mp_custom", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new FileTransportEntry("mp_custom.ff", "usermaps/mp_custom/mp_custom.ff", false, remoteBytes, null)]);

        var result = await CreateController().VerifyServerMaps(gameServerId, new MapVerificationRequestDto { MapNames = ["mp_custom"] });

        var response = Assert.IsType<ApiResponse<MapVerificationCollectionDto>>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal(complete, Assert.Single(response.Data!.Items).ExistsOnServer);
    }

    [Fact]
    public async Task VerifyServerMaps_RedirectUnavailableReturnsFailureInsteadOfMissingMaps()
    {
        var gameServerId = Guid.NewGuid();
        ConfigureMapTransfer(gameServerId, CreateRedirectFile("mp_custom.ff", 3));
        _mockRedirectApiClient.Setup(x => x.Manifest("cod4", "mp_custom", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<MapManifest>(HttpStatusCode.TooManyRequests,
                new ApiResponse<MapManifest>(new ApiError("RATE_LIMITED", "Retry later."))));

        var result = await CreateController().VerifyServerMaps(gameServerId, new MapVerificationRequestDto { MapNames = ["mp_custom"] });

        Assert.Equal(502, Assert.IsType<ObjectResult>(result).StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PushServerMapToHost_DistinguishesUpstreamTimeoutFromCallerCancellation(bool callerCancelled)
    {
        using var cancellation = new CancellationTokenSource();
        var gameServerId = Guid.NewGuid();
        var session = ConfigureMapTransfer(gameServerId, CreateRedirectFile("mp_custom.ff", 3));
        _mockRedirectApiClient.Setup(x => x.Manifest("cod4", "mp_custom", null, cancellation.Token))
            .Callback(() =>
            {
                if (callerCancelled)
                {
                    cancellation.Cancel();
                }
            })
            .ThrowsAsync(new TaskCanceledException("The upstream request timed out."));
        var controller = CreateController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestAborted = cancellation.Token }
        };

        if (callerCancelled)
        {
            await Assert.ThrowsAsync<TaskCanceledException>(() => controller.PushServerMapToHost(gameServerId, "mp_custom"));
        }
        else
        {
            var result = await controller.PushServerMapToHost(gameServerId, "mp_custom");
            Assert.Equal(502, Assert.IsAssignableFrom<StatusCodeResult>(result).StatusCode);
        }
        session.Verify(x => x.UploadStream(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task VerifyServerMaps_DistinguishesUpstreamTimeoutFromCallerCancellation(bool callerCancelled)
    {
        using var cancellation = new CancellationTokenSource();
        var gameServerId = Guid.NewGuid();
        ConfigureMapTransfer(gameServerId, CreateRedirectFile("mp_custom.ff", 3));
        _mockRedirectApiClient.Setup(x => x.Manifest("cod4", "mp_custom", null, cancellation.Token))
            .Callback(() =>
            {
                if (callerCancelled)
                {
                    cancellation.Cancel();
                }
            })
            .ThrowsAsync(new TaskCanceledException("The upstream request timed out."));
        var controller = CreateController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestAborted = cancellation.Token }
        };
        var request = new MapVerificationRequestDto { MapNames = ["mp_custom"] };

        if (callerCancelled)
        {
            await Assert.ThrowsAsync<TaskCanceledException>(() => controller.VerifyServerMaps(gameServerId, request));
        }
        else
        {
            var result = await controller.VerifyServerMaps(gameServerId, request);
            Assert.Equal(502, Assert.IsType<ObjectResult>(result).StatusCode);
        }
    }

    private Mock<IGameServerFileTransportSession> ConfigureMapTransfer(Guid gameServerId, params ContentEntry[] files)
    {
        _mockRepositoryApiClient.Setup(x => x.GameServers.V1.GetGameServer(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<GameServerDto>(HttpStatusCode.OK, new ApiResponse<GameServerDto>(CreateGameServerDto())));
        var session = new Mock<IGameServerFileTransportSession>();
        session.SetupGet(x => x.Transport).Returns(new ResolvedFileTransport(FileTransportType.Sftp, "sftp",
            new FileTransportCredentials("sftp-host", 22, "demo", "secret", "aa:bb")));
        session.Setup(x => x.DirectoryExists(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _mockFileTransportFactory.Setup(x => x.CreateSession(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<IGameServerFileTransportSession>(HttpStatusCode.OK,
                new ApiResponse<IGameServerFileTransportSession>(session.Object)));
        _mockRedirectApiClient.Setup(x => x.Manifest("cod4", "mp_custom", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<MapManifest>(HttpStatusCode.OK, new ApiResponse<MapManifest>(
                new MapManifest("cod4", "mp_custom", "redirect/cod4/usermaps/mp_custom", files, files.Sum(file => file.Bytes), "catalogue-version"))));
        return session;
    }

    private static ContentEntry CreateRedirectFile(string relativePath, long bytes) => new(
        $"redirect/cod4/usermaps/mp_custom/{relativePath}", relativePath.Split('/')[^1], false, bytes,
        DateTimeOffset.UtcNow, "current-version", "https://untrusted.invalid/not-a-download-source");

    [Fact]
    public async Task BuiltInMapOperations_DoNotLogMapName()
    {
        const string mapName = "mp_backlot";
        var gameServerId = Guid.NewGuid();
        var gameServer = CreateGameServerDto();

        _mockRepositoryApiClient
            .Setup(x => x.GameServers.V1.GetGameServer(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<GameServerDto>(HttpStatusCode.OK, new ApiResponse<GameServerDto>(gameServer)));

        var controller = CreateController();

        await controller.PushServerMapToHost(gameServerId, mapName);
        await controller.DeleteServerMapFromHost(gameServerId, mapName);

        var logMessages = _mockLogger.Invocations
            .Where(invocation => invocation.Method.Name == nameof(ILogger.Log))
            .Select(invocation => invocation.Arguments[2]?.ToString())
            .ToList();

        Assert.Equal(2, logMessages.Count);
        Assert.All(logMessages, message => Assert.DoesNotContain(mapName, message));
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
