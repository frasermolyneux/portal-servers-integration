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

    protected Task<IActionResult> Status(Guid gameServerId, string operationName, CancellationToken cancellationToken) =>
        ExecuteStructuredOperation(
            gameServerId,
            operationName,
            AuditAction.Execute,
            (client, ct) => Task.FromResult(client.GetPlayers()),
            players => new RconStatusResponseDto
            {
                Players = players.Select(player => new RconStatusPlayerDto
                {
                    Num = player.Num,
                    Guid = player.Guid ?? string.Empty,
                    Name = player.Name ?? string.Empty,
                    IpAddress = player.IpAddress ?? string.Empty,
                    Rate = player.Rate,
                    Ping = player.Ping
                }).ToList()
            },
            cancellationToken,
            emitAuditAndEvent: false);

    protected Task<IActionResult> GetMaps(Guid gameServerId, string operationName, CancellationToken cancellationToken) =>
        ExecuteStructuredOperation(
            gameServerId,
            operationName,
            AuditAction.Execute,
            (client, ct) => client.GetMaps(),
            maps => new RconMapCollectionDto(maps.Select(map => new RconMapDto(map.GameType, map.MapName))),
            cancellationToken,
            emitAuditAndEvent: false);

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

    protected async Task<IActionResult> Tell(Guid gameServerId, CoD4xTargetMessageRequestDto? request, string operationName, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Request body cannot be null.")).ToBadRequestResult().ToHttpResult();
        }

        if (string.IsNullOrWhiteSpace(request.Target))
        {
            return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Target is required.")).ToBadRequestResult().ToHttpResult();
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Message is required.")).ToBadRequestResult().ToHttpResult();
        }

        if (request.Message.IndexOfAny([';', '\r', '\n']) >= 0)
        {
            return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Message contains unsupported characters.")).ToBadRequestResult().ToHttpResult();
        }

        if (!int.TryParse(request.Target.Trim(), out var clientId) || clientId < 0)
        {
            return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Target must be a non-negative client ID.")).ToBadRequestResult().ToHttpResult();
        }

        var message = request.Message.Trim();
        return await ExecuteStringOperation(
                gameServerId,
                operationName,
                AuditAction.Moderate,
                (client, ct) => client.TellPlayer(clientId, message),
                cancellationToken,
                BuildOperatorData(("ClientId", clientId), ("Message", message)))
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

    protected Task<IActionResult> ServerInfo(Guid gameServerId, string operationName, CancellationToken cancellationToken) =>
        ExecuteStringOperation(gameServerId, operationName, AuditAction.Execute, (client, ct) => client.GetServerInfo(), cancellationToken, emitAuditAndEvent: false);

    protected Task<IActionResult> SystemInfo(Guid gameServerId, string operationName, CancellationToken cancellationToken) =>
        ExecuteStringOperation(gameServerId, operationName, AuditAction.Execute, (client, ct) => client.GetSystemInfo(), cancellationToken, emitAuditAndEvent: false);

    protected Task<IActionResult> CmdList(Guid gameServerId, string operationName, CancellationToken cancellationToken) =>
        ExecuteStringOperation(gameServerId, operationName, AuditAction.Execute, (client, ct) => client.GetCommandList(), cancellationToken, emitAuditAndEvent: false);

    protected Task<IActionResult> CvarList(Guid gameServerId, string operationName, CancellationToken cancellationToken) =>
        ExecuteStringOperation(gameServerId, operationName, AuditAction.Execute, (client, ct) => client.GetCvarList(), cancellationToken, emitAuditAndEvent: false);

    protected Task<IActionResult> DvarList(Guid gameServerId, string operationName, CancellationToken cancellationToken) =>
        ExecuteStringOperation(gameServerId, operationName, AuditAction.Execute, (client, ct) => client.GetDvarList(), cancellationToken, emitAuditAndEvent: false);

    protected async Task<IActionResult> Map(Guid gameServerId, ChangeMapRequest? request, string operationName, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Request body cannot be null.")).ToBadRequestResult().ToHttpResult();
        }

        if (string.IsNullOrWhiteSpace(request.MapName))
        {
            return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "MapName is required.")).ToBadRequestResult().ToHttpResult();
        }

        return await ExecuteStringOperation(
                gameServerId,
                operationName,
                AuditAction.Execute,
                (client, ct) => client.ChangeMap(request.MapName.Trim()),
                cancellationToken,
                BuildOperatorData(("MapName", request.MapName.Trim())))
            .ConfigureAwait(false);
    }

    protected async Task<IActionResult> Kick(Guid gameServerId, ClientSlotRequest? request, string operationName, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Request body cannot be null.")).ToBadRequestResult().ToHttpResult();
        }

        var validationError = ValidateClientSlotRequest(request);
        if (validationError != null)
        {
            return validationError.ToHttpResult();
        }

        return await ExecuteStringOperation(
                gameServerId,
                operationName,
                AuditAction.Moderate,
                (client, ct) => client.KickPlayer(request.ClientId),
                cancellationToken,
                BuildOperatorData(("ClientId", request.ClientId)))
            .ConfigureAwait(false);
    }

    protected async Task<IActionResult> TempBan(Guid gameServerId, ClientSlotRequest? request, string operationName, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Request body cannot be null.")).ToBadRequestResult().ToHttpResult();
        }

        var validationError = ValidateClientSlotRequest(request);
        if (validationError != null)
        {
            return validationError.ToHttpResult();
        }

        return await ExecuteStringOperation(
                gameServerId,
                operationName,
                AuditAction.Moderate,
                (client, ct) => client.TempBanPlayer(request.ClientId),
                cancellationToken,
                BuildOperatorData(("ClientId", request.ClientId)))
            .ConfigureAwait(false);
    }

    protected async Task<IActionResult> Ban(Guid gameServerId, ClientSlotRequest? request, string operationName, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Request body cannot be null.")).ToBadRequestResult().ToHttpResult();
        }

        var validationError = ValidateClientSlotRequest(request);
        if (validationError != null)
        {
            return validationError.ToHttpResult();
        }

        return await ExecuteStringOperation(
                gameServerId,
                operationName,
                AuditAction.Moderate,
                (client, ct) => client.BanPlayer(request.ClientId),
                cancellationToken,
                BuildOperatorData(("ClientId", request.ClientId)))
            .ConfigureAwait(false);
    }

    protected async Task<IActionResult> Set(Guid gameServerId, SetDvarRequest? request, string operationName, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Request body cannot be null.")).ToBadRequestResult().ToHttpResult();
        }

        var validationError = ValidateSetDvarRequest(request);
        if (validationError != null)
        {
            return validationError.ToHttpResult();
        }

        return await ExecuteStringOperation(
                gameServerId,
                operationName,
                AuditAction.Execute,
            (client, ct) => client.SetDvar(request.DvarName.Trim(), request.Value.Trim()),
                cancellationToken,
            BuildOperatorData(("DvarName", request.DvarName.Trim())),
            emitAuditAndEvent: true,
            includeResultInOperatorEvent: false)
            .ConfigureAwait(false);
    }

    protected async Task<IActionResult> Seta(Guid gameServerId, SetDvarRequest? request, string operationName, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Request body cannot be null.")).ToBadRequestResult().ToHttpResult();
        }

        var validationError = ValidateSetDvarRequest(request);
        if (validationError != null)
        {
            return validationError.ToHttpResult();
        }

        return await ExecuteStringOperation(
                gameServerId,
                operationName,
                AuditAction.Execute,
                (client, ct) => client.SetaDvar(request.DvarName.Trim(), request.Value.Trim()),
                cancellationToken,
            BuildOperatorData(("DvarName", request.DvarName.Trim())),
            emitAuditAndEvent: true,
            includeResultInOperatorEvent: false)
            .ConfigureAwait(false);
    }

    private async Task<IActionResult> ExecuteStructuredOperation<TData, TResponse>(
        Guid gameServerId,
        string operationName,
        AuditAction auditAction,
        Func<IRconClient, CancellationToken, Task<TData>> execute,
        Func<TData, TResponse> map,
        CancellationToken cancellationToken,
        bool emitAuditAndEvent = true)
    {
        var rawResult = await ExecuteInternal(
                gameServerId,
                operationName,
                auditAction,
                execute,
                null,
                emitAuditAndEvent,
                includeResultInOperatorEvent: true,
                cancellationToken)
            .ConfigureAwait(false);
        if (!rawResult.IsSuccess)
        {
            return rawResult.ToHttpResult();
        }

        if (rawResult.Result is null)
        {
            return new ApiResponse<TResponse>(
                    new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "RCON command did not return data."))
                .ToApiResult()
                .ToHttpResult();
        }

        var resultData = rawResult.Result.Data;
        if (resultData is null)
        {
            return new ApiResponse<TResponse>(
                    new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "RCON command did not return data."))
                .ToApiResult()
                .ToHttpResult();
        }

        var mappedData = map(resultData);
        return new ApiResponse<TResponse>(mappedData).ToApiResult().ToHttpResult();
    }

    private async Task<IActionResult> ExecuteStringOperation(
        Guid gameServerId,
        string operationName,
        AuditAction auditAction,
        Func<IRconClient, CancellationToken, Task<string>> execute,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, object?>? operatorData = null,
        bool emitAuditAndEvent = true,
        bool includeResultInOperatorEvent = true)
    {
        var response = await ExecuteInternal(
                gameServerId,
                operationName,
                auditAction,
                execute,
                operatorData,
                emitAuditAndEvent,
                includeResultInOperatorEvent,
                cancellationToken)
            .ConfigureAwait(false);

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

    private async Task<ApiResult<TData>> ExecuteInternal<TData>(
        Guid gameServerId,
        string operationName,
        AuditAction auditAction,
        Func<IRconClient, CancellationToken, Task<TData>> execute,
        IReadOnlyDictionary<string, object?>? operatorData,
        bool emitAuditAndEvent,
        bool includeResultInOperatorEvent,
        CancellationToken cancellationToken)
    {
        var contextResult = await TryGetContext(gameServerId, cancellationToken).ConfigureAwait(false);
        if (contextResult.Error != null)
        {
            return new ApiResult<TData>(
                contextResult.Error.StatusCode,
                new ApiResponse<TData>(contextResult.Error.Result?.Errors ?? []));
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
                var resultText = includeResultInOperatorEvent ? result?.ToString() : null;
                await WriteSuccessAuditAndEvent(context, operationName, auditAction, operatorData, cancellationToken, resultText).ConfigureAwait(false);
            }

            return new ApiResponse<TData>(result).ToApiResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogWarning(ex, "{OperationName} is not implemented for game server {GameServerId}", operationName, gameServerId);
            return new ApiResponse<TData>(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The requested operation is not implemented for this game server type.")).ToBadRequestResult();
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
            return new ApiResponse<TData>(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to execute RCON command.")).ToApiResult();
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

    private static ApiResult<string>? ValidateClientSlotRequest(ClientSlotRequest request)
    {
        if (request.ClientId < 0)
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, "ClientId must be greater than or equal to 0.")).ToBadRequestResult();
        }

        return null;
    }

    private static ApiResult<string>? ValidateSetDvarRequest(SetDvarRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DvarName))
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, "DvarName is required.")).ToBadRequestResult();
        }

        if (string.IsNullOrWhiteSpace(request.Value))
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, "Value is required.")).ToBadRequestResult();
        }

        return null;
    }

    private sealed record RconContext(Guid GameServerId, string GameType, string Hostname, int QueryPort, IRconClient Client);
}
