using System.Net;
using System.Net.Http.Json;
using Moq;
using MX.Api.Abstractions;
using Newtonsoft.Json;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.Models.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.Configurations;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.GameServers;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.IntegrationTests.V1;

[Trait("Category", "Integration")]
public class RconCoD4xCommandEndpointsTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RconCoD4xCommandEndpointsTests(CustomWebApplicationFactory factory)
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
    public async Task CoD4xBanUser_WhenValidRequest_ReturnsOkAndCallsRconClient()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServer(gameServerId, GameType.CallOfDuty4x);
        SetupRconConfiguration(gameServerId, JsonConvert.SerializeObject(new { password = "secret" }));

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient.As<ICallOfDuty4xRconClient>()
            .Setup(x => x.BanUser("2310346615957836592", "rule break"))
            .ReturnsAsync("ok");

        _factory.MockRconClientFactory
            .Setup(x => x.CreateInstance(GameType.CallOfDuty4x, gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/ban-user", new CoD4xTargetReasonRequestDto
        {
            Target = "2310346615957836592",
            Reason = "rule break"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        mockRconClient.As<ICallOfDuty4xRconClient>()
            .Verify(x => x.BanUser("2310346615957836592", "rule break"), Times.Once);
    }

    [Fact]
    public async Task CoD4xBanUser_WhenTargetIsWrappedInQuotes_ReturnsOkAndCallsRconClient()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServer(gameServerId, GameType.CallOfDuty4x);
        SetupRconConfiguration(gameServerId, JsonConvert.SerializeObject(new { password = "secret" }));

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient.As<ICallOfDuty4xRconClient>()
            .Setup(x => x.BanUser("\"2310346615957836592\"", "rule break"))
            .ReturnsAsync("ok");

        _factory.MockRconClientFactory
            .Setup(x => x.CreateInstance(GameType.CallOfDuty4x, gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/ban-user", new CoD4xTargetReasonRequestDto
        {
            Target = "\"2310346615957836592\"",
            Reason = "rule break"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        mockRconClient.As<ICallOfDuty4xRconClient>()
            .Verify(x => x.BanUser("\"2310346615957836592\"", "rule break"), Times.Once);
    }

    [Fact]
    public async Task CoD4xBanUser_WhenTargetContainsUnsupportedCharacters_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/ban-user", new CoD4xTargetReasonRequestDto
        {
            Target = "2310346615957836592;quit",
            Reason = "rule break"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CoD4xBanUser_WhenReasonContainsUnsupportedCharacters_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/ban-user", new CoD4xTargetReasonRequestDto
        {
            Target = "2310346615957836592",
            Reason = "rule break;quit"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CoD4xScreenSay_WhenMessageMissing_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/screen-say", new CoD4xMessageRequestDto
        {
            Message = "  "
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CoD4xScreenSay_WhenMessageContainsUnsupportedCharacters_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/screen-say", new CoD4xMessageRequestDto
        {
            Message = "watch language;quit"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CoD4xStatus_WhenValidRequest_ReturnsOkAndCallsRconClient()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServer(gameServerId, GameType.CallOfDuty4x);
        SetupRconConfiguration(gameServerId, JsonConvert.SerializeObject(new { password = "secret" }));

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient.As<ICallOfDuty4xRconClient>()
            .Setup(x => x.Status())
            .ReturnsAsync(
                "hostname: ^1XI Test Server\n" +
                "version: CoD4x 21.1\n" +
                "map: mp_crash\n" +
                "num score ping playerid steamid name lastmsg address qport rate\n" +
                "0 42 50 2310346615957836592 2310346615957836592 ^1Fraser 0 127.0.0.1:28960 28960 25000");

        _factory.MockRconClientFactory
            .Setup(x => x.CreateInstance(GameType.CallOfDuty4x, gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var response = await _client.GetAsync($"/v1.0/rcon/{gameServerId}/cod4x/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"hostname\":\"^1XI Test Server\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"mapName\":\"mp_crash\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"rawName\":\"^1Fraser\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"name\":\"Fraser\"", content, StringComparison.OrdinalIgnoreCase);
        mockRconClient.As<ICallOfDuty4xRconClient>()
            .Verify(x => x.Status(), Times.Once);
    }

    [Fact]
    public async Task CoD4xDumpBanList_WhenValidRequest_ReturnsOkAndCallsRconClient()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServer(gameServerId, GameType.CallOfDuty4x);
        SetupRconConfiguration(gameServerId, JsonConvert.SerializeObject(new { password = "secret" }));

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient.As<ICallOfDuty4xRconClient>()
            .Setup(x => x.DumpBanList())
            .ReturnsAsync(
                "0 playerid: 2310346615957836592; nick: Fraser; adminsteamid: System/Rcon; expire: Never; reason: test ban\n" +
                "1 Active bans");

        _factory.MockRconClientFactory
            .Setup(x => x.CreateInstance(GameType.CallOfDuty4x, gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var response = await _client.GetAsync($"/v1.0/rcon/{gameServerId}/cod4x/dumpbanlist");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"activeBanCount\":1", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"playerIdentifier\":\"2310346615957836592\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"isPermanent\":true", content, StringComparison.OrdinalIgnoreCase);
        mockRconClient.As<ICallOfDuty4xRconClient>()
            .Verify(x => x.DumpBanList(), Times.Once);
    }

    [Fact]
    public async Task CoD4xMaps_WhenValidRequest_ReturnsOkAndCallsRconClient()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServer(gameServerId, GameType.CallOfDuty4x);
        SetupRconConfiguration(gameServerId, JsonConvert.SerializeObject(new { password = "secret" }));

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient.As<ICallOfDuty4xRconClient>()
            .Setup(x => x.GetMaps())
            .ReturnsAsync([
                new Quake3QueryMap { GameType = "war", MapName = "mp_crash" },
                new Quake3QueryMap { GameType = "sd", MapName = "mp_backlot" }
            ]);

        _factory.MockRconClientFactory
            .Setup(x => x.CreateInstance(GameType.CallOfDuty4x, gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var response = await _client.GetAsync($"/v1.0/rcon/{gameServerId}/cod4x/maps");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"mapName\":\"mp_crash\"", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"mapName\":\"mp_backlot\"", content, StringComparison.OrdinalIgnoreCase);
        mockRconClient.As<ICallOfDuty4xRconClient>()
            .Verify(x => x.GetMaps(), Times.Once);
    }

    [Fact]
    public async Task CoD4xSet_WhenDvarNameMissing_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/set", new CoD4xSetDvarRequestDto
        {
            DvarName = " ",
            Value = "1"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CoD4xSet_WhenDvarNameContainsUnsupportedCharacters_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/set", new CoD4xSetDvarRequestDto
        {
            DvarName = "sv_hostname;quit",
            Value = "my server"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CoD4xSet_WhenValueContainsUnsupportedCharacters_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/set", new CoD4xSetDvarRequestDto
        {
            DvarName = "sv_hostname",
            Value = "my server;quit"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CoD4xMap_WhenMapNameContainsUnsupportedCharacters_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/map", new CoD4xMapRequestDto
        {
            MapName = "mp_crash;quit"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CoD4xMap_WhenMapNameContainsWhitespace_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/map", new CoD4xMapRequestDto
        {
            MapName = "mp crash"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CoD4xGametype_WhenGametypeContainsUnsupportedCharacters_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/gametype", new CoD4xGametypeRequestDto
        {
            GameType = "war;quit"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CoD4xPluginInfo_WhenPluginNameContainsUnsupportedCharacters_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/plugins/info", new CoD4xPluginRequestDto
        {
            PluginName = "cod4x_anticheat;quit"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CoD4xSet_WhenDvarNameContainsWhitespace_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/set", new CoD4xSetDvarRequestDto
        {
            DvarName = "sv hostname",
            Value = "my server"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CoD4xAdminChangeCommandPower_WhenCommandContainsUnsupportedCharacters_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/admin/change-command-power", new CoD4xAdminChangeCommandPowerRequestDto
        {
            Command = "kick;quit",
            MinPower = 20
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CoD4xAdminAddAdmin_WhenUserContainsUnsupportedCharacters_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/admin/add-admin", new CoD4xAdminAddAdminRequestDto
        {
            User = "admin;quit",
            Power = 40
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CoD4xAdminRemoveAdmin_WhenUserContainsUnsupportedCharacters_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/admin/remove-admin", new CoD4xAdminUserRequestDto
        {
            User = "admin;quit"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CoD4xAdminChangePassword_WhenUserContainsUnsupportedCharacters_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/admin/change-password", new CoD4xAdminChangePasswordRequestDto
        {
            User = "admin;quit",
            NewPassword = "new-secret"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CoD4xAdminChangePassword_WhenNewPasswordContainsUnsupportedCharacters_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/admin/change-password", new CoD4xAdminChangePasswordRequestDto
        {
            User = "admin",
            NewPassword = "new-secret;quit"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CoD4xRecord_WhenDemoNameContainsUnsupportedCharacters_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/record", new CoD4xRecordRequestDto
        {
            Target = "5",
            DemoName = "scrim01;quit"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CoD4xPluginInfo_WhenValidRequest_ReturnsOkAndCallsRconClient()
    {
        var gameServerId = Guid.NewGuid();
        SetupGameServer(gameServerId, GameType.CallOfDuty4x);
        SetupRconConfiguration(gameServerId, JsonConvert.SerializeObject(new { password = "secret" }));

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient.As<ICallOfDuty4xRconClient>()
            .Setup(x => x.PluginInfo("cod4x_anticheat"))
            .ReturnsAsync("plugin info");

        _factory.MockRconClientFactory
            .Setup(x => x.CreateInstance(GameType.CallOfDuty4x, gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var response = await _client.PostAsJsonAsync($"/v1.0/rcon/{gameServerId}/cod4x/plugins/info", new CoD4xPluginRequestDto
        {
            PluginName = "cod4x_anticheat"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        mockRconClient.As<ICallOfDuty4xRconClient>()
            .Verify(x => x.PluginInfo("cod4x_anticheat"), Times.Once);
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
