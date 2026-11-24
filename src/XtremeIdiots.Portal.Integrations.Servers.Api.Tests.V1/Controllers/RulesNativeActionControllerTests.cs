using System.Net;
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
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.Configurations;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.GameServers;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.RulesEngine;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Controllers;

[Trait("Category", "Unit")]
public class RulesNativeActionControllerTests
{
    [Theory]
    [InlineData("matching", "Dispatched", 1)]
    [InlineData("different", "Stale", 0)]
    [InlineData("missing", "Failed", 0)]
    [InlineData("unparsed", "Failed", 0)]
    [InlineData("duplicate", "Failed", 0)]
    [InlineData("transport", "OutcomeUnknown", 1)]
    public async Task Kick_RevalidatesAndDispatchesAtMostOnce(string scenario, string expected, int dispatchCount)
    {
        var fixture = new Fixture(GameType.CallOfDuty4);
        var roster = new RconRosterObservationDto
        {
            State = RconRosterState.Partial,
            Players = scenario == "missing" ? [] : [new() { Slot = 3, PlayerIdentifier = scenario == "different" ? "other" : "abc" }],
            UnparsedRowCount = scenario == "unparsed" ? 1 : 0
        };
        if (scenario == "duplicate")
        {
            roster.Players.Add(new RconRosterPlayerDto { Slot = 4, PlayerIdentifier = "abc" });
        }
        fixture.Client.As<IPlayerRosterRconClient>()
            .Setup(x => x.GetPlayersWithRoster(It.IsAny<CancellationToken>()))
            .Returns((new List<IRconPlayer>(), roster));
        var dispatch = fixture.Client.As<IRulesNativeRconClient>()
            .Setup(x => x.DispatchKick(3, It.IsAny<CancellationToken>()));
        if (scenario == "transport")
        {
            dispatch.Throws(new IOException("response lost"));
        }
        else
        {
            dispatch.Returns(Task.CompletedTask);
        }

        var result = await fixture.Controller.Execute(fixture.ServerId, fixture.Command(RuleNativeEffectType.DropPlayer));

        Assert.Equal(expected, Outcome(result));
        fixture.Client.As<IRulesNativeRconClient>().Verify(x => x.DispatchKick(3, It.IsAny<CancellationToken>()), Times.Exactly(dispatchCount));
        Assert.Equal(expected == "Dispatched" ? 1 : 0, fixture.Audit.Invocations.Count);
        fixture.Repository.Verify(x => x.GameServersEvents.V1.CreateGameServerEvent(It.IsAny<CreateGameServerEventDto>(), It.IsAny<CancellationToken>()),
            expected == "Dispatched" ? Times.Once() : Times.Never());
    }

    [Fact]
    public async Task Kick_WithReason_IsNotSilentlyDowngraded()
    {
        var fixture = new Fixture(GameType.CallOfDuty4);
        var result = await fixture.Controller.Execute(fixture.ServerId, fixture.Command(RuleNativeEffectType.DropPlayer) with
        { Effect = new RuleNativeEffect { Type = RuleNativeEffectType.DropPlayer, Reason = "Reason" } });
        Assert.Equal("SkippedUnsupported", Outcome(result));
        Assert.Empty(fixture.Audit.Invocations);
    }

    [Fact]
    public async Task UnsupportedGame_DoesNotDispatch()
    {
        var fixture = new Fixture(GameType.Rust);
        var result = await fixture.Controller.Execute(fixture.ServerId, fixture.Command(RuleNativeEffectType.PublicMessage, "hello"));
        Assert.Equal("SkippedUnsupported", Outcome(result));
        Assert.Empty(fixture.Audit.Invocations);
    }

    [Fact]
    public async Task PublicMessage_DispatchesAndWritesAudit()
    {
        var fixture = new Fixture(GameType.CallOfDuty2);
        fixture.Client.As<IRulesNativeRconClient>()
            .Setup(x => x.DispatchPublicMessage("hello", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var result = await fixture.Controller.Execute(fixture.ServerId, fixture.Command(RuleNativeEffectType.PublicMessage, "hello"));
        Assert.Equal("Dispatched", Outcome(result));
        var response = Assert.IsType<ApiResponse<RuleActionResult>>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal(fixture.ServerId, response.Data!.GameServerId);
        fixture.Client.As<IRulesNativeRconClient>().Verify(x => x.DispatchPublicMessage("hello", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Single(fixture.Audit.Invocations);
    }

    [Theory]
    [InlineData("bad;command")]
    [InlineData("a\"b")]
    [InlineData("caf\u00e9")]
    public async Task PublicMessage_UnsupportedTextIsNotChanged(string message)
    {
        var fixture = new Fixture(GameType.CallOfDuty2);
        var result = await fixture.Controller.Execute(fixture.ServerId, fixture.Command(RuleNativeEffectType.PublicMessage, message));
        Assert.Equal("SkippedUnsupported", Outcome(result));
        Assert.Empty(fixture.Audit.Invocations);
    }

    [Theory]
    [InlineData(true, "Stale")]
    [InlineData(false, "WouldExecute")]
    public async Task ExpiredOrDryRun_DoesNotDispatch(bool expired, string expected)
    {
        var fixture = new Fixture(GameType.CallOfDuty2);
        var command = fixture.Command(RuleNativeEffectType.PublicMessage, "hello") with
        {
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(expired ? -1 : 1),
            Mode = RuleExecutionMode.DryRun
        };
        var result = await fixture.Controller.Execute(fixture.ServerId, command);
        Assert.Equal(expected, Outcome(result));
        fixture.Client.As<IRulesNativeRconClient>().Verify(x => x.DispatchPublicMessage(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Empty(fixture.Audit.Invocations);
    }

    [Fact]
    public async Task PrivateMessage_RequiresCurrentTarget()
    {
        var fixture = new Fixture(GameType.CallOfDuty5);
        fixture.Client.As<IPlayerRosterRconClient>()
            .Setup(x => x.GetPlayersWithRoster(It.IsAny<CancellationToken>()))
            .Returns((new List<IRconPlayer>(), new RconRosterObservationDto
            {
                State = RconRosterState.Partial,
                Players = [new() { Slot = 3, PlayerIdentifier = "different" }]
            }));
        var result = await fixture.Controller.Execute(fixture.ServerId, fixture.Command(RuleNativeEffectType.PrivateMessage, "hello"));
        Assert.Equal("Stale", Outcome(result));
        fixture.Client.As<IRulesNativeRconClient>().Verify(x => x.DispatchPrivateMessage(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("plugin", "SkippedUnsupported")]
    [InlineData("missing", "Failed")]
    [InlineData("expired", "Failed")]
    [InlineData("future", "Failed")]
    [InlineData("different", "Stale")]
    [InlineData("runtime", "Stale")]
    [InlineData("generation", "SkippedUnsupported")]
    public async Task ConnectionBinding_IsRequiredBeforeDispatch(string scenario, string expected)
    {
        var fixture = new Fixture(GameType.CallOfDuty4);
        var command = fixture.Command(RuleNativeEffectType.PublicMessage, "hello");
        var binding = fixture.Binding;
        if (scenario == "plugin")
        {
            command = command with { Runtime = command.Runtime with { IdentityModel = RuleRuntimeIdentityModel.PluginRuntimeGeneration } };
        }
        else if (scenario == "generation")
        {
            command = command with { ConnectionGeneration = 1 };
        }
        else
        {
            RuleConnectionBindingDto? replacement = scenario switch
            {
                "missing" => null,
                "expired" => binding with { ObservedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-3) },
                "future" => binding with { ObservedAtUtc = DateTimeOffset.UtcNow.AddMinutes(1) },
                "different" => binding with { ConnectionId = Guid.NewGuid() },
                "runtime" => binding with { Runtime = binding.Runtime with { InstanceId = Guid.NewGuid() } },
                _ => binding
            };
            fixture.SetBinding(replacement);
        }

        var result = await fixture.Controller.Execute(fixture.ServerId, command);

        Assert.Equal(expected, Outcome(result));
        fixture.Client.As<IRulesNativeRconClient>().Verify(x => x.DispatchPublicMessage(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static string Outcome(IActionResult action) =>
        Assert.IsType<ApiResponse<RuleActionResult>>(Assert.IsType<ObjectResult>(action).Value).Data!.Outcome.ToString();

    private sealed class Fixture
    {
        public Guid ServerId { get; } = Guid.NewGuid();
        public Mock<IRepositoryApiClient> Repository { get; } = new() { DefaultValue = DefaultValue.Mock };
        public Mock<IRconClient> Client { get; } = new();
        public Mock<IAuditLogger> Audit { get; } = new();
        public RulesNativeActionController Controller { get; }
        private readonly Guid _connectionId = Guid.NewGuid();
        private readonly RuleRuntimeDescriptor _runtime = new()
        {
            Id = "server-agent",
            Version = "1",
            ProtocolVersion = 1,
            IdentityModel = RuleRuntimeIdentityModel.AgentObservedConnection,
            InstanceId = Guid.NewGuid(),
            StartedAtUtc = DateTimeOffset.UtcNow.AddHours(-1)
        };
        public RuleConnectionBindingDto Binding { get; }

        public RuleActionCommand Command(RuleNativeEffectType type, string? message = null) => new()
        {
            GameServerId = ServerId,
            ExecutionId = Guid.NewGuid(),
            ConnectionId = _connectionId,
            Runtime = _runtime,
            Correlation = new RuleActionCorrelation { ConnectionId = _connectionId },
            ActionId = "test-action",
            LogicalEffectId = "test-effect",
            SlotId = 3,
            PlayerGuid = "abc",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(1),
            Mode = RuleExecutionMode.Enforce,
            Effect = new RuleNativeEffect { Type = type, Message = message }
        };

        public Fixture(GameType gameType)
        {
            Binding = new RuleConnectionBindingDto
            {
                GameServerId = ServerId,
                GameType = gameType,
                SlotId = 3,
                PlayerGuid = "abc",
                ConnectionId = _connectionId,
                Runtime = _runtime,
                ObservedAtUtc = DateTimeOffset.UtcNow,
                ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(2)
            };
            SetBinding(Binding);
            var server = JsonConvert.DeserializeObject<GameServerDto>(JsonConvert.SerializeObject(new
            {
                GameServerId = ServerId,
                GameType = (int)gameType,
                Hostname = "127.0.0.1",
                QueryPort = 28960
            }))!;
            var config = JsonConvert.DeserializeObject<ConfigurationDto>(JsonConvert.SerializeObject(new
            {
                Namespace = "rcon",
                Configuration = JsonConvert.SerializeObject(new { password = "test" })
            }))!;
            Repository.Setup(x => x.GameServers.V1.GetGameServer(ServerId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ApiResult<GameServerDto>(HttpStatusCode.OK, new ApiResponse<GameServerDto>(server)));
            Repository.Setup(x => x.GameServerConfigurations.V1.GetConfiguration(ServerId, "rcon", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ApiResult<ConfigurationDto>(HttpStatusCode.OK, new ApiResponse<ConfigurationDto>(config)));
            var factory = new Mock<IRconClientFactory>();
            Client.As<IPlayerRosterRconClient>();
            Client.As<IRulesNativeRconClient>();
            Client.As<IPlayerRosterRconClient>()
                .Setup(x => x.GetPlayersWithRoster(It.IsAny<CancellationToken>()))
                .Returns((new List<IRconPlayer>(), new RconRosterObservationDto
                {
                    State = RconRosterState.Partial,
                    Players = [new() { Slot = 3, PlayerIdentifier = "abc" }]
                }));
            factory.Setup(x => x.CreateInstance(gameType, ServerId, "127.0.0.1", 28960, "test")).Returns(Client.Object);
            Controller = new RulesNativeActionController(Mock.Of<ILogger<RulesNativeActionController>>(), Repository.Object, factory.Object, Audit.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        public void SetBinding(RuleConnectionBindingDto? binding) =>
            Repository.Setup(x => x.RuleConnectionBindings.V1.GetCurrentBinding(ServerId, 3, "abc", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ApiResult<RuleConnectionBindingDto>(HttpStatusCode.OK, new ApiResponse<RuleConnectionBindingDto>(binding)));
    }
}
