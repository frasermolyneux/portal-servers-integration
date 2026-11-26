using System.Net;
using System.Text.Json;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MX.Api.Abstractions;
using MX.Observability.ApplicationInsights.Auditing;
using MX.Observability.ApplicationInsights.Auditing.Models;
using Newtonsoft.Json;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;
using XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.Configurations;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.GameServers;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Controllers;

[Trait("Category", "Unit")]
public class GameScopedMessageDeliveryTests
{
    [Fact]
    public async Task Say_AtomicHeaderIsOnlyOnTheFirstFragmentOfEachItem()
    {
        var fixture = new Fixture();
        var sent = new List<string>();
        fixture.Client.Setup(x => x.Say(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((message, _) => sent.Add(message))
            .Returns(Task.CompletedTask);
        const string header = "[BOT] ";
        var body = new string('a', 120);

        var response = Assert.IsType<StatusCodeResult>(await fixture.Controller.Say(fixture.ServerId,
            new SayRequest { Message = body, Messages = ["second"], FirstFragmentHeader = header }));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(new[] { header + new string('a', 111), new string('a', 9), header + "second" }, sent);
        fixture.Audit.Verify(x => x.LogAudit(It.Is<AuditEvent>(audit =>
            audit.Properties["FirstFragmentHeader"] == header)), Times.Once);
        fixture.Repository.Verify(x => x.GameServersEvents.V1.CreateGameServerEvent(
            It.Is<CreateGameServerEventDto>(dto =>
                dto.EventData.Contains("\"FirstFragmentHeader\":\"[BOT] \"") &&
                dto.EventData.Contains("\"Messages\":[\"" + body + "\",\"second\"]")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("", true)]
    [InlineData(" ", true)]
    public async Task Say_EmptyOrWhitespaceHeaderIsRetainedInDispatchRecords(string header, bool partialFailure)
    {
        var fixture = new Fixture();
        fixture.Client.Setup(x => x.Say(header + "first", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        if (partialFailure)
        {
            fixture.Client.Setup(x => x.Say(header + "second", It.IsAny<CancellationToken>()))
                .Throws(new IOException("response lost"));
        }
        else
        {
            fixture.Client.Setup(x => x.Say(header + "second", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        var result = await fixture.Controller.Say(fixture.ServerId,
            new SayRequest { Messages = ["first", "second"], FirstFragmentHeader = header });

        if (partialFailure)
        {
            Assert.Equal(502, Assert.IsType<ObjectResult>(result).StatusCode);
        }
        else
        {
            Assert.Equal(200, Assert.IsType<StatusCodeResult>(result).StatusCode);
        }

        var eventName = partialFailure ? "RconCod2Say.PartialDispatch" : "RconCod2Say";
        fixture.Audit.Verify(x => x.LogAudit(It.Is<AuditEvent>(audit =>
            audit.EventName == eventName && audit.Properties.ContainsKey("FirstFragmentHeader") &&
            audit.Properties["FirstFragmentHeader"] == header)), Times.Once);
        fixture.Repository.Verify(x => x.GameServersEvents.V1.CreateGameServerEvent(
            It.Is<CreateGameServerEventDto>(dto => dto.EventType == eventName &&
                dto.EventData.Contains("\"FirstFragmentHeader\":\"" + header + "\"")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Tell_AtomicHeaderKeepsAnUnbrokenBodyOnTheFirstFragment()
    {
        var fixture = new Fixture();
        var sent = new List<string>();
        fixture.Client.Setup(x => x.TellPlayer(3, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<int, string, CancellationToken>((_, message, _) => sent.Add(message))
            .ReturnsAsync("sent");
        const string header = "(PM) [BOT] ";

        var response = Assert.IsType<ObjectResult>(await fixture.Controller.Tell(fixture.ServerId,
            new CoD4xTargetMessageRequestDto
            {
                Target = "3",
                Message = new string('a', 130),
                FirstFragmentHeader = header
            }));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(new[] { header + new string('a', 106), new string('a', 24) }, sent);
        fixture.Audit.Verify(x => x.LogAudit(It.Is<AuditEvent>(audit =>
            audit.EventName == "RconCod2Tell" && audit.Properties["FirstFragmentHeader"] == header)), Times.Once);
    }

    [Theory]
    [InlineData("bad;command")]
    [InlineData("bad\"header")]
    [InlineData("bad\\header")]
    [InlineData("bad\nheader")]
    [InlineData("non-ascii-\u00e9")]
    public async Task Say_InvalidHeaderDoesNotResolveOrDispatch(string header)
    {
        var fixture = new Fixture();

        var response = Assert.IsType<ObjectResult>(await fixture.Controller.Say(fixture.ServerId,
            new SayRequest { Message = "safe", FirstFragmentHeader = header }));

        Assert.Equal(400, response.StatusCode);
        fixture.Repository.Verify(x => x.GameServers.V1.GetGameServer(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Client.Verify(x => x.Say(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Audit.Verify(x => x.LogAudit(It.IsAny<AuditEvent>()), Times.Never);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("")]
    public async Task Say_HeaderCannotReplaceARequiredBody(string body)
    {
        var fixture = new Fixture();

        var response = Assert.IsType<ObjectResult>(await fixture.Controller.Say(fixture.ServerId,
            new SayRequest { Message = body, FirstFragmentHeader = "[BOT] " }));

        Assert.Equal(400, response.StatusCode);
        fixture.Client.Verify(x => x.Say(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Say_HeaderThatLeavesNoBodySpaceDoesNotDispatch()
    {
        var fixture = new Fixture();

        var response = Assert.IsType<ObjectResult>(await fixture.Controller.Say(fixture.ServerId,
            new SayRequest { Messages = ["first", "second"], FirstFragmentHeader = new string('h', 117) }));

        Assert.Equal(400, response.StatusCode);
        fixture.Client.Verify(x => x.Say(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Audit.Verify(x => x.LogAudit(It.IsAny<AuditEvent>()), Times.Never);
    }

    [Fact]
    public async Task SourceSay_HeaderDoesNotEnableColourContinuation()
    {
        var fixture = new Fixture(GameType.Left4Dead2);
        var sent = new List<string>();
        fixture.Client.Setup(x => x.Say(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((message, _) => sent.Add(message))
            .Returns(Task.CompletedTask);

        var response = Assert.IsType<StatusCodeResult>(await fixture.SourceController.Say(fixture.ServerId,
            new SayRequest { Message = new string('a', 120), FirstFragmentHeader = "^1[BOT] " }));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(new[] { "^1[BOT] " + new string('a', 109), new string('a', 11) }, sent);
    }

    [Fact]
    public async Task Say_SplitsBeforeAppendingLaterItemsAndCarriesColour()
    {
        var fixture = new Fixture();
        var sent = new List<string>();
        fixture.Client.Setup(x => x.Say(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((message, _) => sent.Add(message))
            .Returns(Task.CompletedTask);
        var message = "^1" + new string('a', 116);

        var response = Assert.IsType<StatusCodeResult>(await fixture.Controller.Say(fixture.ServerId,
            new SayRequest { Message = message, Messages = [" second "] }));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(new[] { "^1" + new string('a', 115), "^1a", " second " }, sent);
        fixture.Audit.Verify(x => x.LogAudit(It.Is<AuditEvent>(audit => audit.EventName == "RconCod2Say")), Times.Once);
    }

    [Fact]
    public async Task SourceSay_SplitsWithoutAddingQuakeColourCodes()
    {
        var fixture = new Fixture(GameType.Left4Dead2);
        var sent = new List<string>();
        fixture.Client.Setup(x => x.Say(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((message, _) => sent.Add(message))
            .Returns(Task.CompletedTask);
        var message = "^1" + new string('a', 116);

        var response = Assert.IsType<StatusCodeResult>(await fixture.SourceController.Say(fixture.ServerId,
            new SayRequest { Message = message }));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(new[] { "^1" + new string('a', 115), "a" }, sent);
        fixture.Audit.Verify(x => x.LogAudit(It.Is<AuditEvent>(audit => audit.EventName == "RconL4d2Say")), Times.Once);
    }

    [Theory]
    [InlineData(GameType.Insurgency)]
    [InlineData(GameType.Left4Dead2)]
    [InlineData(GameType.Rust)]
    public async Task SourceBudgets_TreatColourLikeTextAtFragmentBoundaries(GameType gameType)
    {
        var fixture = new Fixture(gameType);
        var budget = await MessageChannelBudgetResolver.ResolveAsync(gameType, RuleMessageChannels.RconPublicChat,
            fixture.Client.Object, NullLogger.Instance, CancellationToken.None);

        var (items, validationError) = RconMessageBatch.Normalize(gameType, new string('a', 116) + "^1", null);
        Assert.Null(validationError);
        var (prepared, error) = RconMessageBatch.Prepare(gameType, items!, budget.Budget!);

        Assert.Null(error);
        Assert.Equal(new[] { new string('a', 116) + "^", "1" }, prepared!.Fragments.Select(fragment => fragment.Text));
    }

    [Fact]
    public async Task Tell_AcceptsOrderedMessagesWithoutASeparateSingleMessage()
    {
        var fixture = new Fixture();
        var sent = new List<string>();
        fixture.Client.Setup(x => x.TellPlayer(3, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<int, string, CancellationToken>((_, message, _) => sent.Add(message))
            .ReturnsAsync("sent");

        var response = Assert.IsType<ObjectResult>(await fixture.Controller.Tell(fixture.ServerId,
            new CoD4xTargetMessageRequestDto { Target = "3", Messages = ["first", "second"] }));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(new[] { "first", "second" }, sent);
        fixture.Audit.Verify(x => x.LogAudit(It.Is<AuditEvent>(audit => audit.EventName == "RconCod2Tell")), Times.Once);
    }

    [Fact]
    public async Task Say_ValidatesTheEntireBatchBeforeDispatch()
    {
        var fixture = new Fixture();

        var response = Assert.IsType<ObjectResult>(await fixture.Controller.Say(fixture.ServerId,
            new SayRequest { Messages = ["safe", "invalid;command"] }));

        Assert.Equal(400, response.StatusCode);
        Assert.Contains("INVALID_REQUEST", System.Text.Json.JsonSerializer.Serialize(response.Value));
        fixture.Repository.Verify(x => x.GameServers.V1.GetGameServer(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Client.Verify(x => x.Say(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        fixture.Audit.Verify(x => x.LogAudit(It.IsAny<AuditEvent>()), Times.Never);
    }

    [Fact]
    public async Task Tell_PartialFailureReportsCountsAndDoesNotResend()
    {
        var fixture = new Fixture();
        fixture.Client.Setup(x => x.TellPlayer(3, "first", It.IsAny<CancellationToken>())).ReturnsAsync("sent");
        fixture.Client.Setup(x => x.TellPlayer(3, "second", It.IsAny<CancellationToken>())).Throws(new IOException("response lost"));

        var response = Assert.IsType<ObjectResult>(await fixture.Controller.Tell(fixture.ServerId,
            new CoD4xTargetMessageRequestDto { Target = "3", Messages = ["first", "second", "third"] }));

        Assert.Equal(502, response.StatusCode);
        var json = System.Text.Json.JsonSerializer.Serialize(response.Value);
        Assert.Contains("RCON_MESSAGE_PARTIAL_FAILURE", json);
        Assert.Contains("Dispatched 1 of 3", json);
        Assert.Contains("outcome is unknown", json);
        fixture.Client.Verify(x => x.TellPlayer(3, "first", It.IsAny<CancellationToken>()), Times.Once);
        fixture.Client.Verify(x => x.TellPlayer(3, "second", It.IsAny<CancellationToken>()), Times.Once);
        fixture.Client.Verify(x => x.TellPlayer(3, "third", It.IsAny<CancellationToken>()), Times.Never);
        fixture.Audit.Verify(x => x.LogAudit(It.Is<AuditEvent>(audit =>
            audit.EventName == "RconCod2Tell.PartialDispatch")), Times.Once);
        fixture.Repository.Verify(x => x.GameServersEvents.V1.CreateGameServerEvent(
            It.Is<CreateGameServerEventDto>(dto => dto.EventType == "RconCod2Tell.PartialDispatch"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Say_CancellationAfterDispatchStillRecordsThePartialOutcome()
    {
        var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        fixture.Controller.HttpContext.RequestAborted = cancellation.Token;
        fixture.Client.Setup(x => x.Say("first", It.IsAny<CancellationToken>()))
            .Callback(() => cancellation.Cancel()).Returns(Task.CompletedTask);

        var response = Assert.IsType<ObjectResult>(await fixture.Controller.Say(fixture.ServerId,
            new SayRequest { Messages = ["first", "second"] }));

        Assert.Equal(502, response.StatusCode);
        Assert.Contains("Dispatched 1 of 2", System.Text.Json.JsonSerializer.Serialize(response.Value));
        fixture.Client.Verify(x => x.Say("second", It.IsAny<CancellationToken>()), Times.Never);
        fixture.Repository.Verify(x => x.GameServersEvents.V1.CreateGameServerEvent(
            It.Is<CreateGameServerEventDto>(dto => dto.EventType == "RconCod2Say.PartialDispatch"),
            It.Is<CancellationToken>(token => !token.IsCancellationRequested)), Times.Once);
    }

    private sealed class Fixture
    {
        public Guid ServerId { get; } = Guid.NewGuid();
        public Mock<IRepositoryApiClient> Repository { get; } = new() { DefaultValue = DefaultValue.Mock };
        public Mock<IRconClient> Client { get; } = new();
        public Mock<IAuditLogger> Audit { get; } = new();
        public Cod2RconController Controller { get; }
        public L4d2RconController SourceController { get; }

        public Fixture(GameType gameType = GameType.CallOfDuty2)
        {
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
            Repository.Setup(x => x.GameServersEvents.V1.CreateGameServerEvent(It.IsAny<CreateGameServerEventDto>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ApiResult(HttpStatusCode.Created, new ApiResponse()));
            var factory = new Mock<IRconClientFactory>();
            factory.Setup(x => x.CreateInstance(gameType, ServerId, "127.0.0.1", 28960, "test")).Returns(Client.Object);
            var telemetry = new TelemetryClient(new TelemetryConfiguration { TelemetryChannel = new Mock<ITelemetryChannel>().Object });
            Controller = new Cod2RconController(Mock.Of<ILogger<Cod2RconController>>(), Repository.Object, factory.Object, telemetry, Audit.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            SourceController = new L4d2RconController(Mock.Of<ILogger<L4d2RconController>>(), Repository.Object, factory.Object, telemetry, Audit.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }
    }
}
