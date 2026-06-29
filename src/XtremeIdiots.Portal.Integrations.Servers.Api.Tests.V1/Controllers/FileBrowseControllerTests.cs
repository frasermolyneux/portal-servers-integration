using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MX.Api.Abstractions;
using System.Net;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Ftp;
using XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Controllers;

[Trait("Category", "Unit")]
public class FileBrowseControllerTests
{
    private readonly Mock<ILogger<FileBrowseController>> _mockLogger = new();
    private readonly Mock<IGameServerFileTransportFactory> _mockFileTransportFactory = new();
    private readonly TelemetryClient _telemetryClient;
    private readonly IMemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions());

    public FileBrowseControllerTests()
    {
        var telemetryConfig = new TelemetryConfiguration { TelemetryChannel = new Mock<ITelemetryChannel>().Object };
        _telemetryClient = new TelemetryClient(telemetryConfig);
    }

    private FileBrowseController CreateController() => new(
        _mockLogger.Object,
        _mockFileTransportFactory.Object,
        _telemetryClient,
        _memoryCache);

    [Fact]
    public async Task BrowseDirectory_WhenGameServerNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        var notFoundResult = new ApiResult<IGameServerFileTransportSession>(HttpStatusCode.NotFound, null);
        _mockFileTransportFactory
            .Setup(x => x.CreateSession(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notFoundResult);

        var controller = CreateController();

        // Act
        var result = await controller.BrowseDirectory(gameServerId);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, objectResult.StatusCode);
    }

    [Fact]
    public async Task BrowseDirectory_WhenCredentialsMissing_ReturnsBadRequest()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        var missingResult = new ApiResult<IGameServerFileTransportSession>(HttpStatusCode.BadRequest, new ApiResponse<IGameServerFileTransportSession>(new ApiError("FTP_CREDENTIALS_MISSING", "missing")));

        _mockFileTransportFactory
            .Setup(x => x.CreateSession(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(missingResult);

        var controller = CreateController();

        // Act
        var result = await controller.BrowseDirectory(gameServerId);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, objectResult.StatusCode);
    }

    [Fact]
    public async Task BrowseDirectory_WhenPathContainsTraversal_ReturnsBadRequest()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        var controller = CreateController();

        // Act
        var result = await controller.BrowseDirectory(gameServerId, "/../etc");

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(400, objectResult.StatusCode);
    }

    [Fact]
    public async Task BrowseDirectory_WhenNullPath_DefaultsToRoot()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        var notFoundResult = new ApiResult<IGameServerFileTransportSession>(HttpStatusCode.NotFound, null);

        _mockFileTransportFactory
            .Setup(x => x.CreateSession(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notFoundResult);

        var controller = CreateController();

        // Act - null path should proceed to server lookup (which returns 404)
        var result = await controller.BrowseDirectory(gameServerId, null);

        // Assert - confirms path normalization didn't cause an error
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, objectResult.StatusCode);

        _mockFileTransportFactory.Verify(
            x => x.CreateSession(gameServerId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BrowseDirectory_WhenSessionReturnsItems_UsesListingAndReturnsOk()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        var session = new TestFileTransportSession(new[]
        {
            new FileTransportEntry("configs", "/configs", true, null, DateTime.UtcNow),
            new FileTransportEntry("server.cfg", "/server.cfg", false, 123, DateTime.UtcNow),
        });

        var okResult = new ApiResult<IGameServerFileTransportSession>(HttpStatusCode.OK, new ApiResponse<IGameServerFileTransportSession>(session));
        _mockFileTransportFactory
            .Setup(x => x.CreateSession(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(okResult);

        var controller = CreateController();

        // Act
        var result = await controller.BrowseDirectory(gameServerId, "/");

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
    }

    [Fact]
    public async Task BrowseDirectory_WhenSessionConnectionFails_ReturnsConnectionError()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        var failedResult = new ApiResult<IGameServerFileTransportSession>(
            HttpStatusCode.InternalServerError,
            new ApiResponse<IGameServerFileTransportSession>(new ApiError("FTP_CONNECTION_FAILED", "Connection failed")));

        _mockFileTransportFactory
            .Setup(x => x.CreateSession(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedResult);

        var controller = CreateController();

        // Act
        var result = await controller.BrowseDirectory(gameServerId, "/");

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        var response = Assert.IsType<ApiResponse<FtpDirectoryListingDto>>(objectResult.Value);
        Assert.Equal("FTP_CONNECTION_FAILED", response.Errors?.FirstOrDefault()?.Code);
    }

    private sealed class TestFileTransportSession(IReadOnlyList<FileTransportEntry> entries) : IGameServerFileTransportSession
    {
        public ResolvedFileTransport Transport { get; } = new(FileTransportType.Sftp, "sftp", new FileTransportCredentials("localhost", 22, "user", "pwd"));

        public Task<IReadOnlyList<FileTransportEntry>> GetListing(string path, CancellationToken cancellationToken = default) => Task.FromResult(entries);
        public Task<bool> FileExists(string path, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<byte[]> DownloadBytes(string path, CancellationToken cancellationToken = default) => Task.FromResult(Array.Empty<byte>());
        public Task UploadBytes(string path, byte[] content, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UploadStream(string path, Stream content, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteFile(string path, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> DirectoryExists(string path, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task CreateDirectory(string path, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteDirectory(string path, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}