using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MX.Api.Abstractions;
using Newtonsoft.Json;
using System.Net;
using XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.Configurations;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.GameServers;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Controllers;

[Trait("Category", "Unit")]
public class FtpBrowseControllerTests
{
    private readonly Mock<ILogger<FtpBrowseController>> _mockLogger = new();
    private readonly Mock<IRepositoryApiClient> _mockRepositoryApiClient = new() { DefaultValue = DefaultValue.Mock };
    private readonly TelemetryClient _telemetryClient;
    private readonly IConfiguration _configuration;
    private readonly IMemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions());

    public FtpBrowseControllerTests()
    {
        var telemetryConfig = new TelemetryConfiguration { TelemetryChannel = new Mock<ITelemetryChannel>().Object };
        _telemetryClient = new TelemetryClient(telemetryConfig);
        _configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
    }

    private FtpBrowseController CreateController() => new(
        _mockLogger.Object,
        _mockRepositoryApiClient.Object,
        _telemetryClient,
        _configuration,
        _memoryCache);

    [Fact]
    public async Task BrowseDirectory_WhenGameServerNotFound_ReturnsNotFoundResult()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        var notFoundResult = new ApiResult<GameServerDto>(HttpStatusCode.NotFound, null);

        _mockRepositoryApiClient
            .Setup(x => x.GameServers.V1.GetGameServer(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notFoundResult);

        var controller = CreateController();

        // Act
        var result = await controller.BrowseDirectory(gameServerId);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, objectResult.StatusCode);
    }

    [Fact]
    public async Task BrowseDirectory_WhenFtpCredentialsMissing_ReturnsBadRequest()
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

        // Return an empty configuration (no FTP config)
        var emptyConfigResult = new ApiResult<ConfigurationDto>(HttpStatusCode.NotFound, null);
        _mockRepositoryApiClient
            .Setup(x => x.GameServerConfigurations.V1.GetConfiguration(gameServerId, "ftp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(emptyConfigResult);

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
        var notFoundResult = new ApiResult<GameServerDto>(HttpStatusCode.NotFound, null);

        _mockRepositoryApiClient
            .Setup(x => x.GameServers.V1.GetGameServer(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notFoundResult);

        var controller = CreateController();

        // Act - null path should proceed to server lookup (which returns 404)
        var result = await controller.BrowseDirectory(gameServerId, null);

        // Assert - confirms path normalization didn't cause an error
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(404, objectResult.StatusCode);

        _mockRepositoryApiClient.Verify(
            x => x.GameServers.V1.GetGameServer(gameServerId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
