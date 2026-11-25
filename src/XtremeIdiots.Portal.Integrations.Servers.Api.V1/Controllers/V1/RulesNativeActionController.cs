using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MX.Api.Abstractions;
using MX.Api.Web.Extensions;
using MX.Observability.ApplicationInsights.Auditing;
using MX.Observability.ApplicationInsights.Auditing.Models;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.GameServers;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.RulesEngine;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1;

[ApiController]
[Authorize(Roles = "ServiceAccount")]
[ApiVersion(ApiVersions.V1)]
[Route("v{version:apiVersion}/rcon/{gameServerId}/rules-action")]
public class RulesNativeActionController(
    ILogger<RulesNativeActionController> logger,
    IRepositoryApiClient repository,
    IRconClientFactory factory,
    IAuditLogger auditLogger) : Controller
{
    [HttpGet("message-budget")]
    public async Task<IActionResult> GetMessageChannelBudget(Guid gameServerId, [FromQuery] string? channel)
    {
        var ct = HttpContext.RequestAborted;
        if (channel is not (RuleMessageChannels.RconPublicChat or RuleMessageChannels.RconPrivateChat))
        {
            return BudgetResult(new RuleMessageChannelBudgetDto
            {
                Status = RuleMessageBudgetStatus.Unsupported,
                Detail = "Only public and private RCON chat are supported by the native rules adapter."
            });
        }

        var serverResult = await repository.GameServers.V1.GetGameServer(gameServerId, ct).ConfigureAwait(false);
        var server = serverResult.Result?.Data;
        if (server is null)
        {
            return BudgetResult(new RuleMessageChannelBudgetDto
            {
                Status = RuleMessageBudgetStatus.Unavailable,
                Detail = "Game server could not be resolved."
            });
        }

        if (server.GameType is not (GameType.CallOfDuty2 or GameType.CallOfDuty4 or GameType.CallOfDuty5 or GameType.CallOfDuty4x))
        {
            return BudgetResult(new RuleMessageChannelBudgetDto
            {
                Status = RuleMessageBudgetStatus.Unsupported,
                Detail = "This game has no supported native rules adapter."
            });
        }

        var config = await repository.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon", ct).ConfigureAwait(false);
        var password = RconConfigResolver.ParsePasswordFromConfig(config?.Result?.Data?.Configuration);
        if (string.IsNullOrWhiteSpace(password))
        {
            return BudgetResult(new RuleMessageChannelBudgetDto
            {
                Status = RuleMessageBudgetStatus.Unavailable,
                Detail = "RCON credentials are unavailable."
            });
        }

        var client = factory.CreateInstance(server.GameType, gameServerId, server.Hostname, server.QueryPort, password);
        if (client is not IRulesNativeRconClient)
        {
            return BudgetResult(new RuleMessageChannelBudgetDto
            {
                Status = RuleMessageBudgetStatus.Unsupported,
                Detail = "The configured RCON client does not support native rules actions."
            });
        }

        var budget = await MessageChannelBudgetResolver.ResolveAsync(server.GameType, channel, client, logger, ct).ConfigureAwait(false);
        return BudgetResult(budget);
    }

    private static IActionResult BudgetResult(RuleMessageChannelBudgetDto budget) =>
        new ApiResponse<RuleMessageChannelBudgetDto>(budget).ToApiResult().ToHttpResult();

    [HttpPost]
    public async Task<IActionResult> Execute(Guid gameServerId, [FromBody] RuleActionCommand? request)
    {
        var ct = HttpContext.RequestAborted;
        if (request is null || request.Effect is null || request.Correlation is null ||
            request.ProtocolVersion != 1 || request.GameServerId != gameServerId ||
            request.ExecutionId == Guid.Empty || request.ConnectionId == Guid.Empty ||
            request.Correlation.ConnectionId != request.ConnectionId ||
            string.IsNullOrWhiteSpace(request.ActionId) || string.IsNullOrWhiteSpace(request.LogicalEffectId) ||
            request.Mode is not (RuleExecutionMode.Enforce or RuleExecutionMode.DryRun))
        {
            return BadRequest("The rules action command is invalid.");
        }

        IActionResult Result(RuleActionExecutionOutcome outcome, string detail, DateTimeOffset? occurredAtUtc = null) =>
            new ApiResponse<RuleActionResult>(new RuleActionResult
            {
                ExecutionId = request.ExecutionId,
                GameServerId = gameServerId,
                ActionId = request.ActionId,
                LogicalEffectId = request.LogicalEffectId,
                Correlation = request.Correlation,
                Outcome = outcome,
                OccurredAtUtc = occurredAtUtc ?? DateTimeOffset.UtcNow,
                Detail = detail
            }).ToApiResult().ToHttpResult();

        if (request.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return Result(RuleActionExecutionOutcome.Stale, "The action command has expired.");
        }

        if (request.Effect.Type is not (RuleNativeEffectType.DropPlayer or RuleNativeEffectType.PublicMessage or RuleNativeEffectType.PrivateMessage))
        {
            return Result(RuleActionExecutionOutcome.SkippedUnsupported, "The requested native effect is not supported.");
        }

        if ((request.Effect.Type == RuleNativeEffectType.DropPlayer && request.Effect.Message is not null) ||
            (request.Effect.Type is RuleNativeEffectType.PublicMessage or RuleNativeEffectType.PrivateMessage && request.Effect.Reason is not null))
        {
            return BadRequest("The native effect contains unexpected parameters.");
        }

        if (request.RecipientTarget is not null && request.Effect.Type != RuleNativeEffectType.PrivateMessage)
        {
            return BadRequest("A recipient target is only supported for private messages.");
        }

        if (request.DeliveryIdentity is { } delivery &&
            (request.Effect.Type is not (RuleNativeEffectType.PublicMessage or RuleNativeEffectType.PrivateMessage) ||
             delivery.ParentLogicalEffectId is not { Length: 64 } || !delivery.ParentLogicalEffectId.All(Uri.IsHexDigit) ||
             delivery.MessageIndex < 0 || delivery.FragmentIndex < 0 ||
             delivery.RecipientConnectionId != request.RecipientTarget?.ConnectionId ||
             !string.Equals(delivery.ToLogicalEffectId(), request.LogicalEffectId, StringComparison.Ordinal)))
        {
            return BadRequest("The message delivery identity is invalid.");
        }

        if (request.SlotId < 0 || string.IsNullOrWhiteSpace(request.PlayerGuid))
        {
            return BadRequest("A non-negative slot and player identifier are required.");
        }

        if (request.Runtime is null || request.Runtime.Id != "server-agent" ||
            request.Runtime.IdentityModel != RuleRuntimeIdentityModel.AgentObservedConnection ||
            request.ConnectionGeneration is not null)
        {
            return Result(RuleActionExecutionOutcome.SkippedUnsupported, "Only agent-observed connections can use the RCON adapter.");
        }

        if (request.Runtime.ProtocolVersion != 1 || request.Runtime.InstanceId == Guid.Empty ||
            request.Runtime.StartedAtUtc == default || string.IsNullOrWhiteSpace(request.Runtime.Version))
        {
            return BadRequest("The agent runtime identity is invalid.");
        }

        var recipient = request.RecipientTarget;
        if (recipient is not null &&
            (recipient.GameServerId != gameServerId || recipient.ConnectionId == Guid.Empty ||
             recipient.SlotId < 0 || string.IsNullOrWhiteSpace(recipient.PlayerGuid) ||
             recipient.Runtime is null))
        {
            return BadRequest("The private message recipient is invalid.");
        }

        if (recipient is not null &&
            (recipient.Runtime.Id != "server-agent" ||
             recipient.Runtime.IdentityModel != RuleRuntimeIdentityModel.AgentObservedConnection ||
             recipient.ConnectionGeneration is not null))
        {
            return Result(RuleActionExecutionOutcome.SkippedUnsupported, "Only agent-observed recipients can use the RCON adapter.");
        }

        if (recipient is not null &&
            (recipient.Runtime.ProtocolVersion != 1 || recipient.Runtime.InstanceId == Guid.Empty ||
             recipient.Runtime.StartedAtUtc == default || string.IsNullOrWhiteSpace(recipient.Runtime.Version) ||
             request.DeliveryIdentity is null ||
             request.DeliveryIdentity.RecipientConnectionId != recipient.ConnectionId))
        {
            return BadRequest("The private message recipient identity is invalid.");
        }

        var targetSlot = recipient?.SlotId ?? request.SlotId;
        var targetGuid = recipient?.PlayerGuid ?? request.PlayerGuid;
        var targetConnectionId = recipient?.ConnectionId ?? request.ConnectionId;
        var targetRuntime = recipient?.Runtime ?? request.Runtime;

        if (request.Effect.Type is RuleNativeEffectType.PublicMessage or RuleNativeEffectType.PrivateMessage &&
            (string.IsNullOrEmpty(request.Effect.Message) ||
             (request.DeliveryIdentity is null && string.IsNullOrWhiteSpace(request.Effect.Message)) ||
             request.Effect.Message.Any(c => c < 32 || c == 127)))
        {
            return BadRequest("A non-empty message without control characters is required.");
        }

        var serverResult = await repository.GameServers.V1.GetGameServer(gameServerId, ct).ConfigureAwait(false);
        var server = serverResult.Result?.Data;
        if (server is null)
        {
            return Result(RuleActionExecutionOutcome.Failed, "Game server could not be resolved.");
        }

        if (server.GameType is not (GameType.CallOfDuty2 or GameType.CallOfDuty4 or GameType.CallOfDuty5 or GameType.CallOfDuty4x))
        {
            return Result(RuleActionExecutionOutcome.SkippedUnsupported, "This game has no supported native rules adapter.");
        }

        if (request.Effect.Type is RuleNativeEffectType.PublicMessage or RuleNativeEffectType.PrivateMessage &&
            !MessageChannelBudgetResolver.IsRepresentable(server.GameType, request.Effect.Message!))
        {
            return Result(RuleActionExecutionOutcome.SkippedUnsupported, "This RCON adapter cannot safely represent the requested message.");
        }

        var bindingResult = await repository.RuleConnectionBindings.V1.GetCurrentBinding(gameServerId, targetSlot, targetGuid, ct).ConfigureAwait(false);
        var binding = bindingResult.Result?.Data;
        if (binding is null)
        {
            return Result(RuleActionExecutionOutcome.Failed, "A current agent-observed connection binding is unavailable.");
        }

        var now = DateTimeOffset.UtcNow;
        if (binding.ObservedAtUtc < now.AddMinutes(-2) || binding.ObservedAtUtc > now.AddSeconds(30) ||
            binding.ExpiresAtUtc <= now)
        {
            return Result(RuleActionExecutionOutcome.Failed, "The agent-observed connection binding is not fresh.");
        }

        if (binding.GameServerId != gameServerId || binding.GameType != server.GameType ||
            binding.SlotId != targetSlot ||
            !string.Equals(binding.PlayerGuid, targetGuid, StringComparison.OrdinalIgnoreCase) ||
            binding.ConnectionId != targetConnectionId || binding.ConnectionGeneration is not null ||
            binding.Runtime is null || binding.Runtime.Id != targetRuntime.Id ||
            binding.Runtime.IdentityModel != RuleRuntimeIdentityModel.AgentObservedConnection ||
            binding.Runtime.Version != targetRuntime.Version ||
            binding.Runtime.ProtocolVersion != targetRuntime.ProtocolVersion ||
            binding.Runtime.InstanceId != targetRuntime.InstanceId ||
            binding.Runtime.StartedAtUtc != targetRuntime.StartedAtUtc)
        {
            return Result(RuleActionExecutionOutcome.Stale, "The agent-observed connection binding no longer matches the command.");
        }

        var config = await repository.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon", ct).ConfigureAwait(false);
        var password = RconConfigResolver.ParsePasswordFromConfig(config?.Result?.Data?.Configuration);
        if (string.IsNullOrWhiteSpace(password))
        {
            return Result(RuleActionExecutionOutcome.Failed, "RCON credentials are unavailable.");
        }

        var client = factory.CreateInstance(server.GameType, gameServerId, server.Hostname, server.QueryPort, password);
        if (client is not IRulesNativeRconClient native || client is not IPlayerRosterRconClient rosterClient)
        {
            return Result(RuleActionExecutionOutcome.SkippedUnsupported, "The configured RCON client does not support native rules actions.");
        }

        if (request.Effect.Type is RuleNativeEffectType.PublicMessage or RuleNativeEffectType.PrivateMessage)
        {
            var channel = request.Effect.Type == RuleNativeEffectType.PublicMessage
                ? RuleMessageChannels.RconPublicChat
                : RuleMessageChannels.RconPrivateChat;
            var budget = await MessageChannelBudgetResolver.ResolveAsync(server.GameType, channel, client, logger, ct).ConfigureAwait(false);
            if (budget.Status != RuleMessageBudgetStatus.Available || budget.Budget is null)
            {
                var outcome = budget.Status == RuleMessageBudgetStatus.Unsupported
                    ? RuleActionExecutionOutcome.SkippedUnsupported
                    : RuleActionExecutionOutcome.Failed;
                return Result(outcome, budget.Detail ?? "Message channel budget is unavailable.");
            }

            if (Encoding.UTF8.GetByteCount(request.Effect.Message!) + budget.Budget.ReservedPrefixUtf8Bytes > budget.Budget.MaxPayloadBytes)
            {
                return Result(RuleActionExecutionOutcome.SkippedUnsupported, "The frozen message fragment exceeds the current channel budget.");
            }
        }

        {
            RconRosterObservationDto roster;
            try
            {
                (_, roster) = rosterClient.GetPlayersWithRoster(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Rules action roster check failed for server {GameServerId}", gameServerId);
                return Result(RuleActionExecutionOutcome.Failed, "Unable to validate the current player roster.");
            }

            if (roster.State is RconRosterState.Failed or RconRosterState.Unknown ||
                roster.UnparsedRowCount != 0 || roster.MissingIdentityCount != 0)
            {
                return Result(RuleActionExecutionOutcome.Failed, "The current player roster is incomplete.");
            }

            var matches = roster.Players.Where(p => p.Slot == targetSlot).ToArray();
            if (matches.Length > 1)
            {
                return Result(RuleActionExecutionOutcome.Failed, "The player slot is ambiguous.");
            }

            if (matches.Length == 0)
            {
                return Result(RuleActionExecutionOutcome.Failed, "The partial roster does not establish whether the player is present.");
            }

            if (!string.Equals(matches[0].PlayerIdentifier, targetGuid, StringComparison.OrdinalIgnoreCase))
            {
                return Result(RuleActionExecutionOutcome.Stale, "The player no longer occupies the expected slot.");
            }

            if (roster.Players.Count(p => string.Equals(p.PlayerIdentifier, targetGuid, StringComparison.OrdinalIgnoreCase)) != 1)
            {
                return Result(RuleActionExecutionOutcome.Failed, "The player identifier is ambiguous.");
            }
        }

        if (request.ExpiresAtUtc <= DateTimeOffset.UtcNow)
        {
            return Result(RuleActionExecutionOutcome.Stale, "The action command expired during validation.");
        }
        now = DateTimeOffset.UtcNow;
        if (binding.ExpiresAtUtc <= now || binding.ObservedAtUtc < now.AddMinutes(-2))
        {
            return Result(RuleActionExecutionOutcome.Failed, "The agent-observed connection binding expired during validation.");
        }
        if (request.Mode == RuleExecutionMode.DryRun)
        {
            return Result(RuleActionExecutionOutcome.WouldExecute, "The native effect was not dispatched.");
        }
        ct.ThrowIfCancellationRequested();
        try
        {
            switch (request.Effect.Type)
            {
                case RuleNativeEffectType.DropPlayer:
                    await native.DispatchKick(request.SlotId, ct).ConfigureAwait(false);
                    break;
                case RuleNativeEffectType.PublicMessage:
                    await native.DispatchPublicMessage(request.Effect.Message!, ct).ConfigureAwait(false);
                    break;
                case RuleNativeEffectType.PrivateMessage:
                    await native.DispatchPrivateMessage(targetSlot, request.Effect.Message!, ct).ConfigureAwait(false);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Rules action dispatch outcome is unknown for server {GameServerId}", gameServerId);
            return Result(RuleActionExecutionOutcome.OutcomeUnknown, "The native command may have been sent; do not retry blindly.");
        }

        var occurredAtUtc = DateTimeOffset.UtcNow;
        var eventType = $"NativeAction.{request.Effect.Type}.Rcon.Dispatched";
        var auditOperation = $"RulesNativeAction.{request.Effect.Type}.Dispatched";
        var auditBuilder = AuditEvent.ServerAction(auditOperation, AuditAction.Moderate)
            .WithGameContext(server.GameType.ToString(), gameServerId)
            .WithSource(nameof(RulesNativeActionController))
            .WithTarget(targetGuid, "Player")
            .WithProperty("SlotId", targetSlot.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .WithProperty("ExecutionId", request.ExecutionId.ToString())
            .WithProperty("LogicalEffectId", request.LogicalEffectId)
            .WithProperty("FeatureId", request.Correlation.FeatureId)
            .WithProperty("RuleId", request.Correlation.RuleId);
        if (recipient is not null)
        {
            auditBuilder = auditBuilder
                .WithProperty("TriggerPlayerGuid", request.PlayerGuid)
                .WithProperty("TriggerSlotId", request.SlotId.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .WithProperty("RecipientConnectionId", recipient.ConnectionId.ToString());
        }
        if (!string.IsNullOrWhiteSpace(request.Effect.Reason))
        {
            auditBuilder = auditBuilder.WithProperty("Reason", request.Effect.Reason);
        }

        var auditWriteFailed = false;
        try
        {
            auditLogger.LogAudit(auditBuilder.Build());
        }
        catch (Exception ex)
        {
            auditWriteFailed = true;
            logger.LogError(ex, "Failed to write rules action audit for server {GameServerId}, execution {ExecutionId}", gameServerId, request.ExecutionId);
        }

        const string dispatchDetail = "UDP command sent without confirmation; connection generation was not validated.";
        var data = JsonSerializer.Serialize(new
        {
            request.ExecutionId,
            request.LogicalEffectId,
            request.ActionId,
            ActionType = (string?)null,
            Effect = request.Effect.Type.ToString(),
            Source = "Rcon",
            Outcome = "Dispatched",
            OccurredAtUtc = occurredAtUtc,
            Detail = dispatchDetail,
            AdminActionId = (string?)null,
            request.PlayerGuid,
            request.SlotId,
            request.Effect.Reason,
            request.Effect.Message,
            request.ConnectionId,
            request.ConnectionGeneration,
            RecipientTarget = recipient,
            request.DeliveryIdentity,
            Correlation = request.Correlation
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var eventWriteFailed = false;
        try
        {
            var eventResult = await repository.GameServersEvents.V1
                .CreateGameServerEvent(new CreateGameServerEventDto(gameServerId, eventType, data)
                {
                    IdempotencyKey = CreateEventIdempotencyKey(request.LogicalEffectId, eventType)
                }, CancellationToken.None)
                .ConfigureAwait(false);
            if (!eventResult.IsSuccess)
            {
                eventWriteFailed = true;
                logger.LogError(
                    "Repository returned {StatusCode} while writing rules action event {EventType} for server {GameServerId}",
                    eventResult.StatusCode,
                    eventType,
                    gameServerId);
            }
        }
        catch (Exception ex)
        {
            eventWriteFailed = true;
            logger.LogError(ex, "Failed to write rules action event for server {GameServerId}, execution {ExecutionId}", gameServerId, request.ExecutionId);
        }

        var resultDetail = dispatchDetail;
        if (auditWriteFailed || eventWriteFailed)
        {
            resultDetail += " Audit or event persistence failed; do not retry the native command.";
        }

        return Result(RuleActionExecutionOutcome.Dispatched, resultDetail, occurredAtUtc);
    }

    private static Guid CreateEventIdempotencyKey(string logicalEffectId, string eventType)
    {
        var identity = Encoding.UTF8.GetBytes($"RulesNativeAction|{logicalEffectId}|{eventType}");
        var hash = SHA256.HashData(identity);
        return new Guid(hash.AsSpan(0, 16));
    }
}
