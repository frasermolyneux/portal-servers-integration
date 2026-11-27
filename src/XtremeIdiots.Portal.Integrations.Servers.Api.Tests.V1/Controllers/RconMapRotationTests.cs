using System.Net;
using System.Net.Sockets;
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
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;
using XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.Models.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.Configurations;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.GameServers;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Controllers;

[Trait("Category", "Unit")]
public class RconMapRotationTests
{
    [Theory]
    [InlineData(GameType.CallOfDuty2)]
    [InlineData(GameType.CallOfDuty4)]
    [InlineData(GameType.CallOfDuty4x)]
    [InlineData(GameType.CallOfDuty5)]
    public async Task Maps_ReturnsOrderedDuplicateEntriesOrConfirmedEmptyWithoutAuditing(GameType game)
    {
        await VerifyMaps(game, "maps");
        await VerifyMaps(game, "empty");
    }

    [Theory]
    [InlineData(GameType.CallOfDuty2)]
    [InlineData(GameType.CallOfDuty4)]
    [InlineData(GameType.CallOfDuty4x)]
    [InlineData(GameType.CallOfDuty5)]
    public async Task Maps_ReturnsErrorWithoutSnapshotForMalformedOrTransportFailure(GameType game)
    {
        await VerifyMaps(game, "malformed");
        await VerifyMaps(game, "transport");
    }

    private static async Task VerifyMaps(GameType game, string outcome)
    {
        var id = Guid.NewGuid();
        var repository = new Mock<IRepositoryApiClient> { DefaultValue = DefaultValue.Mock };
        var server = JsonConvert.DeserializeObject<GameServerDto>(JsonConvert.SerializeObject(new
        {
            GameServerId = id,
            GameType = (int)game,
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
        client.As<ICallOfDuty4xRconClient>();
        using var cancellation = new CancellationTokenSource();
        var rotation = client.As<IMapRotationRconClient>();
        var query = rotation.Setup(x => x.GetMaps(cancellation.Token));
        switch (outcome)
        {
            case "maps":
                query.ReturnsAsync(
                [
                    new Quake3QueryMap { GameType = "ftag", MapName = "mp_first" },
                    new Quake3QueryMap { GameType = "ftag", MapName = "mp_first" },
                    new Quake3QueryMap { GameType = "sd", MapName = "mp_first" }
                ]);
                break;
            case "empty":
                query.ReturnsAsync([]);
                break;
            case "malformed":
                query.ThrowsAsync(new FormatException("Incomplete dvar response."));
                break;
            case "transport":
                query.ThrowsAsync(new SocketException());
                break;
        }
        var factory = new Mock<IRconClientFactory>();
        factory.Setup(x => x.CreateInstance(game, id, "127.0.0.1", 28960, "test")).Returns(client.Object);
        using var telemetryConfiguration = new TelemetryConfiguration { TelemetryChannel = new Mock<ITelemetryChannel>().Object };
        var telemetry = new TelemetryClient(telemetryConfiguration);
        var audit = new Mock<IAuditLogger>();
        Controller controller = game switch
        {
            GameType.CallOfDuty2 => new Cod2RconController(Mock.Of<ILogger<Cod2RconController>>(), repository.Object, factory.Object, telemetry, audit.Object),
            GameType.CallOfDuty4 => new Cod4RconController(Mock.Of<ILogger<Cod4RconController>>(), repository.Object, factory.Object, telemetry, audit.Object),
            GameType.CallOfDuty4x => new CoD4xRconController(Mock.Of<ILogger<CoD4xRconController>>(), repository.Object, factory.Object, telemetry, audit.Object, Mock.Of<IBanLifecycleEventPublisher>()),
            GameType.CallOfDuty5 => new Cod5RconController(Mock.Of<ILogger<Cod5RconController>>(), repository.Object, factory.Object, telemetry, audit.Object),
            _ => throw new ArgumentOutOfRangeException(nameof(game))
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestAborted = cancellation.Token }
        };
        var action = controller switch
        {
            Cod2RconController cod2 => cod2.GetMaps(id),
            Cod4RconController cod4 => cod4.GetMaps(id),
            CoD4xRconController cod4x => cod4x.GetMaps(id),
            Cod5RconController cod5 => cod5.GetMaps(id),
            _ => throw new InvalidOperationException()
        };
        var result = Assert.IsType<ObjectResult>(await action);
        var response = Assert.IsType<ApiResponse<RconMapCollectionDto>>(result.Value);
        if (outcome is "malformed" or "transport")
        {
            Assert.Null(response.Data);
            Assert.Equal("RCON_OPERATION_FAILED", Assert.Single(response.Errors!).Code);
        }
        else
        {
            Assert.Empty(response.Errors ?? []);
            var maps = Assert.IsType<RconMapCollectionDto>(response.Data).Items;
            if (outcome == "empty")
            {
                Assert.Empty(maps);
            }
            else
            {
                Assert.Equal([("ftag", "mp_first"), ("ftag", "mp_first"), ("sd", "mp_first")],
                    maps.Select(map => (map.GameType, map.MapName)).ToArray());
            }
        }
        rotation.Verify(x => x.GetMaps(cancellation.Token), Times.Once);
        audit.VerifyNoOtherCalls();
        repository.Verify(x => x.GameServersEvents.V1.CreateGameServerEvent(
            It.IsAny<CreateGameServerEventDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
