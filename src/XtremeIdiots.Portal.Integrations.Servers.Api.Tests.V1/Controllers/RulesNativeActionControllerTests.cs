using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using MX.Api.Client;
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
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.RulesEngine;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Controllers;

[Trait("Category", "Unit")]
public class RulesNativeActionControllerTests
{
    [Theory]
    [InlineData(RuleMessageChannels.RconPublicChat, "sv_consayname", "^2Server: ^7", 12)]
    [InlineData(RuleMessageChannels.RconPrivateChat, "sv_contellname", "^5Server^7->^5PM: ^7", 20)]
    public async Task MessageBudget_CoD4xReservesCurrentPrefixWithoutAuditing(string channel, string variable, string prefix, int expectedBytes)
    {
        var fixture = new Fixture(GameType.CallOfDuty4x);
        fixture.Client.Setup(x => x.GetDvar(variable, It.IsAny<CancellationToken>()))
            .ReturnsAsync($"\"{variable}\" is: \"{prefix}^7\" default: \"default^7\"");

        var result = Assert.IsType<ApiResponse<RuleMessageChannelBudgetDto>>(
            Assert.IsType<ObjectResult>(await fixture.Controller.GetMessageChannelBudget(fixture.ServerId, channel)).Value).Data!;

        Assert.Equal(RuleMessageBudgetStatus.Available, result.Status);
        Assert.Equal(149, result.Budget!.MaxPayloadBytes);
        Assert.Equal(expectedBytes, result.Budget.ReservedPrefixUtf8Bytes);
        Assert.True(result.Budget.RconSafePrintableAscii);
        Assert.True(result.Constraints!.DisallowLocalizationInserts);
        Assert.Equal(";\"\\", result.Constraints.DisallowedCharacters);
        Assert.Contains("localization", result.Budget.Evidence!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fixture.Audit.Invocations);
        fixture.Repository.Verify(x => x.GameServersEvents.V1.CreateGameServerEvent(
            It.IsAny<CreateGameServerEventDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(GameType.CallOfDuty2)]
    [InlineData(GameType.CallOfDuty4)]
    [InlineData(GameType.CallOfDuty5)]
    public async Task MessageBudget_OtherGamesExposeTheInterimPolicy(GameType gameType)
    {
        var fixture = new Fixture(gameType);

        var result = Assert.IsType<ApiResponse<RuleMessageChannelBudgetDto>>(
            Assert.IsType<ObjectResult>(await fixture.Controller.GetMessageChannelBudget(fixture.ServerId, RuleMessageChannels.RconPublicChat)).Value).Data!;

        Assert.Equal(RuleMessageBudgetStatus.Available, result.Status);
        Assert.Equal(149, result.Budget!.MaxPayloadBytes);
        Assert.Equal(32, result.Budget.ReservedPrefixUtf8Bytes);
        Assert.True(result.Budget.RconSafePrintableAscii);
        Assert.False(result.Constraints!.DisallowLocalizationInserts);
        Assert.Contains("not independently verified", result.Budget.Evidence!);
    }

    [Theory]
    [InlineData("\"sv_consayname\" is: \"bad&&1^7\" default: \"default^7\"")]
    [InlineData("\"sv_consayname\" is: \"bad\\prefix^7\" default: \"default^7\"")]
    [InlineData("unparseable")]
    public async Task MessageBudget_UnsafeOrUnparseablePrefixIsUnavailable(string response)
    {
        var fixture = new Fixture(GameType.CallOfDuty4x);
        fixture.Client.Setup(x => x.GetDvar("sv_consayname", It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var result = Assert.IsType<ApiResponse<RuleMessageChannelBudgetDto>>(
            Assert.IsType<ObjectResult>(await fixture.Controller.GetMessageChannelBudget(fixture.ServerId, RuleMessageChannels.RconPublicChat)).Value).Data!;

        Assert.Equal(RuleMessageBudgetStatus.Unavailable, result.Status);
        Assert.Null(result.Budget);
    }

    [Fact]
    public async Task MessageBudget_UnsupportedChannelDoesNotQueryServer()
    {
        var fixture = new Fixture(GameType.CallOfDuty4x);

        var result = Assert.IsType<ApiResponse<RuleMessageChannelBudgetDto>>(
            Assert.IsType<ObjectResult>(await fixture.Controller.GetMessageChannelBudget(fixture.ServerId, "unknown")).Value).Data!;

        Assert.Equal(RuleMessageBudgetStatus.Unsupported, result.Status);
        Assert.Null(result.Budget);
        fixture.Repository.Verify(x => x.GameServers.V1.GetGameServer(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

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

    [Theory]
    [InlineData(GameType.CallOfDuty2)]
    [InlineData(GameType.CallOfDuty4)]
    [InlineData(GameType.CallOfDuty5)]
    [InlineData(GameType.CallOfDuty4x)]
    public async Task Kick_WithReason_DispatchesSlotOnlyAndPreservesReason(GameType gameType)
    {
        const string reason = "Configured reason";
        var fixture = new Fixture(gameType);
        CreateGameServerEventDto? createdEvent = null;
        fixture.Repository.Setup(x => x.GameServersEvents.V1.CreateGameServerEvent(
                It.IsAny<CreateGameServerEventDto>(),
                It.IsAny<CancellationToken>()))
            .Callback<CreateGameServerEventDto, CancellationToken>((dto, _) => createdEvent = dto)
            .ReturnsAsync(new ApiResult(HttpStatusCode.Created, new ApiResponse()));
        fixture.Client.As<IRulesNativeRconClient>()
            .Setup(x => x.DispatchKick(3, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await fixture.Controller.Execute(fixture.ServerId, fixture.Command(RuleNativeEffectType.DropPlayer) with
        {
            LogicalEffectId = "effect",
            Effect = new RuleNativeEffect { Type = RuleNativeEffectType.DropPlayer, Reason = reason }
        });

        Assert.Equal("Dispatched", Outcome(result));
        fixture.Client.As<IRulesNativeRconClient>()
            .Verify(x => x.DispatchKick(3, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Single(fixture.Audit.Invocations);
        Assert.Contains(reason, JsonConvert.SerializeObject(fixture.Audit.Invocations.Single().Arguments[0]));
        Assert.NotNull(createdEvent);
        Assert.Equal("NativeAction.DropPlayer.Rcon.Dispatched", createdEvent.EventType);
        Assert.Equal(Guid.Parse("9a894997-6307-2788-bb0a-82e5c6a00a53"), createdEvent.IdempotencyKey);

        using var eventData = JsonDocument.Parse(createdEvent.EventData);
        var root = eventData.RootElement;
        Assert.Equal("DropPlayer", root.GetProperty("effect").GetString());
        Assert.Equal("Rcon", root.GetProperty("source").GetString());
        Assert.Equal("Dispatched", root.GetProperty("outcome").GetString());
        Assert.True(root.GetProperty("actionType").ValueKind == JsonValueKind.Null);
        Assert.Equal("abc", root.GetProperty("playerGuid").GetString());
        Assert.Equal(3, root.GetProperty("slotId").GetInt32());
        Assert.Equal(reason, root.GetProperty("reason").GetString());
        Assert.True(root.GetProperty("adminActionId").ValueKind == JsonValueKind.Null);
    }

    [Fact]
    public async Task UnsupportedGame_DoesNotDispatch()
    {
        var fixture = new Fixture(GameType.Rust);
        var result = await fixture.Controller.Execute(fixture.ServerId, fixture.Command(RuleNativeEffectType.PublicMessage, "hello"));
        Assert.Equal("SkippedUnsupported", Outcome(result));
        Assert.Empty(fixture.Audit.Invocations);
        fixture.Repository.Verify(x => x.GameServersEvents.V1.CreateGameServerEvent(
                It.IsAny<CreateGameServerEventDto>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PublicMessage_DispatchesAndWritesAudit()
    {
        var fixture = new Fixture(GameType.CallOfDuty2);
        CreateGameServerEventDto? createdEvent = null;
        fixture.Repository.Setup(x => x.GameServersEvents.V1.CreateGameServerEvent(
                It.IsAny<CreateGameServerEventDto>(),
                It.IsAny<CancellationToken>()))
            .Callback<CreateGameServerEventDto, CancellationToken>((dto, _) => createdEvent = dto)
            .ReturnsAsync(new ApiResult(HttpStatusCode.Created, new ApiResponse()));
        fixture.Client.As<IRulesNativeRconClient>()
            .Setup(x => x.DispatchPublicMessage("hello", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var result = await fixture.Controller.Execute(fixture.ServerId, fixture.Command(RuleNativeEffectType.PublicMessage, "hello"));
        Assert.Equal("Dispatched", Outcome(result));
        var response = Assert.IsType<ApiResponse<RuleActionResult>>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal(fixture.ServerId, response.Data!.GameServerId);
        fixture.Client.As<IRulesNativeRconClient>().Verify(x => x.DispatchPublicMessage("hello", It.IsAny<CancellationToken>()), Times.Once);
        Assert.Single(fixture.Audit.Invocations);
        Assert.NotNull(createdEvent);
        Assert.Equal("NativeAction.PublicMessage.Rcon.Dispatched", createdEvent.EventType);
        using var eventData = JsonDocument.Parse(createdEvent.EventData);
        Assert.Equal("PublicMessage", eventData.RootElement.GetProperty("effect").GetString());
        Assert.Equal("hello", eventData.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task PublicMessage_CancelledAfterDispatchStillWritesOperatorEvent()
    {
        var fixture = new Fixture(GameType.CallOfDuty2);
        using var cancellation = new CancellationTokenSource();
        fixture.Controller.HttpContext.RequestAborted = cancellation.Token;
        fixture.Client.As<IRulesNativeRconClient>()
            .Setup(x => x.DispatchPublicMessage("hello", It.IsAny<CancellationToken>()))
            .Callback(() => cancellation.Cancel())
            .Returns(Task.CompletedTask);
        fixture.Repository.Setup(x => x.GameServersEvents.V1.CreateGameServerEvent(
                It.IsAny<CreateGameServerEventDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult(HttpStatusCode.Created, new ApiResponse()));

        var result = await fixture.Controller.Execute(fixture.ServerId, fixture.Command(RuleNativeEffectType.PublicMessage, "hello"));

        Assert.Equal("Dispatched", Outcome(result));
        Assert.Single(fixture.Audit.Invocations);
        fixture.Repository.Verify(x => x.GameServersEvents.V1.CreateGameServerEvent(
            It.IsAny<CreateGameServerEventDto>(),
            It.Is<CancellationToken>(token => !token.IsCancellationRequested)), Times.Once);
    }

    [Fact]
    public async Task Kick_WhenEventWriteConflicts_LogsFailureAndDoesNotRetryDispatch()
    {
        var fixture = new Fixture(GameType.CallOfDuty5);
        var conflict = new ApiHttpRequestException("Conflict", HttpStatusCode.Conflict, new ApiResponse());
        fixture.Client.As<IRulesNativeRconClient>()
            .Setup(x => x.DispatchKick(3, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        fixture.Repository.Setup(x => x.GameServersEvents.V1.CreateGameServerEvent(
                It.IsAny<CreateGameServerEventDto>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(conflict);

        var result = await fixture.Controller.Execute(fixture.ServerId, fixture.Command(RuleNativeEffectType.DropPlayer));

        var response = Assert.IsType<ApiResponse<RuleActionResult>>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal(RuleActionExecutionOutcome.Dispatched, response.Data!.Outcome);
        Assert.Contains("persistence failed; do not retry", response.Data.Detail, StringComparison.OrdinalIgnoreCase);
        fixture.Client.As<IRulesNativeRconClient>()
            .Verify(x => x.DispatchKick(3, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Repository.Verify(x => x.GameServersEvents.V1.CreateGameServerEvent(
                It.IsAny<CreateGameServerEventDto>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        fixture.Logger.Verify(logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("Failed to write rules action event", StringComparison.Ordinal)),
                It.Is<Exception?>(exception => exception == conflict),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Kick_WhenAuditWriteFails_LogsFailureAndStillWritesEventWithoutRetryingDispatch()
    {
        var fixture = new Fixture(GameType.CallOfDuty5);
        var auditException = new InvalidOperationException("Audit store unavailable");
        fixture.Audit.Setup(x => x.LogAudit(It.IsAny<AuditEvent>()))
            .Throws(auditException);
        fixture.Client.As<IRulesNativeRconClient>()
            .Setup(x => x.DispatchKick(3, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        fixture.Repository.Setup(x => x.GameServersEvents.V1.CreateGameServerEvent(
                It.IsAny<CreateGameServerEventDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult(HttpStatusCode.Created, new ApiResponse()));

        var result = await fixture.Controller.Execute(fixture.ServerId, fixture.Command(RuleNativeEffectType.DropPlayer));

        var response = Assert.IsType<ApiResponse<RuleActionResult>>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal(RuleActionExecutionOutcome.Dispatched, response.Data!.Outcome);
        Assert.Contains("persistence failed; do not retry", response.Data.Detail, StringComparison.OrdinalIgnoreCase);
        fixture.Client.As<IRulesNativeRconClient>()
            .Verify(x => x.DispatchKick(3, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Repository.Verify(x => x.GameServersEvents.V1.CreateGameServerEvent(
                It.IsAny<CreateGameServerEventDto>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        fixture.Logger.Verify(logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("Failed to write rules action audit", StringComparison.Ordinal)),
                It.Is<Exception?>(exception => exception == auditException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Kick_WhenEventWriteReturnsFailure_LogsFailureAndDoesNotRetryDispatch()
    {
        var fixture = new Fixture(GameType.CallOfDuty5);
        fixture.Client.As<IRulesNativeRconClient>()
            .Setup(x => x.DispatchKick(3, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        fixture.Repository.Setup(x => x.GameServersEvents.V1.CreateGameServerEvent(
                It.IsAny<CreateGameServerEventDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult(HttpStatusCode.InternalServerError, new ApiResponse()));

        var result = await fixture.Controller.Execute(fixture.ServerId, fixture.Command(RuleNativeEffectType.DropPlayer));

        var response = Assert.IsType<ApiResponse<RuleActionResult>>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal(RuleActionExecutionOutcome.Dispatched, response.Data!.Outcome);
        Assert.Contains("persistence failed; do not retry", response.Data.Detail, StringComparison.OrdinalIgnoreCase);
        fixture.Client.As<IRulesNativeRconClient>()
            .Verify(x => x.DispatchKick(3, It.IsAny<CancellationToken>()), Times.Once);
        fixture.Repository.Verify(x => x.GameServersEvents.V1.CreateGameServerEvent(
                It.IsAny<CreateGameServerEventDto>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        fixture.Logger.Verify(logger => logger.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("Repository returned", StringComparison.Ordinal)),
                It.Is<Exception?>(exception => exception == null),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
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
    [InlineData(129, "WouldExecute")]
    [InlineData(130, "SkippedUnsupported")]
    public async Task PrivateMessage_RechecksFrozenFragmentAgainstCurrentPrefix(int messageLength, string expected)
    {
        var fixture = new Fixture(GameType.CallOfDuty4x);
        fixture.Client.Setup(x => x.GetDvar("sv_contellname", It.IsAny<CancellationToken>()))
            .ReturnsAsync("\"sv_contellname\" is: \"^5Server^7->^5PM: ^7^7\" default: \"default^7\"");
        var identity = new RuleDeliveryIdentity { ParentLogicalEffectId = new string('a', 64) };
        var command = fixture.Command(RuleNativeEffectType.PrivateMessage, new string('x', messageLength)) with
        {
            Mode = RuleExecutionMode.DryRun,
            LogicalEffectId = identity.ToLogicalEffectId(),
            DeliveryIdentity = identity
        };

        var result = await fixture.Controller.Execute(fixture.ServerId, command);

        Assert.Equal(expected, Outcome(result));
        fixture.Client.As<IRulesNativeRconClient>().Verify(
            x => x.DispatchPrivateMessage(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CoD4xMessage_RejectsLocalizationInsertion()
    {
        var fixture = new Fixture(GameType.CallOfDuty4x);

        var result = await fixture.Controller.Execute(fixture.ServerId,
            fixture.Command(RuleNativeEffectType.PublicMessage, "hello &&1"));

        Assert.Equal("SkippedUnsupported", Outcome(result));
        fixture.Client.As<IRulesNativeRconClient>().Verify(
            x => x.DispatchPublicMessage(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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
        fixture.Repository.Verify(x => x.GameServersEvents.V1.CreateGameServerEvent(
                It.IsAny<CreateGameServerEventDto>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
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
    [InlineData("matching", "Dispatched")]
    [InlineData("missing-binding", "Failed")]
    [InlineData("reconnected", "Stale")]
    [InlineData("replaced-slot", "Stale")]
    [InlineData("missing-roster", "Failed")]
    public async Task PrivateMessage_ValidatesRecipientIndependentlyOfTrigger(string scenario, string expected)
    {
        var fixture = new Fixture(GameType.CallOfDuty4);
        var recipientConnectionId = Guid.NewGuid();
        var recipientBinding = fixture.Binding with
        {
            SlotId = 7,
            PlayerGuid = "recipient",
            ConnectionId = scenario == "reconnected" ? Guid.NewGuid() : recipientConnectionId
        };
        fixture.Repository.Setup(x => x.RuleConnectionBindings.V1.GetCurrentBinding(
                fixture.ServerId, 7, "recipient", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<RuleConnectionBindingDto>(HttpStatusCode.OK,
                new ApiResponse<RuleConnectionBindingDto>(scenario == "missing-binding" ? null : recipientBinding)));
        fixture.Client.As<IPlayerRosterRconClient>()
            .Setup(x => x.GetPlayersWithRoster(It.IsAny<CancellationToken>()))
            .Returns((new List<IRconPlayer>(), new RconRosterObservationDto
            {
                State = RconRosterState.Partial,
                Players = scenario == "missing-roster" ? [] :
                    [new() { Slot = 7, PlayerIdentifier = scenario == "replaced-slot" ? "other" : "recipient" }]
            }));
        fixture.Client.As<IRulesNativeRconClient>()
            .Setup(x => x.DispatchPrivateMessage(7, "hello", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var delivery = new RuleDeliveryIdentity
        {
            ParentLogicalEffectId = new string('a', 64),
            RecipientConnectionId = recipientConnectionId
        };
        var command = fixture.Command(RuleNativeEffectType.PrivateMessage, "hello") with
        {
            LogicalEffectId = delivery.ToLogicalEffectId(),
            RecipientTarget = new RuleMessageRecipientTarget
            {
                GameServerId = fixture.ServerId,
                Runtime = fixture.Binding.Runtime,
                SlotId = 7,
                PlayerGuid = "recipient",
                ConnectionId = recipientConnectionId
            },
            DeliveryIdentity = delivery
        };

        var result = await fixture.Controller.Execute(fixture.ServerId, command);

        Assert.Equal(expected, Outcome(result));
        fixture.Client.As<IRulesNativeRconClient>().Verify(
            x => x.DispatchPrivateMessage(7, "hello", It.IsAny<CancellationToken>()),
            scenario == "matching" ? Times.Once() : Times.Never());
        fixture.Repository.Verify(x => x.RuleConnectionBindings.V1.GetCurrentBinding(
            fixture.ServerId, 3, "abc", It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DropPlayer_RejectsSeparateRecipient()
    {
        var fixture = new Fixture(GameType.CallOfDuty4);
        var command = fixture.Command(RuleNativeEffectType.DropPlayer) with
        {
            RecipientTarget = new RuleMessageRecipientTarget
            {
                GameServerId = fixture.ServerId,
                SlotId = 7,
                PlayerGuid = "recipient",
                ConnectionId = Guid.NewGuid(),
                Runtime = fixture.Binding.Runtime
            }
        };

        var result = await fixture.Controller.Execute(fixture.ServerId, command);

        Assert.IsType<BadRequestObjectResult>(result);
        fixture.Client.As<IRulesNativeRconClient>().Verify(
            x => x.DispatchKick(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PrivateMessage_RejectsAlteredDeliveryIdentity()
    {
        var fixture = new Fixture(GameType.CallOfDuty4);
        var command = fixture.Command(RuleNativeEffectType.PrivateMessage, "hello") with
        {
            DeliveryIdentity = new RuleDeliveryIdentity { ParentLogicalEffectId = new string('a', 64), FragmentIndex = 1 }
        };

        var result = await fixture.Controller.Execute(fixture.ServerId, command);

        Assert.IsType<BadRequestObjectResult>(result);
        fixture.Client.As<IRulesNativeRconClient>().Verify(
            x => x.DispatchPrivateMessage(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
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
        public Mock<ILogger<RulesNativeActionController>> Logger { get; } = new();
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
            Controller = new RulesNativeActionController(Logger.Object, Repository.Object, factory.Object, Audit.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
        }

        public void SetBinding(RuleConnectionBindingDto? binding) =>
            Repository.Setup(x => x.RuleConnectionBindings.V1.GetCurrentBinding(ServerId, 3, "abc", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ApiResult<RuleConnectionBindingDto>(HttpStatusCode.OK, new ApiResponse<RuleConnectionBindingDto>(binding)));
    }
}
