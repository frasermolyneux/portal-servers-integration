using System.Net;
using System.Net.Http.Json;
using Moq;
using MX.Api.Abstractions;
using Newtonsoft.Json;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.Configurations;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.GameServers;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.IntegrationTests.V1;

[Trait("Category", "Integration")]
public class GameScopedRconEndpointsTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public GameScopedRconEndpointsTests(CustomWebApplicationFactory factory)
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
    public async Task Cod2Status_WhenServerNotFound_ReturnsNotFoundWithExpectedErrorCode()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServerNotFound(gameServerId);

        var response = await _client.GetAsync($"/v1.0/rcon/{gameServerId}/cod2/status");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("GAME_SERVER_NOT_FOUND", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cod4Status_WhenGameTypeDoesNotMatch_ReturnsBadRequestWithExpectedErrorCode()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServer(gameServerId, GameType.CallOfDuty2);

        var response = await _client.GetAsync($"/v1.0/rcon/{gameServerId}/cod4/status");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("OPERATION_NOT_SUPPORTED_FOR_GAME_TYPE", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cod5Status_WhenRconClientReturnsNullPlayers_ReturnsOkWithOperationFailedError()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServer(gameServerId, GameType.CallOfDuty5);
        SetupRconConfiguration(gameServerId, /*lang=json,strict*/ "{\"password\":\"secret\"}");

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient
            .Setup(x => x.GetPlayers())
            .Returns((List<IRconPlayer>)null!);

        _factory.MockRconClientFactory
            .Setup(x => x.CreateInstance(GameType.CallOfDuty5, gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var response = await _client.GetAsync($"/v1.0/rcon/{gameServerId}/cod5/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("RCON_OPERATION_FAILED", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cod2Tell_WhenTargetMissing_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod2/tell", new CoD4xTargetMessageRequestDto
        {
            Target = " ",
            Message = "hello"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cod2Tell_WhenTargetIsNotNumeric_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod2/tell", new CoD4xTargetMessageRequestDto
        {
            Target = "abc",
            Message = "hello"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cod2Tell_WhenMessageContainsUnsupportedCharacters_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod2/tell", new CoD4xTargetMessageRequestDto
        {
            Target = "3",
            Message = "hello;quit"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Cod2Tell_WhenValidRequest_ReturnsOkAndCallsRconClient()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServer(gameServerId, GameType.CallOfDuty2);
        SetupRconConfiguration(gameServerId, JsonConvert.SerializeObject(new { password = "secret" }));

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient
            .Setup(x => x.TellPlayer(5, "hello"))
            .ReturnsAsync("Tell command sent to player");

        _factory.MockRconClientFactory
            .Setup(x => x.CreateInstance(GameType.CallOfDuty2, gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod2/tell", new CoD4xTargetMessageRequestDto
        {
            Target = "5",
            Message = "  hello  "
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        mockRconClient.Verify(x => x.TellPlayer(5, "hello"), Times.Once);
    }

    [Fact]
    public async Task Cod2Set_WhenSuccessful_DoesNotIncludeResultInOperatorEventPayload()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServer(gameServerId, GameType.CallOfDuty2);
        SetupRconConfiguration(gameServerId, /*lang=json,strict*/ "{\"password\":\"secret\"}");

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient
            .Setup(x => x.SetDvar("sv_rconPassword", "super-secret-value"))
            .ReturnsAsync("sv_rconPassword was set to super-secret-value");

        _factory.MockRconClientFactory
            .Setup(x => x.CreateInstance(GameType.CallOfDuty2, gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        CreateGameServerEventDto? capturedEvent = null;
        var gameServerEventsApi = new Mock<IGameServersEventsApi>();
        gameServerEventsApi
            .Setup(x => x.CreateGameServerEvent(It.IsAny<CreateGameServerEventDto>(), It.IsAny<CancellationToken>()))
            .Callback<CreateGameServerEventDto, CancellationToken>((dto, _) => capturedEvent = dto)
            .ReturnsAsync(new ApiResult(HttpStatusCode.OK, new ApiResponse()));

        var versionedGameServerEventsApi = new Mock<IVersionedGameServersEventsApi>();
        versionedGameServerEventsApi
            .SetupGet(x => x.V1)
            .Returns(gameServerEventsApi.Object);

        _factory.MockRepositoryApiClient
            .SetupGet(x => x.GameServersEvents)
            .Returns(versionedGameServerEventsApi.Object);

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod2/set", new SetDvarRequest
        {
            DvarName = "sv_rconPassword",
            Value = "super-secret-value"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(capturedEvent);
        Assert.DoesNotContain("\"Result\"", capturedEvent!.EventData, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("super-secret-value", capturedEvent.EventData, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cod2Seta_WhenSuccessful_DoesNotIncludeResultInOperatorEventPayload()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServer(gameServerId, GameType.CallOfDuty2);
        SetupRconConfiguration(gameServerId, /*lang=json,strict*/ "{\"password\":\"secret\"}");

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient
            .Setup(x => x.SetaDvar("sv_rconPassword", "super-secret-value"))
            .ReturnsAsync("sv_rconPassword was archived to super-secret-value");

        _factory.MockRconClientFactory
            .Setup(x => x.CreateInstance(GameType.CallOfDuty2, gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        CreateGameServerEventDto? capturedEvent = null;
        var gameServerEventsApi = new Mock<IGameServersEventsApi>();
        gameServerEventsApi
            .Setup(x => x.CreateGameServerEvent(It.IsAny<CreateGameServerEventDto>(), It.IsAny<CancellationToken>()))
            .Callback<CreateGameServerEventDto, CancellationToken>((dto, _) => capturedEvent = dto)
            .ReturnsAsync(new ApiResult(HttpStatusCode.OK, new ApiResponse()));

        var versionedGameServerEventsApi = new Mock<IVersionedGameServersEventsApi>();
        versionedGameServerEventsApi
            .SetupGet(x => x.V1)
            .Returns(gameServerEventsApi.Object);

        _factory.MockRepositoryApiClient
            .SetupGet(x => x.GameServersEvents)
            .Returns(versionedGameServerEventsApi.Object);

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod2/seta", new SetDvarRequest
        {
            DvarName = "sv_rconPassword",
            Value = "super-secret-value"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(capturedEvent);
        Assert.DoesNotContain("\"Result\"", capturedEvent!.EventData, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("super-secret-value", capturedEvent.EventData, StringComparison.OrdinalIgnoreCase);
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
