using System.Net;
using System.Net.Http.Json;
using MX.Api.Abstractions;
using Newtonsoft.Json;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.Configurations;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.GameServers;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.IntegrationTests.V1;

[Trait("Category", "Integration")]
public class RconResolvePlayerEndpointTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RconResolvePlayerEndpointTests(CustomWebApplicationFactory factory)
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
    public async Task ResolvePlayer_WhenRequestIsInvalid_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/resolve-player", new ResolvePlayerRequestDto
        {
            PlayerQuery = "   "
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResolvePlayer_WhenPlayerQueryIsMissing_ReturnsBadRequestWithStableErrorCode()
    {
        var gameServerId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/resolve-player", new { MaxSuggestions = 3 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("INVALID_REQUEST", content);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public async Task ResolvePlayer_WhenMaxSuggestionsOutOfRange_ReturnsBadRequestWithStableErrorCode(int maxSuggestions)
    {
        var gameServerId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/resolve-player", new ResolvePlayerRequestDto
        {
            PlayerQuery = "fraser",
            MaxSuggestions = maxSuggestions
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("INVALID_REQUEST", content);
    }

    [Fact]
    public async Task ResolvePlayer_WhenServerNotFound_ReturnsNotFound()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServerNotFound(gameServerId);

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/resolve-player", new ResolvePlayerRequestDto
        {
            PlayerQuery = "fraser"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ResolvePlayer_WhenUniqueMatchExists_ReturnsResolvedStatus()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServer(gameServerId, GameType.CallOfDuty4x);
        SetupRconConfiguration(gameServerId, /*lang=json,strict*/ "{\"password\":\"secret\"}");

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient.Setup(x => x.GetPlayers()).Returns([
            CreateRconPlayer(2, "^1Fraser", "g-1"),
            CreateRconPlayer(9, "AnotherPlayer", "g-2")
        ]);

        _factory.MockRconClientFactory
            .Setup(x => x.CreateInstance(GameType.CallOfDuty4x, gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/resolve-player", new ResolvePlayerRequestDto
        {
            PlayerQuery = "fraser"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"Resolved\"", content);
        Assert.Contains("\"slot\":2", content);
    }

    [Fact]
    public async Task ResolvePlayer_WhenNoPlayerMatches_ReturnsNotFoundStatusPayload()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServer(gameServerId, GameType.CallOfDuty4x);
        SetupRconConfiguration(gameServerId, /*lang=json,strict*/ "{\"password\":\"secret\"}");

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient.Setup(x => x.GetPlayers()).Returns([
            CreateRconPlayer(2, "Alpha", "g-1"),
            CreateRconPlayer(9, "Bravo", "g-2")
        ]);

        _factory.MockRconClientFactory
            .Setup(x => x.CreateInstance(GameType.CallOfDuty4x, gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/resolve-player", new ResolvePlayerRequestDto
        {
            PlayerQuery = "charlie"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"NotFound\"", content);
    }

    [Fact]
    public async Task ResolvePlayer_WhenAmbiguousMatchesExist_ReturnsAmbiguousStatusWithSuggestions()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServer(gameServerId, GameType.CallOfDuty4x);
        SetupRconConfiguration(gameServerId, /*lang=json,strict*/ "{\"password\":\"secret\"}");

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient.Setup(x => x.GetPlayers()).Returns([
            CreateRconPlayer(1, "Frase", "g-1"),
            CreateRconPlayer(2, "Frazz", "g-2"),
            CreateRconPlayer(3, "Other", "g-3")
        ]);

        _factory.MockRconClientFactory
            .Setup(x => x.CreateInstance(GameType.CallOfDuty4x, gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/resolve-player", new ResolvePlayerRequestDto
        {
            PlayerQuery = "fra",
            MaxSuggestions = 2
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":\"Ambiguous\"", content);
        Assert.Contains("\"suggestions\":[", content);
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

    private static IRconPlayer CreateRconPlayer(int slot, string name, string guid)
    {
        var mockPlayer = new Mock<IRconPlayer>();
        mockPlayer.SetupProperty(x => x.Num, slot);
        mockPlayer.SetupProperty(x => x.Name, name);
        mockPlayer.SetupProperty(x => x.Guid, guid);
        mockPlayer.SetupProperty(x => x.IpAddress, "127.0.0.1");
        mockPlayer.SetupProperty(x => x.Ping, 40);
        mockPlayer.SetupProperty(x => x.Rate, 25000);

        return mockPlayer.Object;
    }
}
