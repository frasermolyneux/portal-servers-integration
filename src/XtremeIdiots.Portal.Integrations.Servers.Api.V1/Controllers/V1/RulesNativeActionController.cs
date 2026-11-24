using System.Text.Json;
using System.Text;
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

        IActionResult Result(RuleActionExecutionOutcome outcome, string detail) =>
            new ApiResponse<RuleActionResult>(new RuleActionResult
            {
                ExecutionId = request.ExecutionId,
                GameServerId = gameServerId,
                ActionId = request.ActionId,
                LogicalEffectId = request.LogicalEffectId,
                Correlation = request.Correlation,
                Outcome = outcome,
                OccurredAtUtc = DateTimeOffset.UtcNow,
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

        if (request.Effect.Type == RuleNativeEffectType.DropPlayer && !string.IsNullOrWhiteSpace(request.Effect.Reason))
        {
            return Result(RuleActionExecutionOutcome.SkippedUnsupported, "This RCON adapter cannot deliver a kick reason.");
        }

        if ((request.Effect.Type == RuleNativeEffectType.DropPlayer && request.Effect.Message is not null) ||
            (request.Effect.Type is RuleNativeEffectType.PublicMessage or RuleNativeEffectType.PrivateMessage && request.Effect.Reason is not null))
        {
            return BadRequest("The native effect contains unexpected parameters.");
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

        if (request.Effect.Type is RuleNativeEffectType.PublicMessage or RuleNativeEffectType.PrivateMessage &&
            (string.IsNullOrWhiteSpace(request.Effect.Message) || Encoding.UTF8.GetByteCount(request.Effect.Message) > 256 ||
             request.Effect.Message.Any(c => c < 32 || c == 127)))
        {
            return BadRequest("A message of at most 256 UTF-8 bytes without control characters is required.");
        }

        if (request.Effect.Type is RuleNativeEffectType.PublicMessage or RuleNativeEffectType.PrivateMessage &&
            request.Effect.Message!.Any(c => c > 126 || c is ';' or '"' or '\\'))
        {
            return Result(RuleActionExecutionOutcome.SkippedUnsupported, "This RCON adapter cannot safely represent the requested message.");
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

        var bindingResult = await repository.RuleConnectionBindings.V1.GetCurrentBinding(gameServerId, request.SlotId, request.PlayerGuid, ct).ConfigureAwait(false);
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
            binding.SlotId != request.SlotId ||
            !string.Equals(binding.PlayerGuid, request.PlayerGuid, StringComparison.OrdinalIgnoreCase) ||
            binding.ConnectionId != request.ConnectionId || binding.ConnectionGeneration is not null ||
            binding.Runtime is null || binding.Runtime.Id != request.Runtime.Id ||
            binding.Runtime.IdentityModel != RuleRuntimeIdentityModel.AgentObservedConnection ||
            binding.Runtime.Version != request.Runtime.Version ||
            binding.Runtime.ProtocolVersion != request.Runtime.ProtocolVersion ||
            binding.Runtime.InstanceId != request.Runtime.InstanceId ||
            binding.Runtime.StartedAtUtc != request.Runtime.StartedAtUtc)
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

            var matches = roster.Players.Where(p => p.Slot == request.SlotId).ToArray();
            if (matches.Length > 1)
            {
                return Result(RuleActionExecutionOutcome.Failed, "The player slot is ambiguous.");
            }

            if (matches.Length == 0)
            {
                return Result(RuleActionExecutionOutcome.Failed, "The partial roster does not establish whether the player is present.");
            }

            if (!string.Equals(matches[0].PlayerIdentifier, request.PlayerGuid, StringComparison.OrdinalIgnoreCase))
            {
                return Result(RuleActionExecutionOutcome.Stale, "The player no longer occupies the expected slot.");
            }

            if (roster.Players.Count(p => string.Equals(p.PlayerIdentifier, request.PlayerGuid, StringComparison.OrdinalIgnoreCase)) != 1)
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
                    await native.DispatchPrivateMessage(request.SlotId, request.Effect.Message!, ct).ConfigureAwait(false);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Rules action dispatch outcome is unknown for server {GameServerId}", gameServerId);
            return Result(RuleActionExecutionOutcome.OutcomeUnknown, "The native command may have been sent; do not retry blindly.");
        }

        var operation = $"RulesNativeAction.{request.Effect.Type}.Dispatched";
        var audit = AuditEvent.ServerAction(operation, AuditAction.Moderate)
            .WithGameContext(server.GameType.ToString(), gameServerId)
            .WithSource(nameof(RulesNativeActionController))
            .WithTarget(request.PlayerGuid, "Player")
            .WithProperty("SlotId", request.SlotId.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .WithProperty("ExecutionId", request.ExecutionId.ToString())
            .WithProperty("LogicalEffectId", request.LogicalEffectId)
            .WithProperty("FeatureId", request.Correlation.FeatureId)
            .WithProperty("RuleId", request.Correlation.RuleId)
            .Build();
        auditLogger.LogAudit(audit);
        var data = JsonSerializer.Serialize(new
        {
            request.Effect.Type,
            request.SlotId,
            request.PlayerGuid,
            request.Effect.Message,
            request.ConnectionId,
            request.ConnectionGeneration,
            request.ExecutionId,
            request.LogicalEffectId,
            request.ActionId,
            request.Correlation.FeatureId,
            request.Correlation.RuleId,
            request.Correlation.TriggerOccurrenceId,
            Outcome = "Dispatched"
        });
        try
        {
            await repository.GameServersEvents.V1.CreateGameServerEvent(new CreateGameServerEventDto(gameServerId, operation, data), ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to write rules action dispatch event for server {GameServerId}", gameServerId);
        }
        return Result(RuleActionExecutionOutcome.Dispatched, "UDP command sent without confirmation; connection generation was not validated.");
    }
}
