using System.Net;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MX.Api.Abstractions;
using MX.Observability.ApplicationInsights.Auditing;
using MX.Observability.ApplicationInsights.Auditing.Models;
using Newtonsoft.Json;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;
using XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.Configurations;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.GameServers;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Controllers;

[Trait("Category", "Unit")]
public class CoD4xSayAuditTests
{
    [Fact]
    public async Task Say_SuccessAuditsLocalDispatchWithoutClaimingDelivery()
    {
        var fixture = new Fixture();
        fixture.Client.Setup(x => x.Say("hello", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        AuditEvent? audit = null;
        fixture.Audit.Setup(x => x.LogAudit(It.IsAny<AuditEvent>())).Callback<AuditEvent>(value =>
        {
            fixture.Client.Verify(x => x.Say("hello", It.IsAny<CancellationToken>()), Times.Once);
            audit = value;
        });

        var response = Assert.IsType<ObjectResult>(await fixture.Controller.Say(fixture.ServerId, new CoD4xMessageRequestDto { Message = "hello" }));

        Assert.Equal(200, response.StatusCode);
        Assert.NotNull(audit);
        Assert.Equal("RconCoD4xSay", audit.EventName);
        Assert.Equal(AuditOutcome.Success, audit.Outcome);
        Assert.Equal("LocalUdpSend", audit.Properties["Dispatch"]);
        Assert.Equal("Unconfirmed", audit.Properties["PlayerDelivery"]);
        fixture.Repository.Verify(x => x.GameServersEvents.V1.CreateGameServerEvent(
            It.Is<CreateGameServerEventDto>(dto => dto.EventType == "RconCoD4xSay" &&
                dto.EventData.Contains("\"Dispatch\":\"LocalUdpSend\"") &&
                dto.EventData.Contains("\"PlayerDelivery\":\"Unconfirmed\"")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Say_FailedExecutionLogsWithoutAuditOrOperatorEvent(bool notImplemented)
    {
        var fixture = new Fixture();
        fixture.Client.Setup(x => x.Say("hello", It.IsAny<CancellationToken>()))
            .Throws(notImplemented ? new NotImplementedException() : new IOException("send failed"));

        var response = Assert.IsType<ObjectResult>(await fixture.Controller.Say(fixture.ServerId, new CoD4xMessageRequestDto { Message = "hello" }));

        Assert.Contains(notImplemented ? "OPERATION_NOT_IMPLEMENTED" : "RCON_OPERATION_FAILED",
            System.Text.Json.JsonSerializer.Serialize(response.Value));
        fixture.AssertNoAuditOrEvent();
        Assert.Contains(fixture.Logger.Invocations, invocation => invocation.Method.Name == nameof(ILogger.Log));
    }

    [Fact]
    public async Task Say_MissingServerLogsWithoutAuditOrOperatorEvent()
    {
        var fixture = new Fixture();
        fixture.Repository.Setup(x => x.GameServers.V1.GetGameServer(fixture.ServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<GameServerDto>(HttpStatusCode.NotFound, new ApiResponse<GameServerDto>()));

        await fixture.Controller.Say(fixture.ServerId, new CoD4xMessageRequestDto { Message = "hello" });

        fixture.Client.Verify(x => x.Say(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.AssertNoAuditOrEvent();
        Assert.Contains(fixture.Logger.Invocations, invocation => invocation.Method.Name == nameof(ILogger.Log));
    }

    [Fact]
    public async Task Say_SplitsEachItemInOrderAndPreservesContent()
    {
        var fixture = new Fixture();
        var sent = new List<string>();
        fixture.Client.Setup(x => x.Say(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((message, _) => sent.Add(message))
            .Returns(Task.CompletedTask);
        var longMessage = new string('a', 150);

        var response = Assert.IsType<ObjectResult>(await fixture.Controller.Say(fixture.ServerId,
            new CoD4xMessageRequestDto { Message = longMessage, Messages = [" second "] }));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(new[] { new string('a', 137), new string('a', 13), " second " }, sent);
        fixture.Audit.Verify(x => x.LogAudit(It.Is<AuditEvent>(audit =>
            audit.EventName == "RconCoD4xSay" && audit.Properties["CompletedFragments"] == "3")), Times.Once);
    }

    [Fact]
    public async Task Say_InvalidLaterItemDoesNotDispatchAnything()
    {
        var fixture = new Fixture();

        var response = Assert.IsType<ObjectResult>(await fixture.Controller.Say(fixture.ServerId,
            new CoD4xMessageRequestDto { Messages = ["first", "bad;command"] }));

        Assert.Equal(400, response.StatusCode);
        fixture.Repository.Verify(x => x.GameServers.V1.GetGameServer(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Client.Verify(x => x.Say(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.AssertNoAuditOrEvent();
    }

    [Fact]
    public async Task Say_LaterFailureReportsAndAuditsPartialDispatchWithoutRetry()
    {
        var fixture = new Fixture();
        fixture.Client.Setup(x => x.Say("first", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        fixture.Client.Setup(x => x.Say("second", It.IsAny<CancellationToken>())).Throws(new IOException("response lost"));

        var response = Assert.IsType<ObjectResult>(await fixture.Controller.Say(fixture.ServerId,
            new CoD4xMessageRequestDto { Messages = ["first", "second", "third"] }));

        Assert.Equal(502, response.StatusCode);
        var json = System.Text.Json.JsonSerializer.Serialize(response.Value);
        Assert.Contains("RCON_MESSAGE_PARTIAL_FAILURE", json);
        Assert.Contains("Dispatched 1 of 3", json);
        Assert.Contains("outcome is unknown", json);
        fixture.Client.Verify(x => x.Say("first", It.IsAny<CancellationToken>()), Times.Once);
        fixture.Client.Verify(x => x.Say("second", It.IsAny<CancellationToken>()), Times.Once);
        fixture.Client.Verify(x => x.Say("third", It.IsAny<CancellationToken>()), Times.Never);
        fixture.Audit.Verify(x => x.LogAudit(It.Is<AuditEvent>(audit =>
            audit.EventName == "RconCoD4xSay.PartialDispatch" && audit.Properties["CompletedFragments"] == "1")), Times.Once);
        fixture.Repository.Verify(x => x.GameServersEvents.V1.CreateGameServerEvent(
            It.Is<CreateGameServerEventDto>(dto => dto.EventType == "RconCoD4xSay.PartialDispatch"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Say_StopsIfPrefixGrowsAfterTheFirstFragment()
    {
        var fixture = new Fixture();
        fixture.RawClient.SetupSequence(x => x.GetDvar("sv_consayname", It.IsAny<CancellationToken>()))
            .ReturnsAsync("\"sv_consayname\" is: \"^2Server: ^7^7\" default: \"default^7\"")
            .ReturnsAsync("\"sv_consayname\" is: \"^2Server: ^7^7\" default: \"default^7\"")
            .ReturnsAsync($"\"sv_consayname\" is: \"{new string('p', 140)}^7\" default: \"default^7\"");
        fixture.Client.Setup(x => x.Say(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var response = Assert.IsType<ObjectResult>(await fixture.Controller.Say(fixture.ServerId,
            new CoD4xMessageRequestDto { Message = new string('a', 150) }));

        Assert.Equal(502, response.StatusCode);
        Assert.Contains("Dispatched 1 of 2", System.Text.Json.JsonSerializer.Serialize(response.Value));
        fixture.Client.Verify(x => x.Say(new string('a', 137), It.IsAny<CancellationToken>()), Times.Once);
        fixture.Client.Verify(x => x.Say(new string('a', 13), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("screen-say")]
    [InlineData("con-say")]
    [InlineData("tell")]
    [InlineData("screen-tell")]
    [InlineData("con-tell")]
    public async Task OtherMessageChannels_AcceptOrderedLists(string channel)
    {
        var fixture = new Fixture();
        var sent = new List<string>();
        fixture.Client.Setup(x => x.ScreenSay(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((text, _) => sent.Add(text)).ReturnsAsync("ok");
        fixture.Client.Setup(x => x.ConSay(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((text, _) => sent.Add(text)).ReturnsAsync("ok");
        fixture.Client.Setup(x => x.Tell("3", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, text, _) => sent.Add(text)).ReturnsAsync("ok");
        fixture.Client.Setup(x => x.ScreenTell("3", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, text, _) => sent.Add(text)).ReturnsAsync("ok");
        fixture.Client.Setup(x => x.ConTell("3", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, text, _) => sent.Add(text)).ReturnsAsync("ok");
        var publicRequest = new CoD4xMessageRequestDto { Message = "first", Messages = ["second"] };
        var privateRequest = new CoD4xTargetMessageRequestDto { Target = "3", Message = "first", Messages = ["second"] };

        var result = channel switch
        {
            "screen-say" => await fixture.Controller.ScreenSay(fixture.ServerId, publicRequest),
            "con-say" => await fixture.Controller.ConSay(fixture.ServerId, publicRequest),
            "tell" => await fixture.Controller.Tell(fixture.ServerId, privateRequest),
            "screen-tell" => await fixture.Controller.ScreenTell(fixture.ServerId, privateRequest),
            "con-tell" => await fixture.Controller.ConTell(fixture.ServerId, privateRequest),
            _ => throw new ArgumentOutOfRangeException(nameof(channel))
        };

        Assert.Equal(200, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.Equal(new[] { "first", "second" }, sent);
    }

    [Theory]
    [InlineData("client-kick", 0)]
    [InlineData("only-kick", 0)]
    [InlineData("ban-client", 0)]
    [InlineData("client-kick", 1)]
    [InlineData("only-kick", 1)]
    [InlineData("ban-client", 1)]
    public async Task ClientActions_ExplicitNonNegativeSlotDispatches(string action, int clientId)
    {
        var fixture = new Fixture();
        fixture.Client.Setup(x => x.ClientKick(clientId, "reason")).ReturnsAsync("ok");
        fixture.Client.Setup(x => x.OnlyKick(clientId, "reason")).ReturnsAsync("ok");
        fixture.Client.Setup(x => x.BanClient(clientId, "reason")).ReturnsAsync("ok");
        var request = System.Text.Json.JsonSerializer.Deserialize<CoD4xClientReasonRequestDto>(
            $$"""{"clientId":{{clientId}},"reason":"reason"}""",
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

        var response = Assert.IsType<ObjectResult>(await ExecuteClientAction(fixture, action, request));

        Assert.Equal(200, response.StatusCode);
        fixture.Client.Verify(x => x.ClientKick(clientId, "reason"), action == "client-kick" ? Times.Once() : Times.Never());
        fixture.Client.Verify(x => x.OnlyKick(clientId, "reason"), action == "only-kick" ? Times.Once() : Times.Never());
        fixture.Client.Verify(x => x.BanClient(clientId, "reason"), action == "ban-client" ? Times.Once() : Times.Never());
        fixture.Audit.Verify(x => x.LogAudit(It.IsAny<AuditEvent>()), Times.Once);
    }

    [Theory]
    [InlineData("client-kick", true)]
    [InlineData("only-kick", true)]
    [InlineData("ban-client", true)]
    [InlineData("client-kick", false)]
    [InlineData("only-kick", false)]
    [InlineData("ban-client", false)]
    public async Task ClientActions_MissingOrNegativeSlotDoesNotDispatch(string action, bool missing)
    {
        var fixture = new Fixture();
        var payload = new Dictionary<string, object> { ["reason"] = "reason" };
        if (!missing)
        {
            payload["clientId"] = -1;
        }

        var request = System.Text.Json.JsonSerializer.Deserialize<CoD4xClientReasonRequestDto>(
            System.Text.Json.JsonSerializer.Serialize(payload),
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web))!;

        var response = Assert.IsType<ObjectResult>(await ExecuteClientAction(fixture, action, request));

        Assert.Equal(400, response.StatusCode);
        fixture.Client.Verify(x => x.ClientKick(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        fixture.Client.Verify(x => x.OnlyKick(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        fixture.Client.Verify(x => x.BanClient(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        fixture.AssertNoAuditOrEvent();
    }

    [Theory]
    [InlineData("permban", "Banrecord added for player: Tester id: 2310346615957836592")]
    [InlineData("tempban", "Banrecord added for id: 2310346615957836592")]
    [InlineData("unban", "Removing ban for Nick: Tester, PlayerID: 2310346615957836592, Banreason: reason")]
    public async Task StructuredBanActions_ConfirmedResultAuditsAndEmitsEvent(string action, string nativeResponse)
    {
        var fixture = new Fixture();
        SetupBanResponse(fixture, nativeResponse);

        var response = Assert.IsType<ObjectResult>(await ExecuteBanAction(fixture, action));

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("\"IsSuccess\":true", System.Text.Json.JsonSerializer.Serialize(response.Value));
        fixture.Audit.Verify(x => x.LogAudit(It.IsAny<AuditEvent>()), Times.Once);
        fixture.Repository.Verify(x => x.GameServersEvents.V1.CreateGameServerEvent(
            It.IsAny<CreateGameServerEventDto>(), It.IsAny<CancellationToken>()), Times.Once);
        fixture.BanLifecycle.Verify(x => x.PublishBanLiftAppliedAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            action == "unban" ? Times.Once() : Times.Never());
    }

    [Theory]
    [InlineData("permban", "Error: This player can not be banned", "Error")]
    [InlineData("tempban", "Error: Did not got a valid ban time", "Error")]
    [InlineData("unban", "Error: no such ban", "Error")]
    [InlineData("permban", "attempting to add Banrecord for player: Tester id: 2310346615957836592", "Unknown")]
    [InlineData("tempban", "", "Empty")]
    [InlineData("unban", "", "NoMatch")]
    [InlineData("permban", "Removing ban for Nick: Tester, PlayerID: 2310346615957836592, Banreason: reason", "Unknown")]
    [InlineData("unban", "Banrecord added for id: 2310346615957836592", "Unknown")]
    public async Task StructuredBanActions_UnconfirmedResultDoesNotAuditOrEmitEvent(string action, string nativeResponse, string outcome)
    {
        var fixture = new Fixture();
        SetupBanResponse(fixture, nativeResponse);

        var response = Assert.IsType<ObjectResult>(await ExecuteBanAction(fixture, action));

        Assert.Equal(200, response.StatusCode);
        var json = System.Text.Json.JsonSerializer.Serialize(response.Value);
        Assert.Contains("\"IsSuccess\":false", json);
        Assert.Contains($"\"Outcome\":\"{outcome}\"", json);
        fixture.AssertNoAuditOrEvent();
        fixture.BanLifecycle.Verify(x => x.PublishBanLiftAppliedAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains(fixture.Logger.Invocations, invocation => invocation.Method.Name == nameof(ILogger.Log));
    }

    [Theory]
    [InlineData("permban")]
    [InlineData("tempban")]
    [InlineData("unban")]
    public async Task StructuredBanActions_TransportFailureDoesNotAuditOrEmitEvent(string action)
    {
        var fixture = new Fixture();
        fixture.Client.Setup(x => x.BanPlayerByPlayerIdentifier(It.IsAny<string>(), It.IsAny<string>())).ThrowsAsync(new IOException("send failed"));
        fixture.Client.Setup(x => x.TempBanPlayerByPlayerIdentifier(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>())).ThrowsAsync(new IOException("send failed"));
        fixture.Client.Setup(x => x.UnbanPlayerByPlayerIdentifier(It.IsAny<string>())).ThrowsAsync(new IOException("send failed"));

        var response = Assert.IsType<ObjectResult>(await ExecuteBanAction(fixture, action));

        Assert.Contains("RCON_OPERATION_FAILED", System.Text.Json.JsonSerializer.Serialize(response.Value));
        fixture.AssertNoAuditOrEvent();
        fixture.BanLifecycle.Verify(x => x.PublishBanLiftAppliedAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static void SetupBanResponse(Fixture fixture, string nativeResponse)
    {
        fixture.Client.Setup(x => x.BanPlayerByPlayerIdentifier(It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(nativeResponse);
        fixture.Client.Setup(x => x.TempBanPlayerByPlayerIdentifier(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>())).ReturnsAsync(nativeResponse);
        fixture.Client.Setup(x => x.UnbanPlayerByPlayerIdentifier(It.IsAny<string>())).ReturnsAsync(nativeResponse);
    }

    private static Task<IActionResult> ExecuteBanAction(Fixture fixture, string action) =>
        action switch
        {
            "permban" => fixture.Controller.PermBan(fixture.ServerId, new CoD4xPermBanRequestDto { PlayerIdentifier = "2310346615957836592" }),
            "tempban" => fixture.Controller.TempBan(fixture.ServerId, new CoD4xTempBanRequestDto { PlayerIdentifier = "2310346615957836592", DurationMinutes = 15 }),
            "unban" => fixture.Controller.Unban(fixture.ServerId, new CoD4xUnbanRequestDto { PlayerIdentifier = "2310346615957836592" }),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };

    private static Task<IActionResult> ExecuteClientAction(Fixture fixture, string action, CoD4xClientReasonRequestDto request) =>
        action switch
        {
            "client-kick" => fixture.Controller.ClientKick(fixture.ServerId, request),
            "only-kick" => fixture.Controller.OnlyKick(fixture.ServerId, request),
            "ban-client" => fixture.Controller.BanClient(fixture.ServerId, request),
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        };

    private sealed class Fixture
    {
        public Guid ServerId { get; } = Guid.NewGuid();
        public Mock<IRepositoryApiClient> Repository { get; } = new() { DefaultValue = DefaultValue.Mock };
        public Mock<ICallOfDuty4xRconClient> Client { get; }
        public Mock<IRconClient> RawClient { get; }
        public Mock<IAuditLogger> Audit { get; } = new();
        public Mock<IBanLifecycleEventPublisher> BanLifecycle { get; } = new();
        public Mock<ILogger<CoD4xRconController>> Logger { get; } = new();
        public CoD4xRconController Controller { get; }

        public Fixture()
        {
            var server = JsonConvert.DeserializeObject<GameServerDto>(JsonConvert.SerializeObject(new
            {
                GameServerId = ServerId,
                GameType = (int)GameType.CallOfDuty4x,
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
            Repository.Setup(x => x.GameServersEvents.V1.CreateGameServerEvent(It.IsAny<CreateGameServerEventDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ApiResult(HttpStatusCode.Created, new ApiResponse()));
            var client = new Mock<IRconClient>();
            RawClient = client;
            client.Setup(x => x.GetDvar("sv_consayname", It.IsAny<CancellationToken>()))
                .ReturnsAsync("\"sv_consayname\" is: \"^2Server: ^7^7\" default: \"default^7\"");
            client.Setup(x => x.GetDvar("sv_contellname", It.IsAny<CancellationToken>()))
                .ReturnsAsync("\"sv_contellname\" is: \"^5Server^7->^5PM: ^7^7\" default: \"default^7\"");
            Client = client.As<ICallOfDuty4xRconClient>();
            var factory = new Mock<IRconClientFactory>();
            factory.Setup(x => x.CreateInstance(GameType.CallOfDuty4x, ServerId, "127.0.0.1", 28960, "test"))
                .Returns(client.Object);
            var telemetry = new TelemetryClient(new TelemetryConfiguration { TelemetryChannel = new Mock<ITelemetryChannel>().Object });
            Controller = new CoD4xRconController(Logger.Object, Repository.Object, factory.Object, telemetry, Audit.Object,
                BanLifecycle.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        public void AssertNoAuditOrEvent()
        {
            Audit.Verify(x => x.LogAudit(It.IsAny<AuditEvent>()), Times.Never);
            Repository.Verify(x => x.GameServersEvents.V1.CreateGameServerEvent(
                It.IsAny<CreateGameServerEventDto>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
