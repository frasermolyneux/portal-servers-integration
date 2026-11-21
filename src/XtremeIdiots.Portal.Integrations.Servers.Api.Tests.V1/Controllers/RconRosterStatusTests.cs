using System.Text.Json;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MX.Api.Abstractions;
using MX.Observability.ApplicationInsights.Auditing;
using Newtonsoft.Json;
using System.Net;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;
using XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.Configurations;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.GameServers;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Controllers;

[Trait("Category", "Unit")]
public class RconRosterStatusTests
{
    private const string Header =
        "map: mp_crash\n" +
        "num score ping playerid steamid name lastmsg address qport rate\n" +
        "--- ----- ---- -------- ------- ---- ------- ------- ----- ----\n";

    [Fact]
    public void Cod4xStatus_ReportsUnparsedAndMissingIdentitiesWithoutLosingValidPlayers()
    {
        var response = CoD4xRconController.ParseStatusResponse(Header +
            "0 42 50 2310346615957836592 0 Alice 0 127.0.0.1:28960 -42 25000\n" +
            "1 0 50 0 0 Pending 0 127.0.0.2:28960 42 25000\n" +
            "2 0 0 2310346615957836599 0 TestBot 0 bot 0 25000\n" +
            "3 malformed player\n");

        var roster = Assert.IsType<RconRosterObservationDto>(response.Roster);
        Assert.Equal(RconRosterState.Partial, roster.State);
        Assert.Equal("2310346615957836592", Assert.Single(roster.Players).PlayerIdentifier);
        Assert.Equal(1, roster.MissingIdentityCount);
        Assert.Equal(1, roster.KnownBotCount);
        Assert.Equal(2, roster.UnparsedRowCount);
    }

    [Fact]
    public void Roster_UsesStringEnumsAndOldResponsesDefaultToUnknown()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var oldResponse = System.Text.Json.JsonSerializer.Deserialize<RconStatusResponseDto>("{}", options);
        Assert.Null(oldResponse!.Roster);

        var json = System.Text.Json.JsonSerializer.Serialize(new RconRosterObservationDto { State = RconRosterState.Partial }, options);
        Assert.Contains("\"state\":\"Partial\"", json);
        Assert.Equal(RconRosterState.Partial, System.Text.Json.JsonSerializer.Deserialize<RconRosterObservationDto>(json, options)!.State);
        Assert.DoesNotContain("ipAddress", json);
        Assert.DoesNotContain("rawResponse", json);
    }

    [Fact]
    public void AdditiveRoster_DoesNotChangeLegacyStatusFields()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var status = new RconStatusResponseDto
        {
            Players = [new RconStatusPlayerDto { Num = 1, Guid = "123", Name = "Player" }],
            Roster = new RconRosterObservationDto { State = RconRosterState.Partial }
        };
        var json = System.Text.Json.JsonSerializer.Serialize(status, options);
        var legacy = System.Text.Json.JsonSerializer.Deserialize<LegacyStatus>(json, options)!;
        var newtonsoftLegacy = JsonConvert.DeserializeObject<LegacyStatus>(json)!;
        Assert.Equal("123", Assert.Single(legacy.Players).Guid);
        Assert.Equal("123", Assert.Single(newtonsoftLegacy.Players).Guid);

        var cod4x = new CoD4xStatusResponseDto
        {
            Hostname = "Test",
            Players = [new CoD4xStatusPlayerDto { Num = 1, PlayerIdentifier = "123456" }],
            Roster = new RconRosterObservationDto { State = RconRosterState.Partial }
        };
        json = System.Text.Json.JsonSerializer.Serialize(cod4x, options);
        var legacyCod4x = System.Text.Json.JsonSerializer.Deserialize<LegacyCoD4xStatus>(json, options)!;
        var newtonsoftCod4x = JsonConvert.DeserializeObject<LegacyCoD4xStatus>(json)!;
        Assert.Equal("123456", Assert.Single(legacyCod4x.Players).PlayerIdentifier);
        Assert.Equal("123456", Assert.Single(newtonsoftCod4x.Players).PlayerIdentifier);
        Assert.Equal("Test", legacyCod4x.Hostname);
    }

    [Fact]
    public async Task Cod4xStatus_ApiExposesPartialRoster()
    {
        var id = Guid.NewGuid();
        var repository = new Mock<IRepositoryApiClient> { DefaultValue = DefaultValue.Mock };
        var server = JsonConvert.DeserializeObject<GameServerDto>(JsonConvert.SerializeObject(new
        {
            GameServerId = id,
            GameType = (int)GameType.CallOfDuty4x,
            Hostname = "127.0.0.1",
            QueryPort = 28960
        }))!;
        var config = JsonConvert.DeserializeObject<ConfigurationDto>(JsonConvert.SerializeObject(new
        {
            Namespace = "rcon",
            Configuration = JsonConvert.SerializeObject(new { password = "test" })
        }))!;
        repository.Setup(x => x.GameServers.V1.GetGameServer(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<GameServerDto>(HttpStatusCode.OK, new ApiResponse<GameServerDto>(server)));
        repository.Setup(x => x.GameServerConfigurations.V1.GetConfiguration(id, "rcon", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<ConfigurationDto>(HttpStatusCode.OK, new ApiResponse<ConfigurationDto>(config)));
        var client = new Mock<IRconClient>();
        client.As<ICallOfDuty4xRconClient>().Setup(x => x.Status()).ReturnsAsync(Header +
            "0 42 50 2310346615957836592 0 Alice 0 127.0.0.1:28960 42 25000\n" +
            "1 malformed player\n");
        var factory = new Mock<IRconClientFactory>();
        factory.Setup(x => x.CreateInstance(GameType.CallOfDuty4x, id, "127.0.0.1", 28960, "test")).Returns(client.Object);
        var telemetry = new TelemetryClient(new TelemetryConfiguration { TelemetryChannel = new Mock<ITelemetryChannel>().Object });
        var controller = new CoD4xRconController(Mock.Of<ILogger<CoD4xRconController>>(), repository.Object, factory.Object,
            telemetry, Mock.Of<IAuditLogger>(), Mock.Of<IBanLifecycleEventPublisher>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = Assert.IsType<ObjectResult>(await controller.Status(id));
        var data = Assert.IsType<ApiResponse<CoD4xStatusResponseDto>>(result.Value).Data!;
        Assert.Equal(RconRosterState.Partial, data.Roster!.State);
        Assert.Equal("2310346615957836592", Assert.Single(data.Roster.Players).PlayerIdentifier);
        Assert.Equal(1, data.Roster.UnparsedRowCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cod2Status_ApiExposesPartialRosterOrTransportError(bool transportFailure)
    {
        var id = Guid.NewGuid();
        var repository = new Mock<IRepositoryApiClient> { DefaultValue = DefaultValue.Mock };
        var server = JsonConvert.DeserializeObject<GameServerDto>(JsonConvert.SerializeObject(new
        {
            GameServerId = id,
            GameType = (int)GameType.CallOfDuty2,
            Hostname = "127.0.0.1",
            QueryPort = 28960
        }))!;
        var config = JsonConvert.DeserializeObject<ConfigurationDto>(JsonConvert.SerializeObject(new
        {
            Namespace = "rcon",
            Configuration = JsonConvert.SerializeObject(new { password = "test" })
        }))!;
        repository.Setup(x => x.GameServers.V1.GetGameServer(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<GameServerDto>(HttpStatusCode.OK, new ApiResponse<GameServerDto>(server)));
        repository.Setup(x => x.GameServerConfigurations.V1.GetConfiguration(id, "rcon", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<ConfigurationDto>(HttpStatusCode.OK, new ApiResponse<ConfigurationDto>(config)));
        var client = new Mock<IRconClient>();
        client.As<IPlayerRosterRconClient>().Setup(x => x.GetPlayersWithRoster(It.IsAny<CancellationToken>()))
            .Returns((new List<IRconPlayer>(), new RconRosterObservationDto
            {
                State = RconRosterState.Partial,
                Players = [new RconRosterPlayerDto { Slot = 0, PlayerIdentifier = "123456789" }],
                UnparsedRowCount = 1
            }));
        if (transportFailure)
        {
            client.As<IPlayerRosterRconClient>().Setup(x => x.GetPlayersWithRoster(It.IsAny<CancellationToken>()))
                .Throws(new InvalidOperationException("Connection failed"));
        }
        var factory = new Mock<IRconClientFactory>();
        factory.Setup(x => x.CreateInstance(GameType.CallOfDuty2, id, "127.0.0.1", 28960, "test")).Returns(client.Object);
        var telemetry = new TelemetryClient(new TelemetryConfiguration { TelemetryChannel = new Mock<ITelemetryChannel>().Object });
        var controller = new Cod2RconController(Mock.Of<ILogger<Cod2RconController>>(), repository.Object, factory.Object,
            telemetry, Mock.Of<IAuditLogger>())
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = Assert.IsType<ObjectResult>(await controller.Status(id));
        if (transportFailure)
        {
            var errorJson = System.Text.Json.JsonSerializer.Serialize(result.Value);
            Assert.Contains("RCON_OPERATION_FAILED", errorJson);
            return;
        }

        var data = Assert.IsType<ApiResponse<RconStatusResponseDto>>(result.Value).Data!;
        Assert.Equal(RconRosterState.Partial, data.Roster!.State);
        Assert.Equal("123456789", Assert.Single(data.Roster.Players).PlayerIdentifier);
        Assert.Equal(1, data.Roster.UnparsedRowCount);
    }

    private sealed class LegacyStatus
    {
        public List<RconStatusPlayerDto> Players { get; set; } = [];
    }

    private sealed class LegacyCoD4xStatus
    {
        public string? Hostname { get; set; }
        public List<CoD4xStatusPlayerDto> Players { get; set; } = [];
    }
}
