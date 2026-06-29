using System.Text.Json;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Mvc;
using MX.Api.Abstractions;
using MX.Api.Web.Extensions;
using MX.Observability.ApplicationInsights.Auditing;
using MX.Observability.ApplicationInsights.Auditing.Models;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Constants;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.GameServers;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1;

public abstract class GameScopedRconControllerBase(
    ILogger logger,
    IRepositoryApiClient repositoryApiClient,
    IRconClientFactory rconClientFactory,
    TelemetryClient telemetryClient,
    IAuditLogger auditLogger,
    GameType supportedGameType,
    string sourceName) : Controller
{
    protected Task<IActionResult> GetCurrentMap(Guid gameServerId, string operationName, CancellationToken cancellationToken) =>
        ExecuteStructuredOperation(gameServerId, operationName, AuditAction.Execute, (client, ct) => client.GetCurrentMap(), mapName => new RconCurrentMapDto(mapName), cancellationToken, emitAuditAndEvent: false);

    protected async Task<IActionResult> Say(Guid gameServerId, SayRequest? request, string operationName, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Request body cannot be null.")).ToBadRequestResult().ToHttpResult();
        }

        var messages = ExtractMessages(request);
        if (messages.Count == 0)
        {
            return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "At least one message is required.")).ToBadRequestResult().ToHttpResult();
        }

        return await ExecuteVoidOperation(
                gameServerId,
                operationName,
                AuditAction.Execute,
                async (client, ct) =>
                {
                    foreach (var message in messages)
                    {
                        await client.Say(message).ConfigureAwait(false);
                    }
                },
                BuildOperatorData(("MessageCount", messages.Count)),
                cancellationToken)
            .ConfigureAwait(false);
    }

    protected Task<IActionResult> Restart(Guid gameServerId, string operationName, CancellationToken cancellationToken) =>
        ExecuteStringOperation(gameServerId, operationName, AuditAction.Execute, (client, ct) => client.Restart(), cancellationToken);

    protected Task<IActionResult> RestartMap(Guid gameServerId, string operationName, CancellationToken cancellationToken) =>
        ExecuteStringOperation(gameServerId, operationName, AuditAction.Execute, (client, ct) => client.RestartMap(), cancellationToken);

    protected Task<IActionResult> FastRestartMap(Guid gameServerId, string operationName, CancellationToken cancellationToken) =>
        ExecuteStringOperation(gameServerId, operationName, AuditAction.Execute, (client, ct) => client.FastRestartMap(), cancellationToken);

    protected Task<IActionResult> NextMap(Guid gameServerId, string operationName, CancellationToken cancellationToken) =>
        ExecuteStringOperation(gameServerId, operationName, AuditAction.Execute, (client, ct) => client.NextMap(), cancellationToken);

    private async Task<IActionResult> ExecuteStructuredOperation<TResponse>(
        Guid gameServerId,
        string operationName,
        AuditAction auditAction,
        Func<IRconClient, CancellationToken, Task<string>> execute,
        Func<string, TResponse> map,
        CancellationToken cancellationToken,
        bool emitAuditAndEvent = true)
    {
        var rawResult = await ExecuteInternal(gameServerId, operationName, auditAction, execute, null, emitAuditAndEvent, cancellationToken).ConfigureAwait(false);
        if (!rawResult.IsSuccess)
        {
            return rawResult.ToHttpResult();
        }

        return new ApiResponse<TResponse>(map(rawResult.Result?.Data ?? string.Empty)).ToApiResult().ToHttpResult();
    }

    private async Task<IActionResult> ExecuteStringOperation(
        Guid gameServerId,
        string operationName,
        AuditAction auditAction,
        Func<IRconClient, CancellationToken, Task<string>> execute,
        CancellationToken cancellationToken)
    {
        var response = await ExecuteInternal(gameServerId, operationName, auditAction, execute, null, emitAuditAndEvent: true, cancellationToken).ConfigureAwait(false);
        return response.ToHttpResult();
    }

    private async Task<IActionResult> ExecuteVoidOperation(
        Guid gameServerId,
        string operationName,
        AuditAction auditAction,
        Func<IRconClient, CancellationToken, Task> execute,
        IReadOnlyDictionary<string, object?>? operatorData,
        CancellationToken cancellationToken)
    {
        var contextResult = await TryGetContext(gameServerId, cancellationToken).ConfigureAwait(false);
        if (contextResult.Error != null)
        {
            return contextResult.Error.ToHttpResult();
        }

        var context = contextResult.Context!;
        var operation = telemetryClient.StartOperation<DependencyTelemetry>(operationName);
        operation.Telemetry.Type = $"{context.GameType}Server";
        operation.Telemetry.Target = $"{context.Hostname}:{context.QueryPort}";

        try
        {
            await execute(context.Client, cancellationToken).ConfigureAwait(false);

            await WriteSuccessAuditAndEvent(context, operationName, auditAction, operatorData, cancellationToken).ConfigureAwait(false);
            return new ApiResponse().ToApiResult().ToHttpResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogWarning(ex, "{OperationName} is not implemented for game server {GameServerId}", operationName, gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The requested operation is not implemented for this game server type.")).ToBadRequestResult().ToHttpResult();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = "Cancelled";
            throw;
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to execute {OperationName} on game server {GameServerId}", operationName, gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to execute RCON command.")).ToApiResult().ToHttpResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    private async Task<ApiResult<string>> ExecuteInternal(
        Guid gameServerId,
        string operationName,
        AuditAction auditAction,
        Func<IRconClient, CancellationToken, Task<string>> execute,
        IReadOnlyDictionary<string, object?>? operatorData,
        bool emitAuditAndEvent,
        CancellationToken cancellationToken)
    {
        var contextResult = await TryGetContext(gameServerId, cancellationToken).ConfigureAwait(false);
        if (contextResult.Error != null)
        {
            return contextResult.Error;
        }

        var context = contextResult.Context!;
        var operation = telemetryClient.StartOperation<DependencyTelemetry>(operationName);
        operation.Telemetry.Type = $"{context.GameType}Server";
        operation.Telemetry.Target = $"{context.Hostname}:{context.QueryPort}";

        try
        {
            var result = await execute(context.Client, cancellationToken).ConfigureAwait(false);
            if (emitAuditAndEvent)
            {
                await WriteSuccessAuditAndEvent(context, operationName, auditAction, operatorData, cancellationToken, result).ConfigureAwait(false);
            }

            return new ApiResponse<string>(result).ToApiResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogWarning(ex, "{OperationName} is not implemented for game server {GameServerId}", operationName, gameServerId);
            return new ApiResponse<string>(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The requested operation is not implemented for this game server type.")).ToBadRequestResult();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = "Cancelled";
            throw;
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to execute {OperationName} on game server {GameServerId}", operationName, gameServerId);
            return new ApiResponse<string>(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to execute RCON command.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    private async Task WriteSuccessAuditAndEvent(
        RconContext context,
        string operationName,
        AuditAction auditAction,
        IReadOnlyDictionary<string, object?>? operatorData,
        CancellationToken cancellationToken,
        string? result = null)
    {
        var audit = AuditEvent.ServerAction(operationName, auditAction)
            .WithGameContext(context.GameType, context.GameServerId)
            .WithSource(sourceName)
            .Build();
        auditLogger.LogAudit(audit);

        var data = operatorData == null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(operatorData);

        if (!string.IsNullOrWhiteSpace(result))
        {
            data["Result"] = result;
        }

        await TryWriteOperatorEventAsync(context.GameServerId, operationName, data, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(ApiResult<string>? Error, RconContext? Context)> TryGetContext(Guid gameServerId, CancellationToken cancellationToken)
    {
        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId, cancellationToken).ConfigureAwait(false);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return (new ApiResponse<string>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult(), null);
        }

        if (gameServerApiResponse.Result.Data.GameType != supportedGameType)
        {
            return (new ApiResponse<string>(new ApiError(ErrorCodes.OPERATION_NOT_SUPPORTED_FOR_GAME_TYPE, $"This operation is only supported for {supportedGameType} game servers.")).ToBadRequestResult(), null);
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon", cancellationToken).ConfigureAwait(false);
        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);

        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return (new ApiResponse<string>(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult(), null);
        }

        var rconClient = rconClientFactory.CreateInstance(
            gameServerApiResponse.Result.Data.GameType,
            gameServerApiResponse.Result.Data.GameServerId,
            gameServerApiResponse.Result.Data.Hostname,
            gameServerApiResponse.Result.Data.QueryPort,
            rconPassword);

        return (null, new RconContext(
            gameServerApiResponse.Result.Data.GameServerId,
            gameServerApiResponse.Result.Data.GameType.ToString(),
            gameServerApiResponse.Result.Data.Hostname,
            gameServerApiResponse.Result.Data.QueryPort,
            rconClient));
    }

    private async Task TryWriteOperatorEventAsync(Guid gameServerId, string eventType, object data, CancellationToken cancellationToken = default)
    {
        var eventData = JsonSerializer.Serialize(data);
        var effectiveCancellationToken = cancellationToken == default
            ? HttpContext.RequestAborted
            : cancellationToken;

        try
        {
            await repositoryApiClient.GameServersEvents.V1
                .CreateGameServerEvent(new CreateGameServerEventDto(gameServerId, eventType, eventData), effectiveCancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to write {EventType} operator event for game server {GameServerId}",
                eventType,
                gameServerId);
        }
    }

    private static IReadOnlyList<string> ExtractMessages(SayRequest request)
    {
        var messages = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.Message))
        {
            messages.Add(request.Message.Trim());
        }

        if (request.Messages != null)
        {
            foreach (var message in request.Messages)
            {
                if (!string.IsNullOrWhiteSpace(message))
                {
                    messages.Add(message.Trim());
                }
            }
        }

        return messages;
    }

    private static IReadOnlyDictionary<string, object?> BuildOperatorData(params (string Key, object? Value)[] values)
    {
        var properties = new Dictionary<string, object?>();

        foreach (var (key, value) in values)
        {
            if (value is string stringValue)
            {
                if (!string.IsNullOrWhiteSpace(stringValue))
                {
                    properties[key] = stringValue;
                }

                continue;
            }

            if (value != null)
            {
                properties[key] = value;
            }
        }

        return properties;
    }

    private sealed record RconContext(Guid GameServerId, string GameType, string Hostname, int QueryPort, IRconClient Client);
}
