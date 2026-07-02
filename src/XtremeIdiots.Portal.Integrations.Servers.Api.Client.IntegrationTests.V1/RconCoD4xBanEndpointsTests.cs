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
public class RconCoD4xBanEndpointsTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private const string DefaultCoD4xBanReason = "Banned by XtremeIdiots Portal";

    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RconCoD4xBanEndpointsTests(CustomWebApplicationFactory factory)
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
    public async Task CoD4xPermBan_WhenIdentifierIsInvalid_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/permban", new CoD4xPermBanRequestDto
        {
            PlayerIdentifier = "invalid id"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CoD4xPermBan_WhenServerNotFound_ReturnsNotFound()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServerNotFound(gameServerId);

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/permban", new CoD4xPermBanRequestDto
        {
            PlayerIdentifier = "2310346615957836592"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CoD4xPermBan_WhenServerIsNotCoD4x_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServer(gameServerId, GameType.CallOfDuty4);

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/permban", new CoD4xPermBanRequestDto
        {
            PlayerIdentifier = "2310346615957836592"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CoD4xPermBan_WhenRequestIsValid_ReturnsOkAndCallsRconClient()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServer(gameServerId, GameType.CallOfDuty4x);
        SetupRconConfiguration(gameServerId, /*lang=json,strict*/ "{\"password\":\"secret\"}");

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient.As<ICallOfDuty4xRconClient>()
            .Setup(x => x.BanPlayerByPlayerIdentifier("2310346615957836592", DefaultCoD4xBanReason))
            .ReturnsAsync("Banrecord added for id: 2310346615957836592");

        _factory.MockRconClientFactory
            .Setup(x => x.CreateInstance(GameType.CallOfDuty4x, gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/permban", new CoD4xPermBanRequestDto
        {
            PlayerIdentifier = " 2310346615957836592 "
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"outcome\":\"AddedOffline\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"isSuccess\":true", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"playerIdentifier\":\"2310346615957836592\"", content, StringComparison.OrdinalIgnoreCase);
        mockRconClient.As<ICallOfDuty4xRconClient>()
            .Verify(x => x.BanPlayerByPlayerIdentifier("2310346615957836592", DefaultCoD4xBanReason), Times.Once);
    }

    [Fact]
    public async Task CoD4xTempBan_WhenServerNotFound_ReturnsNotFound()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServerNotFound(gameServerId);

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/tempban", new CoD4xTempBanRequestDto
        {
            PlayerIdentifier = "2310346615957836592",
            DurationMinutes = 15
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CoD4xTempBan_WhenIdentifierIsInvalid_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/tempban", new CoD4xTempBanRequestDto
        {
            PlayerIdentifier = "invalid id",
            DurationMinutes = 15
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CoD4xTempBan_WhenServerIsNotCoD4x_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServer(gameServerId, GameType.CallOfDuty4);

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/tempban", new CoD4xTempBanRequestDto
        {
            PlayerIdentifier = "2310346615957836592",
            DurationMinutes = 15
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CoD4xTempBan_WhenRequestIsValid_ReturnsOkAndCallsRconClient()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServer(gameServerId, GameType.CallOfDuty4x);
        SetupRconConfiguration(gameServerId, /*lang=json,strict*/ "{\"password\":\"secret\"}");

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient.As<ICallOfDuty4xRconClient>()
            .Setup(x => x.TempBanPlayerByPlayerIdentifier("2310346615957836592", 15, DefaultCoD4xBanReason))
            .ReturnsAsync("Banrecord added for player: ^1Fraser id: 2310346615957836592");

        _factory.MockRconClientFactory
            .Setup(x => x.CreateInstance(GameType.CallOfDuty4x, gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/tempban", new CoD4xTempBanRequestDto
        {
            PlayerIdentifier = "2310346615957836592",
            DurationMinutes = 15
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"outcome\":\"AddedOnline\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"isSuccess\":true", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"playerName\":\"^1Fraser\"", content, StringComparison.OrdinalIgnoreCase);
        mockRconClient.As<ICallOfDuty4xRconClient>()
            .Verify(x => x.TempBanPlayerByPlayerIdentifier("2310346615957836592", 15, DefaultCoD4xBanReason), Times.Once);
    }

    [Fact]
    public async Task CoD4xUnban_WhenServerNotFound_ReturnsNotFound()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServerNotFound(gameServerId);

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/unban", new CoD4xUnbanRequestDto
        {
            PlayerIdentifier = "2310346615957836592"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CoD4xUnban_WhenIdentifierIsInvalid_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/unban", new CoD4xUnbanRequestDto
        {
            PlayerIdentifier = "invalid id"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CoD4xUnban_WhenServerIsNotCoD4x_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServer(gameServerId, GameType.CallOfDuty4);

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/unban", new CoD4xUnbanRequestDto
        {
            PlayerIdentifier = "2310346615957836592"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CoD4xUnban_WhenRequestIsValid_ReturnsOkAndCallsRconClient()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServer(gameServerId, GameType.CallOfDuty4x);
        SetupRconConfiguration(gameServerId, /*lang=json,strict*/ "{\"password\":\"secret\"}");

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient.As<ICallOfDuty4xRconClient>()
            .Setup(x => x.UnbanPlayerByPlayerIdentifier("2310346615957836592"))
            .ReturnsAsync("Removing ban for Nick: Fraser, PlayerID: 2310346615957836592, Banreason: test ban");

        _factory.MockRconClientFactory
            .Setup(x => x.CreateInstance(GameType.CallOfDuty4x, gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/unban", new CoD4xUnbanRequestDto
        {
            PlayerIdentifier = "2310346615957836592"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"outcome\":\"Removed\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"isSuccess\":true", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"banReason\":\"test ban\"", content, StringComparison.OrdinalIgnoreCase);
        mockRconClient.As<ICallOfDuty4xRconClient>()
            .Verify(x => x.UnbanPlayerByPlayerIdentifier("2310346615957836592"), Times.Once);

        _factory.MockBanLifecycleEventPublisher
            .Verify(x => x.PublishBanLiftAppliedAsync(
                gameServerId,
                GameType.CallOfDuty4x.ToString(),
                "2310346615957836592",
                "Fraser",
                "portal",
                "Portal unban command applied",
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
    }

    [Fact]
    public async Task CoD4xUnban_WhenBanLiftPublishFails_ReturnsOkAndCallsRconClient()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServer(gameServerId, GameType.CallOfDuty4x);
        SetupRconConfiguration(gameServerId, /*lang=json,strict*/ "{\"password\":\"secret\"}");

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient.As<ICallOfDuty4xRconClient>()
            .Setup(x => x.UnbanPlayerByPlayerIdentifier("2310346615957836592"))
            .ReturnsAsync("Removing ban for Nick: Fraser, PlayerID: 2310346615957836592, Banreason: test ban");

        _factory.MockRconClientFactory
            .Setup(x => x.CreateInstance(GameType.CallOfDuty4x, gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        _factory.MockBanLifecycleEventPublisher
            .Setup(x => x.PublishBanLiftAppliedAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated publish failure"));

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/unban", new CoD4xUnbanRequestDto
        {
            PlayerIdentifier = "2310346615957836592"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        mockRconClient.As<ICallOfDuty4xRconClient>()
            .Verify(x => x.UnbanPlayerByPlayerIdentifier("2310346615957836592"), Times.Once);

        _factory.MockBanLifecycleEventPublisher
            .Verify(x => x.PublishBanLiftAppliedAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
    }

    [Fact]
    public async Task CoD4xUnban_WhenUnbanResponseIsNotRemoved_DoesNotPublishBanLiftApplied()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServer(gameServerId, GameType.CallOfDuty4x);
        SetupRconConfiguration(gameServerId, /*lang=json,strict*/ "{\"password\":\"secret\"}");

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient.As<ICallOfDuty4xRconClient>()
            .Setup(x => x.UnbanPlayerByPlayerIdentifier("2310346615957836592"))
            .ReturnsAsync("No matching ban entry for target");

        _factory.MockRconClientFactory
            .Setup(x => x.CreateInstance(GameType.CallOfDuty4x, gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/unban", new CoD4xUnbanRequestDto
        {
            PlayerIdentifier = "2310346615957836592"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        _factory.MockBanLifecycleEventPublisher
            .Verify(x => x.PublishBanLiftAppliedAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
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
