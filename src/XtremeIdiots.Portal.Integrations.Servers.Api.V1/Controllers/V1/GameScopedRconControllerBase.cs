using System.Text.Json;
using System.Net;
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
            (client, ct) =>
            {
                if (client is IPlayerRosterRconClient rosterClient)
                {
                    var (players, roster) = rosterClient.GetPlayersWithRoster(ct);
                    return Task.FromResult(new RconPlayersSnapshot(players, roster));
                }

                return Task.FromResult(new RconPlayersSnapshot(client.GetPlayers(), null));
            },
            snapshot => new RconStatusResponseDto
            {
                Players = snapshot.Players.Select(player => new RconStatusPlayerDto
                {
                    Num = player.Num,
                    Guid = player.Guid ?? string.Empty,
                    Name = player.Name ?? string.Empty,
                    IpAddress = player.IpAddress ?? string.Empty,
                    Rate = player.Rate,
                    Ping = player.Ping
                }).ToList(),
                Roster = snapshot.Roster
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
            return MessageError(false, HttpStatusCode.BadRequest, ErrorCodes.INVALID_REQUEST, "Request body cannot be null.");
        }

        return await ExecuteMessageBatch(gameServerId, operationName, AuditAction.Execute,
            RuleMessageChannels.RconPublicChat, request.Message, request.Messages, request.FirstFragmentHeader, null, cancellationToken).ConfigureAwait(false);
    }

    protected async Task<IActionResult> Tell(Guid gameServerId, CoD4xTargetMessageRequestDto? request, string operationName, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return MessageError(true, HttpStatusCode.BadRequest, ErrorCodes.INVALID_REQUEST, "Request body cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(request.Target))
        {
            return MessageError(true, HttpStatusCode.BadRequest, ErrorCodes.INVALID_REQUEST, "Target is required.");
        }

        if (!int.TryParse(request.Target.Trim(), out var clientId) || clientId < 0)
        {
            return MessageError(true, HttpStatusCode.BadRequest, ErrorCodes.INVALID_REQUEST, "Target must be a non-negative client ID.");
        }

        return await ExecuteMessageBatch(gameServerId, operationName, AuditAction.Moderate,
            RuleMessageChannels.RconPrivateChat, request.Message, request.Messages, request.FirstFragmentHeader, clientId, cancellationToken).ConfigureAwait(false);
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

    private async Task<IActionResult> ExecuteMessageBatch(
        Guid gameServerId,
        string operationName,
        AuditAction auditAction,
        string channel,
        string? message,
        IEnumerable<string>? messages,
        string? firstFragmentHeader,
        int? clientId,
        CancellationToken cancellationToken)
    {
        var (items, validationError) = RconMessageBatch.Normalize(supportedGameType, message, messages, firstFragmentHeader);
        if (items is null)
        {
            return MessageError(clientId.HasValue, HttpStatusCode.BadRequest, ErrorCodes.INVALID_REQUEST, validationError!);
        }

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
            var budget = await MessageChannelBudgetResolver.ResolveAsync(supportedGameType, channel, context.Client, logger, cancellationToken).ConfigureAwait(false);
            if (budget.Status != RuleMessageBudgetStatus.Available || budget.Budget is null)
            {
                var code = budget.Status == RuleMessageBudgetStatus.Unsupported ? ErrorCodes.OPERATION_NOT_IMPLEMENTED : ErrorCodes.RCON_OPERATION_FAILED;
                return MessageError(clientId.HasValue, HttpStatusCode.BadRequest, code, budget.Detail ?? "Message budget is unavailable.");
            }

            var (prepared, error) = RconMessageBatch.Prepare(supportedGameType, items, budget.Budget, firstFragmentHeader);
            if (prepared is null)
            {
                return MessageError(clientId.HasValue, HttpStatusCode.BadRequest, ErrorCodes.INVALID_REQUEST, error!);
            }

            var result = await RconMessageBatch.DispatchAsync(gameServerId, supportedGameType, channel, context.Client, prepared,
                async (fragment, ct) =>
                {
                    if (clientId is { } id)
                    {
                        return await context.Client.TellPlayer(id, fragment, ct).ConfigureAwait(false);
                    }

                    await context.Client.Say(fragment, ct).ConfigureAwait(false);
                    return null;
                }, logger, cancellationToken).ConfigureAwait(false);

            var auditSucceeded = true;
            if (result.CompletedFragments > 0)
            {
                var eventName = result.IsSuccess ? operationName : $"{operationName}.PartialDispatch";
                var operatorData = BuildOperatorData(
                    ("ClientId", clientId),
                    ("MessageCount", prepared.Items.Count),
                    ("Message", prepared.Items.Count == 1 ? prepared.Items[0] : null),
                    ("Messages", prepared.Items.Count > 1 ? prepared.Items : null),
                    ("FirstFragmentHeader", firstFragmentHeader),
                    ("CompletedFragments", result.CompletedFragments),
                    ("TotalFragments", result.TotalFragments),
                    ("OutcomeUnknown", result.OutcomeUnknown));
                auditSucceeded = await WriteMessageAuditAndEvent(context, eventName, auditAction, clientId, operatorData).ConfigureAwait(false);
                if (!auditSucceeded && result.IsSuccess)
                {
                    operation.Telemetry.Success = false;
                    return MessageError(clientId.HasValue, HttpStatusCode.BadGateway, ErrorCodes.RCON_MESSAGE_PARTIAL_FAILURE,
                        $"All {result.TotalFragments} fragments were dispatched, but audit or event persistence failed. Do not retry the batch.");
                }
            }

            if (!result.IsSuccess)
            {
                operation.Telemetry.Success = false;
                operation.Telemetry.ResultCode = result.Detail;
                var code = result.CompletedFragments > 0 ? ErrorCodes.RCON_MESSAGE_PARTIAL_FAILURE :
                    result.NotImplemented ? ErrorCodes.OPERATION_NOT_IMPLEMENTED : ErrorCodes.RCON_OPERATION_FAILED;
                var detail = result.DescribeFailure() + (auditSucceeded ? string.Empty : " Audit or event persistence also failed.");
                return MessageError(clientId.HasValue, HttpStatusCode.BadGateway, code, detail);
            }

            return clientId.HasValue
                ? new ApiResponse<string>(result.LastResponse ?? "Tell command sent to player").ToApiResult().ToHttpResult()
                : new ApiResponse().ToApiResult().ToHttpResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    private static IActionResult MessageError(bool hasResponse, HttpStatusCode status, string code, string detail)
    {
        var error = new ApiError(code, detail);
        return hasResponse
            ? new ApiResult<string>(status, new ApiResponse<string>(error)).ToHttpResult()
            : new ObjectResult(new ApiResponse(error)) { StatusCode = (int)status };
    }

    private async Task<bool> WriteMessageAuditAndEvent(
        RconContext context, string eventName, AuditAction action, int? clientId, IReadOnlyDictionary<string, object?> operatorData)
    {
        var audit = AuditEvent.ServerAction(eventName, action)
            .WithGameContext(context.GameType, context.GameServerId)
            .WithSource(sourceName)
            .WithProperty("CompletedFragments", operatorData["CompletedFragments"]!.ToString()!)
            .WithProperty("TotalFragments", operatorData["TotalFragments"]!.ToString()!);
        if (clientId is { } id)
        {
            audit = audit.WithTarget(id.ToString(System.Globalization.CultureInfo.InvariantCulture), "ClientId");
        }
        if (operatorData.TryGetValue("FirstFragmentHeader", out var header) && header is string headerText)
        {
            audit = audit.WithProperty("FirstFragmentHeader", headerText);
        }

        var auditSucceeded = true;
        try
        {
            auditLogger.LogAudit(audit.Build());
        }
        catch (Exception ex)
        {
            auditSucceeded = false;
            logger.LogError(ex, "Failed to write message audit for server {GameServerId}", context.GameServerId);
        }

        var eventSucceeded = await TryWriteOperatorEventAsync(context.GameServerId, eventName, operatorData, CancellationToken.None).ConfigureAwait(false);
        return auditSucceeded && eventSucceeded;
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

    private async Task<bool> TryWriteOperatorEventAsync(Guid gameServerId, string eventType, object data, CancellationToken? cancellationToken = null)
    {
        var eventData = JsonSerializer.Serialize(data);
        var effectiveCancellationToken = cancellationToken ?? HttpContext.RequestAborted;

        try
        {
            var result = await repositoryApiClient.GameServersEvents.V1
                .CreateGameServerEvent(new CreateGameServerEventDto(gameServerId, eventType, eventData), effectiveCancellationToken)
                .ConfigureAwait(false);
            if (!result.IsSuccess)
            {
                logger.LogWarning("Repository returned {StatusCode} while writing {EventType} for game server {GameServerId}",
                    result.StatusCode, eventType, gameServerId);
            }
            return result.IsSuccess;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to write {EventType} operator event for game server {GameServerId}",
                eventType,
                gameServerId);
            return false;
        }
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

        if (request.Value is null)
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, "Value is required.")).ToBadRequestResult();
        }

        return null;
    }

    private sealed record RconPlayersSnapshot(List<IRconPlayer> Players, RconRosterObservationDto? Roster);

    private sealed record RconContext(Guid GameServerId, string GameType, string Hostname, int QueryPort, IRconClient Client);
}
