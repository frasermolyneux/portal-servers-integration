using System.Net;
using System.Net.Http.Json;
using MX.Api.Abstractions;
using Newtonsoft.Json;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Constants;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.Configurations;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.GameServers;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.IntegrationTests.V1;

[Trait("Category", "Integration")]
public class RconScreenshotEndpointTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RconScreenshotEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync()
    {
        _factory.ResetMocks();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task TakeScreenshot_WhenIdentifierIsInvalid_ReturnsBadRequestWithStableErrorCode()
    {
        var gameServerId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/screenshot", new TakeScreenshotRequestDto
        {
            PlayerIdentifier = "invalid id"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var responseBody = await response.Content.ReadFromJsonAsync<ApiResponse<string>>();
        Assert.Equal(ErrorCodes.INVALID_PLAYER_IDENTIFIER, responseBody?.Errors?.Single().Code);
    }

    [Fact]
    public async Task TakeScreenshot_WhenServerIsNotCoD4x_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServer(gameServerId, GameType.CallOfDuty4);
        SetupRconConfiguration(gameServerId, /*lang=json,strict*/ "{\"password\":\"secret\"}");

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient
            .Setup(x => x.TakeScreenshot("2310346615957836592", It.IsAny<CancellationToken>()))
            .ReturnsAsync("screenshot queued");

        _factory.MockRconClientFactory
            .Setup(x => x.CreateInstance(GameType.CallOfDuty4, gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/screenshot", new TakeScreenshotRequestDto
        {
            PlayerIdentifier = "2310346615957836592"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var responseBody = await response.Content.ReadFromJsonAsync<ApiResponse<string>>();
        Assert.Equal(ErrorCodes.OPERATION_NOT_SUPPORTED_FOR_GAME_TYPE, responseBody?.Errors?.Single().Code);
        mockRconClient.Verify(x => x.TakeScreenshot("2310346615957836592", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task TakeScreenshot_WhenServerNotFound_ReturnsNotFound()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServerNotFound(gameServerId);

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/screenshot", new TakeScreenshotRequestDto
        {
            PlayerIdentifier = "2310346615957836592"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task TakeScreenshot_WhenCoD4xAndValidIdentifier_ReturnsOkAndCallsRconClient()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServer(gameServerId, GameType.CallOfDuty4x);
        SetupRconConfiguration(gameServerId, /*lang=json,strict*/ "{\"password\":\"secret\"}");

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient
            .Setup(x => x.TakeScreenshot("2310346615957836592", It.IsAny<CancellationToken>()))
            .ReturnsAsync("screenshot queued");

        _factory.MockRconClientFactory
            .Setup(x => x.CreateInstance(GameType.CallOfDuty4x, gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/screenshot", new TakeScreenshotRequestDto
        {
            PlayerIdentifier = " 2310346615957836592 "
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        mockRconClient
            .Verify(x => x.TakeScreenshot("2310346615957836592", It.IsAny<CancellationToken>()), Times.Once);
    }

    private void SetupGameServer(Guid gameServerId, GameType gameType)
    {
        var json = JsonConvert.SerializeObject(new
        {
            GameServerId = gameServerId,
            GameType = (int)gameType,
            Hostname = "127.0.0.1",
            QueryPort = 28960
        });

        var gameServerDto = JsonConvert.DeserializeObject<GameServerDto>(json)!;
        var apiResponse = new ApiResponse<GameServerDto>(gameServerDto);
        var apiResult = new ApiResult<GameServerDto>(HttpStatusCode.OK, apiResponse);

        _factory.MockRepositoryApiClient
            .Setup(x => x.GameServers.V1.GetGameServer(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResult);
    }

    private void SetupGameServerNotFound(Guid gameServerId)
    {
        var apiResult = new ApiResult<GameServerDto>(HttpStatusCode.NotFound, null);

        _factory.MockRepositoryApiClient
            .Setup(x => x.GameServers.V1.GetGameServer(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResult);
    }

    private void SetupRconConfiguration(Guid gameServerId, string configuration)
    {
        var json = JsonConvert.SerializeObject(new
        {
            Namespace = "rcon",
            Configuration = configuration,
            LastModifiedUtc = DateTime.UtcNow,
        });

        var configurationDto = JsonConvert.DeserializeObject<ConfigurationDto>(json)!;
        var apiResponse = new ApiResponse<ConfigurationDto>(configurationDto);
        var apiResult = new ApiResult<ConfigurationDto>(HttpStatusCode.OK, apiResponse);

        _factory.MockRepositoryApiClient
            .Setup(x => x.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon", It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResult);
    }
}
