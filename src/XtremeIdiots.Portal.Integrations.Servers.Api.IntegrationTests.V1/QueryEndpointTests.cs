using System.Net;
using MX.Api.Abstractions;
using Newtonsoft.Json;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.GameServers;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.IntegrationTests.V1;

[Trait("Category", "Integration")]
public class QueryEndpointTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public QueryEndpointTests(CustomWebApplicationFactory factory)
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

    private void SetupGameServerFound(Guid gameServerId)
    {
        var json = JsonConvert.SerializeObject(new
        {
            GameServerId = gameServerId,
            GameType = (int)GameType.CallOfDuty4,
            Hostname = "127.0.0.1",
            QueryPort = 28960
        });
        var gameServerDto = JsonConvert.DeserializeObject<GameServerDto>(json)!;

        var apiResponse = new ApiResponse<GameServerDto>(gameServerDto);
        var apiResult = new ApiResult<GameServerDto>(HttpStatusCode.OK, apiResponse);

        var mockGameServersApi = new Mock<IGameServersApi>();
        mockGameServersApi
            .Setup(x => x.GetGameServer(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResult);

        var mockVersionedGameServersApi = new Mock<IVersionedGameServersApi>();
        mockVersionedGameServersApi.Setup(x => x.V1).Returns(mockGameServersApi.Object);

        _factory.MockRepositoryApiClient
            .Setup(x => x.GameServers)
            .Returns(mockVersionedGameServersApi.Object);
    }

    private void SetupGameServerNotFound(Guid gameServerId)
    {
        var apiResult = new ApiResult<GameServerDto>(HttpStatusCode.NotFound, null);

        var mockGameServersApi = new Mock<IGameServersApi>();
        mockGameServersApi
            .Setup(x => x.GetGameServer(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResult);

        var mockVersionedGameServersApi = new Mock<IVersionedGameServersApi>();
        mockVersionedGameServersApi.Setup(x => x.V1).Returns(mockGameServersApi.Object);

        _factory.MockRepositoryApiClient
            .Setup(x => x.GameServers)
            .Returns(mockVersionedGameServersApi.Object);
    }

    [Fact]
    public async Task GetQueryStatus_WhenServerNotFound_ReturnsNotFound()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServerNotFound(gameServerId);

        var response = await _client.GetAsync($"/v1.0/query/{gameServerId}/status");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetQueryStatus_WhenServerFound_ReturnsResult()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServerFound(gameServerId);

        var mockQueryResponse = new Mock<IQueryResponse>();
        mockQueryResponse.Setup(x => x.ServerName).Returns("Test Server");
        mockQueryResponse.Setup(x => x.Map).Returns("mp_crash");
        mockQueryResponse.Setup(x => x.Mod).Returns("default");
        mockQueryResponse.Setup(x => x.MaxPlayers).Returns(32);
        mockQueryResponse.Setup(x => x.PlayerCount).Returns(5);
        mockQueryResponse.Setup(x => x.ServerParams).Returns(new Dictionary<string, string>());
        mockQueryResponse.Setup(x => x.Players).Returns(new List<IQueryPlayer>());

        var mockQueryClient = new Mock<IQueryClient>();
        mockQueryClient.Setup(x => x.GetServerStatus()).ReturnsAsync(mockQueryResponse.Object);

        _factory.MockQueryClientFactory
            .Setup(x => x.CreateInstance(GameType.CallOfDuty4, "127.0.0.1", 28960))
            .Returns(mockQueryClient.Object);

        var response = await _client.GetAsync($"/v1.0/query/{gameServerId}/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Test Server", content);
        Assert.Contains("mp_crash", content);
    }

    [Fact]
    public async Task GetQueryStatus_WhenQueryFails_ReturnsOkWithError()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServerFound(gameServerId);

        var mockQueryClient = new Mock<IQueryClient>();
        mockQueryClient.Setup(x => x.GetServerStatus()).ThrowsAsync(new Exception("Connection failed"));

        _factory.MockQueryClientFactory
            .Setup(x => x.CreateInstance(It.IsAny<GameType>(), It.IsAny<string>(), It.IsAny<int>()))
            .Returns(mockQueryClient.Object);

        var response = await _client.GetAsync($"/v1.0/query/{gameServerId}/status");

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("QUERY_CONNECTION_FAILED", content);
    }
}
