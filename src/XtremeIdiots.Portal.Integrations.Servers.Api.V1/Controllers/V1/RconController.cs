using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Asp.Versioning;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MX.Api.Abstractions;
using MX.Api.Web.Extensions;
using MX.Observability.ApplicationInsights.Auditing;
using MX.Observability.ApplicationInsights.Auditing.Models;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Constants;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.GameServers;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1;

[ApiController]
[Authorize(Roles = "ServiceAccount")]
[ApiVersion(ApiVersions.V1)]
[Route("v{version:apiVersion}")]
public class RconController(
    ILogger<RconController> logger,
    IRepositoryApiClient repositoryApiClient,
    IRconClientFactory rconClientFactory,
    TelemetryClient telemetryClient,
    IAuditLogger auditLogger) : Controller, IRconApi
{
    private static readonly Regex DvarResponseRegex = new(@"""([^""]+)""\s+is:\s+""([^""]*)""", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex QuakeColorCodeRegex = new(@"\^[0-9A-Za-z]", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex MultiWhitespaceRegex = new(@"\s+", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex CoD4xPlayerIdentifierRegex = new(@"^[0-9]{17,21}$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));


    /// <summary>
    /// Verifies that the player in the specified slot matches the expected player name
    /// </summary>
    /// <returns>ApiResponse with error if verification fails, null if verification passes</returns>
    private ApiResponse? VerifyPlayerInSlot(IRconClient rconClient, int clientId, string expectedPlayerName, Guid gameServerId)
    {
        try
        {
            var normalizedExpectedPlayerName = NormalizePlayerName(expectedPlayerName);
            var players = rconClient.GetPlayers();
            var player = players?.FirstOrDefault(p => p.Num == clientId);

            if (player == null)
            {
                logger.LogWarning(
                    "Player verification failed for game server {GameServerId}: no player found in slot {ClientId}. Expected player '{ExpectedPlayerName}' (normalized '{NormalizedExpectedPlayerName}')",
                    gameServerId,
                    clientId,
                    expectedPlayerName,
                    normalizedExpectedPlayerName);

                return new ApiResponse(new ApiError(ErrorCodes.PLAYER_VERIFICATION_FAILED, $"No player found in slot {clientId}."));
            }

            var normalizedActualPlayerName = NormalizePlayerName(player.Name);
            if (!string.Equals(normalizedActualPlayerName, normalizedExpectedPlayerName, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning(
                    "Player verification mismatch on game server {GameServerId}, slot {ClientId}. Expected '{ExpectedPlayerName}' (normalized '{NormalizedExpectedPlayerName}') but found '{ActualPlayerName}' (normalized '{NormalizedActualPlayerName}')",
                    gameServerId,
                    clientId,
                    expectedPlayerName,
                    normalizedExpectedPlayerName,
                    player.Name,
                    normalizedActualPlayerName);

                return new ApiResponse(new ApiError(ErrorCodes.PLAYER_VERIFICATION_FAILED, $"Player verification failed. Expected '{expectedPlayerName}' but found '{player.Name}' in slot {clientId}."));
            }

            return null; // Verification passed
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to verify player in slot {ClientId} on game server {GameServerId}", clientId, gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to verify player identity before operation."));
        }
    }

    private static string NormalizePlayerName(string? playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            return string.Empty;
        }

        var withoutColors = QuakeColorCodeRegex.Replace(playerName, string.Empty);
        var collapsedWhitespace = MultiWhitespaceRegex.Replace(withoutColors, " ");
        return collapsedWhitespace.Trim();
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

    [HttpGet]
    [Route("rcon/{gameServerId}/status")]
    public async Task<IActionResult> GetServerStatus(Guid gameServerId)
    {
        var response = await ((IRconApi)this).GetServerStatus(gameServerId);

        return response.ToHttpResult();
    }

    async Task<ApiResult<ServerRconStatusResponseDto>> IRconApi.GetServerStatus(Guid gameServerId)
    {
        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse<ServerRconStatusResponseDto>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon").ConfigureAwait(false);


        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);


        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return new ApiResponse<ServerRconStatusResponseDto>(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult();
        }

        var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, rconPassword);

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconServerStatus");
        operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
        operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

        try
        {
            var statusResult = rconClient.GetPlayers();

            if (statusResult != null)
            {
                var dto = new ServerRconStatusResponseDto
                {
                    Players = statusResult.Select(p => new ServerRconPlayerDto
                    {
                        Num = p.Num,
                        Guid = p.Guid,
                        Name = p.Name,
                        IpAddress = p.IpAddress,
                        Rate = p.Rate,
                        Ping = p.Ping
                    }).ToList()
                };

                return new ApiResponse<ServerRconStatusResponseDto>(dto).ToApiResult();
            }
            else
            {
                return new ApiResponse<ServerRconStatusResponseDto>(new ServerRconStatusResponseDto()).ToApiResult();
            }
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to get RCON server status for game server {GameServerId}", gameServerId);
            return new ApiResponse<ServerRconStatusResponseDto>(new ApiError(ErrorCodes.RCON_CONNECTION_FAILED, "Failed to connect to the game server via RCON.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    [HttpGet]
    [Route("rcon/{gameServerId}/maps")]
    public async Task<IActionResult> GetServerMaps(Guid gameServerId)
    {
        var response = await ((IRconApi)this).GetServerMaps(gameServerId);

        return response.ToHttpResult();
    }

    async Task<ApiResult<RconMapCollectionDto>> IRconApi.GetServerMaps(Guid gameServerId)
    {
        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse<RconMapCollectionDto>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon").ConfigureAwait(false);


        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);


        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return new ApiResponse<RconMapCollectionDto>(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult();
        }

        var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, rconPassword);

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconMapRotation");
        operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
        operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

        try
        {
            var statusResult = await rconClient.GetMaps();

            if (statusResult != null && statusResult.Any())
            {
                var maps = statusResult.Select(m => new RconMapDto(m.GameType, m.MapName)).ToList();
                var data = new RconMapCollectionDto(maps);

                return new ApiResponse<RconMapCollectionDto>(data).ToApiResult();
            }
            else
            {
                var emptyResult = new RconMapCollectionDto(new List<RconMapDto>());
                return new ApiResponse<RconMapCollectionDto>(emptyResult).ToApiResult();
            }
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to get RCON server maps for game server {GameServerId}", gameServerId);
            return new ApiResponse<RconMapCollectionDto>(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to retrieve map rotation from the game server via RCON.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    [HttpGet]
    [Route("rcon/{gameServerId}/current-map")]
    public async Task<IActionResult> GetCurrentMap(Guid gameServerId)
    {
        var response = await ((IRconApi)this).GetCurrentMap(gameServerId);

        return response.ToHttpResult();
    }

    async Task<ApiResult<RconCurrentMapDto>> IRconApi.GetCurrentMap(Guid gameServerId)
    {
        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse<RconCurrentMapDto>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon").ConfigureAwait(false);


        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);


        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return new ApiResponse<RconCurrentMapDto>(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult();
        }

        var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, rconPassword);

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconCurrentMap");
        operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
        operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

        try
        {
            var currentMap = await rconClient.GetCurrentMap();
            var data = new RconCurrentMapDto(currentMap);

            return new ApiResponse<RconCurrentMapDto>(data).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to get current map for game server {GameServerId}", gameServerId);
            return new ApiResponse<RconCurrentMapDto>(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to retrieve current map from the game server via RCON.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    [HttpPost]
    [Route("rcon/{gameServerId}/kick/{clientId}")]
    public async Task<IActionResult> KickPlayer(Guid gameServerId, int clientId)
    {
        var response = await ((IRconApi)this).KickPlayer(gameServerId, clientId);

        return response.ToHttpResult();
    }

    async Task<ApiResult> IRconApi.KickPlayer(Guid gameServerId, int clientId)
    {
        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon").ConfigureAwait(false);


        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);


        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return new ApiResponse(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult();
        }

        var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, rconPassword);

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconKickPlayer");
        operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
        operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

        try
        {
            var result = await rconClient.KickPlayer(clientId);

            auditLogger.LogAudit(AuditEvent.ServerAction("RconKickPlayer", AuditAction.Moderate)
                .WithGameContext(gameServerApiResponse.Result.Data.GameType.ToString(), gameServerApiResponse.Result.Data.GameServerId)
                .WithTarget(clientId.ToString(), "Player")
                .WithSource("RconController")
                .WithProperty("Result", result.ToString())
                .Build());

            await TryWriteOperatorEventAsync(gameServerApiResponse.Result.Data.GameServerId, "RconKickPlayer", new
            {
                ClientId = clientId,
                Result = result.ToString()
            }).ConfigureAwait(false);

            return new ApiResponse().ToApiResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogWarning(ex, "Kick player operation not implemented for game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The kick player operation is not implemented for this game server type.")).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to kick player {ClientId} from game server {GameServerId}", clientId, gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to kick player from the game server via RCON.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    [HttpPost]
    [Route("rcon/{gameServerId}/ban/{clientId}")]
    public async Task<IActionResult> BanPlayer(Guid gameServerId, int clientId)
    {
        var response = await ((IRconApi)this).BanPlayer(gameServerId, clientId);

        return response.ToHttpResult();
    }

    async Task<ApiResult> IRconApi.BanPlayer(Guid gameServerId, int clientId)
    {
        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon").ConfigureAwait(false);


        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);


        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return new ApiResponse(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult();
        }

        var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, rconPassword);

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconBanPlayer");
        operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
        operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

        try
        {
            var result = await rconClient.BanPlayer(clientId);

            auditLogger.LogAudit(AuditEvent.ServerAction("RconBanPlayer", AuditAction.Moderate)
                .WithGameContext(gameServerApiResponse.Result.Data.GameType.ToString(), gameServerApiResponse.Result.Data.GameServerId)
                .WithTarget(clientId.ToString(), "Player")
                .WithSource("RconController")
                .WithProperty("Result", result.ToString())
                .Build());

            await TryWriteOperatorEventAsync(gameServerApiResponse.Result.Data.GameServerId, "RconBanPlayer", new
            {
                ClientId = clientId,
                Result = result.ToString()
            }).ConfigureAwait(false);

            return new ApiResponse().ToApiResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogWarning(ex, "Ban player operation not implemented for game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The ban player operation is not implemented for this game server type.")).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to ban player {ClientId} from game server {GameServerId}", clientId, gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to ban player from the game server via RCON.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    [HttpPost]
    [Route("rcon/{gameServerId}/restart")]
    public async Task<IActionResult> Restart(Guid gameServerId)
    {
        var response = await ((IRconApi)this).Restart(gameServerId);

        return response.ToHttpResult();
    }

    async Task<ApiResult> IRconApi.Restart(Guid gameServerId)
    {
        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon").ConfigureAwait(false);


        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);


        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return new ApiResponse(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult();
        }

        var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, rconPassword);

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconRestart");
        operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
        operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

        try
        {
            await rconClient.Restart();

            auditLogger.LogAudit(AuditEvent.ServerAction("RconRestart", AuditAction.Execute)
                .WithGameContext(gameServerApiResponse.Result.Data.GameType.ToString(), gameServerApiResponse.Result.Data.GameServerId)
                .WithSource("RconController")
                .Build());

            await TryWriteOperatorEventAsync(gameServerApiResponse.Result.Data.GameServerId, "RconRestart", new
            {
                Operation = "Restart"
            }).ConfigureAwait(false);

            return new ApiResponse().ToApiResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogWarning(ex, "Restart operation not implemented for game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The restart operation is not implemented for this game server type.")).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to restart game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to restart the game server via RCON.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    [HttpPost]
    [Route("rcon/{gameServerId}/restart-map")]
    public async Task<IActionResult> RestartMap(Guid gameServerId)
    {
        var response = await ((IRconApi)this).RestartMap(gameServerId);

        return response.ToHttpResult();
    }

    async Task<ApiResult> IRconApi.RestartMap(Guid gameServerId)
    {
        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon").ConfigureAwait(false);


        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);


        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return new ApiResponse(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult();
        }

        var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, rconPassword);

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconRestartMap");
        operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
        operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

        try
        {
            await rconClient.RestartMap();

            auditLogger.LogAudit(AuditEvent.ServerAction("RconRestartMap", AuditAction.Execute)
                .WithGameContext(gameServerApiResponse.Result.Data.GameType.ToString(), gameServerApiResponse.Result.Data.GameServerId)
                .WithSource("RconController")
                .Build());

            await TryWriteOperatorEventAsync(gameServerApiResponse.Result.Data.GameServerId, "RconRestartMap", new
            {
                Operation = "RestartMap"
            }).ConfigureAwait(false);

            return new ApiResponse().ToApiResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogWarning(ex, "Restart map operation not implemented for game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The restart map operation is not implemented for this game server type.")).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to restart map on game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to restart map on the game server via RCON.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    [HttpPost]
    [Route("rcon/{gameServerId}/fast-restart-map")]
    public async Task<IActionResult> FastRestartMap(Guid gameServerId)
    {
        var response = await ((IRconApi)this).FastRestartMap(gameServerId);

        return response.ToHttpResult();
    }

    async Task<ApiResult> IRconApi.FastRestartMap(Guid gameServerId)
    {
        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon").ConfigureAwait(false);


        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);


        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return new ApiResponse(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult();
        }

        var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, rconPassword);

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconFastRestartMap");
        operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
        operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

        try
        {
            await rconClient.FastRestartMap();

            auditLogger.LogAudit(AuditEvent.ServerAction("RconFastRestartMap", AuditAction.Execute)
                .WithGameContext(gameServerApiResponse.Result.Data.GameType.ToString(), gameServerApiResponse.Result.Data.GameServerId)
                .WithSource("RconController")
                .Build());

            await TryWriteOperatorEventAsync(gameServerApiResponse.Result.Data.GameServerId, "RconFastRestartMap", new
            {
                Operation = "FastRestartMap"
            }).ConfigureAwait(false);

            return new ApiResponse().ToApiResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogWarning(ex, "Fast restart map operation not implemented for game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The fast restart map operation is not implemented for this game server type.")).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to fast restart map on game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to fast restart map on the game server via RCON.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    [HttpPost]
    [Route("rcon/{gameServerId}/next-map")]
    public async Task<IActionResult> NextMap(Guid gameServerId)
    {
        var response = await ((IRconApi)this).NextMap(gameServerId);

        return response.ToHttpResult();
    }

    async Task<ApiResult> IRconApi.NextMap(Guid gameServerId)
    {
        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon").ConfigureAwait(false);


        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);


        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return new ApiResponse(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult();
        }

        var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, rconPassword);

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconNextMap");
        operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
        operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

        try
        {
            await rconClient.NextMap();

            auditLogger.LogAudit(AuditEvent.ServerAction("RconNextMap", AuditAction.Execute)
                .WithGameContext(gameServerApiResponse.Result.Data.GameType.ToString(), gameServerApiResponse.Result.Data.GameServerId)
                .WithSource("RconController")
                .Build());

            await TryWriteOperatorEventAsync(gameServerApiResponse.Result.Data.GameServerId, "RconNextMap", new
            {
                Operation = "NextMap"
            }).ConfigureAwait(false);

            return new ApiResponse().ToApiResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogWarning(ex, "Next map operation not implemented for game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The next map operation is not implemented for this game server type.")).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to rotate to next map on game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to rotate to next map on the game server via RCON.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    [HttpPost]
    [Route("rcon/{gameServerId}/say")]
    public async Task<IActionResult> Say(Guid gameServerId, [FromBody] SayRequest? request)
    {
        if (request == null)
        {
            return BadRequest(new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Request body cannot be null.")).ToApiResult());
        }

        var response = await ((IRconApi)this).Say(gameServerId, request.Message);

        return response.ToHttpResult();
    }

    async Task<ApiResult> IRconApi.Say(Guid gameServerId, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Message cannot be null or empty.")).ToBadRequestResult();
        }

        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon").ConfigureAwait(false);


        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);


        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return new ApiResponse(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult();
        }

        var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, rconPassword);

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconSay");
        operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
        operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

        try
        {
            await rconClient.Say(message);

            auditLogger.LogAudit(AuditEvent.ServerAction("RconSay", AuditAction.Execute)
                .WithGameContext(gameServerApiResponse.Result.Data.GameType.ToString(), gameServerApiResponse.Result.Data.GameServerId)
                .WithSource("RconController")
                .WithProperty("Message", message)
                .Build());

            await TryWriteOperatorEventAsync(gameServerApiResponse.Result.Data.GameServerId, "RconSay", new
            {
                Message = message
            }).ConfigureAwait(false);

            return new ApiResponse().ToApiResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogWarning(ex, "Say operation not implemented for game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The say operation is not implemented for this game server type.")).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to send message to game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to send message to the game server via RCON.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    [HttpPost]
    [Route("rcon/{gameServerId}/tell/{clientId}")]
    public async Task<IActionResult> TellPlayer(Guid gameServerId, int clientId, [FromBody] string message)
    {
        var response = await ((IRconApi)this).TellPlayer(gameServerId, clientId, message);

        return response.ToHttpResult();
    }

    async Task<ApiResult> IRconApi.TellPlayer(Guid gameServerId, int clientId, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Message cannot be null or empty.")).ToBadRequestResult();
        }

        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon").ConfigureAwait(false);


        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);


        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return new ApiResponse(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult();
        }

        var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, rconPassword);

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconTellPlayer");
        operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
        operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

        try
        {
            await rconClient.TellPlayer(clientId, message);

            auditLogger.LogAudit(AuditEvent.ServerAction("RconTellPlayer", AuditAction.Execute)
                .WithGameContext(gameServerApiResponse.Result.Data.GameType.ToString(), gameServerApiResponse.Result.Data.GameServerId)
                .WithTarget(clientId.ToString(), "Player")
                .WithSource("RconController")
                .Build());

            await TryWriteOperatorEventAsync(gameServerApiResponse.Result.Data.GameServerId, "RconTellPlayer", new
            {
                ClientId = clientId,
                Message = message
            }).ConfigureAwait(false);

            return new ApiResponse().ToApiResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogWarning(ex, "Tell player operation not implemented for game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The tell player operation is not implemented for this game server type.")).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to send message to player {ClientId} on game server {GameServerId}", clientId, gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to send message to player on the game server via RCON.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    [HttpPost]
    [Route("rcon/{gameServerId}/change-map")]
    public async Task<IActionResult> ChangeMap(Guid gameServerId, [FromBody] ChangeMapRequest? request)
    {
        if (request == null)
        {
            return BadRequest(new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Request body cannot be null.")).ToApiResult());
        }

        var response = await ((IRconApi)this).ChangeMap(gameServerId, request.MapName);

        return response.ToHttpResult();
    }

    async Task<ApiResult> IRconApi.ChangeMap(Guid gameServerId, string mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Map name cannot be null or empty.")).ToBadRequestResult();
        }

        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon").ConfigureAwait(false);


        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);


        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return new ApiResponse(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult();
        }

        var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, rconPassword);

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconChangeMap");
        operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
        operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

        try
        {
            await rconClient.ChangeMap(mapName);

            auditLogger.LogAudit(AuditEvent.ServerAction("RconChangeMap", AuditAction.Execute)
                .WithGameContext(gameServerApiResponse.Result.Data.GameType.ToString(), gameServerApiResponse.Result.Data.GameServerId)
                .WithSource("RconController")
                .WithProperty("MapName", mapName)
                .Build());

            await TryWriteOperatorEventAsync(gameServerApiResponse.Result.Data.GameServerId, "RconChangeMap", new
            {
                MapName = mapName
            }).ConfigureAwait(false);

            return new ApiResponse().ToApiResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogWarning(ex, "Change map operation not implemented for game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The change map operation is not implemented for this game server type.")).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to change map on game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to change map on the game server via RCON.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    [HttpPost]
    [Route("rcon/{gameServerId}/kick-player-by-name")]
    public async Task<IActionResult> KickPlayerByName(Guid gameServerId, [FromBody] string name)
    {
        var response = await ((IRconApi)this).KickPlayerByName(gameServerId, name);

        return response.ToHttpResult();
    }

    async Task<ApiResult> IRconApi.KickPlayerByName(Guid gameServerId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Player name cannot be null or empty.")).ToBadRequestResult();
        }

        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon").ConfigureAwait(false);


        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);


        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return new ApiResponse(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult();
        }

        var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, rconPassword);

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconKickPlayerByName");
        operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
        operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

        try
        {
            await rconClient.KickPlayerByName(name);

            await TryWriteOperatorEventAsync(gameServerApiResponse.Result.Data.GameServerId, "RconKickPlayerByName", new
            {
                Name = name
            }).ConfigureAwait(false);

            return new ApiResponse().ToApiResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogWarning(ex, "Kick player by name operation not implemented for game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The kick player by name operation is not implemented for this game server type.")).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to kick player by name {Name} from game server {GameServerId}", name, gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to kick player from the game server via RCON.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    [HttpPost]
    [Route("rcon/{gameServerId}/kick-all-players")]
    public async Task<IActionResult> KickAllPlayers(Guid gameServerId)
    {
        var response = await ((IRconApi)this).KickAllPlayers(gameServerId);

        return response.ToHttpResult();
    }

    async Task<ApiResult> IRconApi.KickAllPlayers(Guid gameServerId)
    {
        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon").ConfigureAwait(false);


        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);


        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return new ApiResponse(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult();
        }

        var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, rconPassword);

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconKickAllPlayers");
        operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
        operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

        try
        {
            await rconClient.KickAllPlayers();

            await TryWriteOperatorEventAsync(gameServerApiResponse.Result.Data.GameServerId, "RconKickAllPlayers", new
            {
                Operation = "KickAllPlayers"
            }).ConfigureAwait(false);

            return new ApiResponse().ToApiResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogWarning(ex, "Kick all players operation not implemented for game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The kick all players operation is not implemented for this game server type.")).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to kick all players from game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to kick all players from the game server via RCON.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    [HttpPost]
    [Route("rcon/{gameServerId}/ban-player-by-name")]
    public async Task<IActionResult> BanPlayerByName(Guid gameServerId, [FromBody] string name)
    {
        var response = await ((IRconApi)this).BanPlayerByName(gameServerId, name);

        return response.ToHttpResult();
    }

    async Task<ApiResult> IRconApi.BanPlayerByName(Guid gameServerId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Player name cannot be null or empty.")).ToBadRequestResult();
        }

        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon").ConfigureAwait(false);


        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);


        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return new ApiResponse(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult();
        }

        var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, rconPassword);

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconBanPlayerByName");
        operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
        operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

        try
        {
            await rconClient.BanPlayerByName(name);

            await TryWriteOperatorEventAsync(gameServerApiResponse.Result.Data.GameServerId, "RconBanPlayerByName", new
            {
                Name = name
            }).ConfigureAwait(false);

            return new ApiResponse().ToApiResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogWarning(ex, "Ban player by name operation not implemented for game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The ban player by name operation is not implemented for this game server type.")).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to ban player by name {Name} from game server {GameServerId}", name, gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to ban player from the game server via RCON.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    [HttpPost]
    [Route("rcon/{gameServerId}/tempban/{clientId}")]
    public async Task<IActionResult> TempBanPlayer(Guid gameServerId, int clientId)
    {
        var response = await ((IRconApi)this).TempBanPlayer(gameServerId, clientId);

        return response.ToHttpResult();
    }

    async Task<ApiResult> IRconApi.TempBanPlayer(Guid gameServerId, int clientId)
    {
        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon").ConfigureAwait(false);


        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);


        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return new ApiResponse(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult();
        }

        var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, rconPassword);

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconTempBanPlayer");
        operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
        operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

        try
        {
            await rconClient.TempBanPlayer(clientId);

            await TryWriteOperatorEventAsync(gameServerApiResponse.Result.Data.GameServerId, "RconTempBanPlayer", new
            {
                ClientId = clientId
            }).ConfigureAwait(false);

            return new ApiResponse().ToApiResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogWarning(ex, "Temp ban player operation not implemented for game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The temporary ban player operation is not implemented for this game server type.")).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to temp ban player {ClientId} from game server {GameServerId}", clientId, gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to temporarily ban player from the game server via RCON.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    [HttpPost]
    [Route("rcon/{gameServerId}/tempban-player-by-name")]
    public async Task<IActionResult> TempBanPlayerByName(Guid gameServerId, [FromBody] string name)
    {
        var response = await ((IRconApi)this).TempBanPlayerByName(gameServerId, name);

        return response.ToHttpResult();
    }

    async Task<ApiResult> IRconApi.TempBanPlayerByName(Guid gameServerId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Player name cannot be null or empty.")).ToBadRequestResult();
        }

        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon").ConfigureAwait(false);


        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);


        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return new ApiResponse(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult();
        }

        var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, rconPassword);

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconTempBanPlayerByName");
        operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
        operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

        try
        {
            await rconClient.TempBanPlayerByName(name);

            await TryWriteOperatorEventAsync(gameServerApiResponse.Result.Data.GameServerId, "RconTempBanPlayerByName", new
            {
                Name = name
            }).ConfigureAwait(false);

            return new ApiResponse().ToApiResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogWarning(ex, "Temp ban player by name operation not implemented for game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The temporary ban player by name operation is not implemented for this game server type.")).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to temp ban player by name {Name} from game server {GameServerId}", name, gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to temporarily ban player from the game server via RCON.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    [HttpPost]
    [Route("rcon/{gameServerId}/unban-player")]
    public async Task<IActionResult> UnbanPlayer(Guid gameServerId, [FromBody] string name)
    {
        var response = await ((IRconApi)this).UnbanPlayer(gameServerId, name);

        return response.ToHttpResult();
    }

    async Task<ApiResult> IRconApi.UnbanPlayer(Guid gameServerId, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Player name cannot be null or empty.")).ToBadRequestResult();
        }

        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon").ConfigureAwait(false);


        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);


        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return new ApiResponse(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult();
        }

        var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, rconPassword);

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconUnbanPlayer");
        operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
        operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

        try
        {
            await rconClient.UnbanPlayer(name);

            await TryWriteOperatorEventAsync(gameServerApiResponse.Result.Data.GameServerId, "RconUnbanPlayer", new
            {
                Name = name
            }).ConfigureAwait(false);

            return new ApiResponse().ToApiResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogWarning(ex, "Unban player operation not implemented for game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The unban player operation is not implemented for this game server type.")).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to unban player {Name} from game server {GameServerId}", name, gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to unban player from the game server via RCON.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    [HttpGet]
    [Route("rcon/{gameServerId}/server-info")]
    public async Task<IActionResult> GetServerInfo(Guid gameServerId)
    {
        var response = await ((IRconApi)this).GetServerInfo(gameServerId);

        return response.ToHttpResult();
    }

    async Task<ApiResult<string>> IRconApi.GetServerInfo(Guid gameServerId)
    {
        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon").ConfigureAwait(false);


        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);


        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult();
        }

        var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, rconPassword);

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconGetServerInfo");
        operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
        operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

        try
        {
            var result = await rconClient.GetServerInfo();
            return new ApiResponse<string>(result).ToApiResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogWarning(ex, "Get server info operation not implemented for game server {GameServerId}", gameServerId);
            return new ApiResponse<string>(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The get server info operation is not implemented for this game server type.")).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to get server info from game server {GameServerId}", gameServerId);
            return new ApiResponse<string>(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to get server info from the game server via RCON.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    [HttpGet]
    [Route("rcon/{gameServerId}/system-info")]
    public async Task<IActionResult> GetSystemInfo(Guid gameServerId)
    {
        var response = await ((IRconApi)this).GetSystemInfo(gameServerId);

        return response.ToHttpResult();
    }

    async Task<ApiResult<string>> IRconApi.GetSystemInfo(Guid gameServerId)
    {
        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon").ConfigureAwait(false);


        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);


        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult();
        }

        var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, rconPassword);

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconGetSystemInfo");
        operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
        operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

        try
        {
            var result = await rconClient.GetSystemInfo();
            return new ApiResponse<string>(result).ToApiResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogWarning(ex, "Get system info operation not implemented for game server {GameServerId}", gameServerId);
            return new ApiResponse<string>(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The get system info operation is not implemented for this game server type.")).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to get system info from game server {GameServerId}", gameServerId);
            return new ApiResponse<string>(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to get system info from the game server via RCON.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    [HttpGet]
    [Route("rcon/{gameServerId}/command-list")]
    public async Task<IActionResult> GetCommandList(Guid gameServerId)
    {
        var response = await ((IRconApi)this).GetCommandList(gameServerId);

        return response.ToHttpResult();
    }

    async Task<ApiResult<string>> IRconApi.GetCommandList(Guid gameServerId)
    {
        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon").ConfigureAwait(false);


        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);


        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult();
        }

        var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, rconPassword);

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconGetCommandList");
        operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
        operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

        try
        {
            var result = await rconClient.GetCommandList();
            return new ApiResponse<string>(result).ToApiResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogWarning(ex, "Get command list operation not implemented for game server {GameServerId}", gameServerId);
            return new ApiResponse<string>(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The get command list operation is not implemented for this game server type.")).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to get command list from game server {GameServerId}", gameServerId);
            return new ApiResponse<string>(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to get command list from the game server via RCON.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    [HttpPost]
    [Route("rcon/{gameServerId}/kick/{clientId}/verify")]
    public async Task<IActionResult> KickPlayerWithVerification(Guid gameServerId, int clientId, [FromBody] string? expectedPlayerName)
    {
        var response = await ((IRconApi)this).KickPlayerWithVerification(gameServerId, clientId, expectedPlayerName);

        return response.ToHttpResult();
    }

    async Task<ApiResult> IRconApi.KickPlayerWithVerification(Guid gameServerId, int clientId, string? expectedPlayerName)
    {
        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon").ConfigureAwait(false);


        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);


        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return new ApiResponse(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult();
        }

        var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, rconPassword);

        // Verify player name if provided
        if (!string.IsNullOrWhiteSpace(expectedPlayerName))
        {
            var verificationError = VerifyPlayerInSlot(rconClient, clientId, expectedPlayerName, gameServerId);
            if (verificationError != null)
            {
                return verificationError.ToBadRequestResult();
            }
        }

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconKickPlayerWithVerification");
        operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
        operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

        try
        {
            var result = await rconClient.KickPlayer(clientId);

            auditLogger.LogAudit(AuditEvent.ServerAction("RconKickPlayerWithVerification", AuditAction.Moderate)
                .WithGameContext(gameServerApiResponse.Result.Data.GameType.ToString(), gameServerApiResponse.Result.Data.GameServerId)
                .WithTarget(clientId.ToString(), "Player", expectedPlayerName)
                .WithSource("RconController")
                .WithProperty("Result", result.ToString())
                .Build());

            await TryWriteOperatorEventAsync(gameServerApiResponse.Result.Data.GameServerId, "RconKickPlayerWithVerification", new
            {
                ClientId = clientId,
                ExpectedPlayerName = expectedPlayerName,
                Result = result.ToString()
            }).ConfigureAwait(false);

            return new ApiResponse().ToApiResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogWarning(ex, "Kick player operation not implemented for game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The kick player operation is not implemented for this game server type.")).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to kick player {ClientId} from game server {GameServerId}", clientId, gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to kick player from the game server via RCON.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    [HttpPost]
    [Route("rcon/{gameServerId}/ban/{clientId}/verify")]
    public async Task<IActionResult> BanPlayerWithVerification(Guid gameServerId, int clientId, [FromBody] string? expectedPlayerName)
    {
        var response = await ((IRconApi)this).BanPlayerWithVerification(gameServerId, clientId, expectedPlayerName);

        return response.ToHttpResult();
    }

    async Task<ApiResult> IRconApi.BanPlayerWithVerification(Guid gameServerId, int clientId, string? expectedPlayerName)
    {
        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon").ConfigureAwait(false);


        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);


        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return new ApiResponse(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult();
        }

        var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, rconPassword);

        // Verify player name if provided
        if (!string.IsNullOrWhiteSpace(expectedPlayerName))
        {
            var verificationError = VerifyPlayerInSlot(rconClient, clientId, expectedPlayerName, gameServerId);
            if (verificationError != null)
            {
                return verificationError.ToBadRequestResult();
            }
        }

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconBanPlayerWithVerification");
        operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
        operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

        try
        {
            var result = await rconClient.BanPlayer(clientId);

            auditLogger.LogAudit(AuditEvent.ServerAction("RconBanPlayerWithVerification", AuditAction.Moderate)
                .WithGameContext(gameServerApiResponse.Result.Data.GameType.ToString(), gameServerApiResponse.Result.Data.GameServerId)
                .WithTarget(clientId.ToString(), "Player", expectedPlayerName)
                .WithSource("RconController")
                .WithProperty("Result", result.ToString())
                .Build());

            await TryWriteOperatorEventAsync(gameServerApiResponse.Result.Data.GameServerId, "RconBanPlayerWithVerification", new
            {
                ClientId = clientId,
                ExpectedPlayerName = expectedPlayerName,
                Result = result.ToString()
            }).ConfigureAwait(false);

            return new ApiResponse().ToApiResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogWarning(ex, "Ban player operation not implemented for game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The ban player operation is not implemented for this game server type.")).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to ban player {ClientId} from game server {GameServerId}", clientId, gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to ban player from the game server via RCON.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    [HttpPost]
    [Route("rcon/{gameServerId}/tempban/{clientId}/verify")]
    public async Task<IActionResult> TempBanPlayerWithVerification(Guid gameServerId, int clientId, [FromBody] string? expectedPlayerName)
    {
        var response = await ((IRconApi)this).TempBanPlayerWithVerification(gameServerId, clientId, expectedPlayerName);

        return response.ToHttpResult();
    }

    async Task<ApiResult> IRconApi.TempBanPlayerWithVerification(Guid gameServerId, int clientId, string? expectedPlayerName)
    {
        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon").ConfigureAwait(false);


        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);


        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return new ApiResponse(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult();
        }

        var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, rconPassword);

        // Verify player name if provided
        if (!string.IsNullOrWhiteSpace(expectedPlayerName))
        {
            var verificationError = VerifyPlayerInSlot(rconClient, clientId, expectedPlayerName, gameServerId);
            if (verificationError != null)
            {
                return verificationError.ToBadRequestResult();
            }
        }

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconTempBanPlayerWithVerification");
        operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
        operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

        try
        {
            var result = await rconClient.TempBanPlayer(clientId);

            auditLogger.LogAudit(AuditEvent.ServerAction("RconTempBanPlayerWithVerification", AuditAction.Moderate)
                .WithGameContext(gameServerApiResponse.Result.Data.GameType.ToString(), gameServerApiResponse.Result.Data.GameServerId)
                .WithTarget(clientId.ToString(), "Player", expectedPlayerName)
                .WithSource("RconController")
                .WithProperty("Result", result.ToString())
                .Build());

            await TryWriteOperatorEventAsync(gameServerApiResponse.Result.Data.GameServerId, "RconTempBanPlayerWithVerification", new
            {
                ClientId = clientId,
                ExpectedPlayerName = expectedPlayerName,
                Result = result.ToString()
            }).ConfigureAwait(false);

            return new ApiResponse().ToApiResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogWarning(ex, "Temp ban player operation not implemented for game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The temporary ban player operation is not implemented for this game server type.")).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to temp ban player {ClientId} from game server {GameServerId}", clientId, gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to temporarily ban player from the game server via RCON.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    [HttpPost]
    [Route("rcon/{gameServerId}/tell/{clientId}/verify")]
    public async Task<IActionResult> TellPlayerWithVerification(Guid gameServerId, int clientId, [FromBody] TellPlayerWithVerificationRequest request)
    {
        var response = await ((IRconApi)this).TellPlayerWithVerification(gameServerId, clientId, request.Message, request.ExpectedPlayerName);

        return response.ToHttpResult();
    }

    async Task<ApiResult> IRconApi.TellPlayerWithVerification(Guid gameServerId, int clientId, string message, string? expectedPlayerName)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Message cannot be null or empty.")).ToBadRequestResult();
        }

        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon").ConfigureAwait(false);


        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);


        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return new ApiResponse(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult();
        }

        var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, rconPassword);

        // Verify player name if provided
        if (!string.IsNullOrWhiteSpace(expectedPlayerName))
        {
            var verificationError = VerifyPlayerInSlot(rconClient, clientId, expectedPlayerName, gameServerId);
            if (verificationError != null)
            {
                return verificationError.ToBadRequestResult();
            }
        }

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconTellPlayerWithVerification");
        operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
        operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

        try
        {
            await rconClient.TellPlayer(clientId, message);

            auditLogger.LogAudit(AuditEvent.ServerAction("RconTellPlayerWithVerification", AuditAction.Execute)
                .WithGameContext(gameServerApiResponse.Result.Data.GameType.ToString(), gameServerApiResponse.Result.Data.GameServerId)
                .WithTarget(clientId.ToString(), "Player", expectedPlayerName)
                .WithSource("RconController")
                .Build());

            await TryWriteOperatorEventAsync(gameServerApiResponse.Result.Data.GameServerId, "RconTellPlayerWithVerification", new
            {
                ClientId = clientId,
                ExpectedPlayerName = expectedPlayerName,
                Message = message
            }).ConfigureAwait(false);

            return new ApiResponse().ToApiResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogWarning(ex, "Tell player operation not implemented for game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The tell player operation is not implemented for this game server type.")).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to send message to player {ClientId} on game server {GameServerId}", clientId, gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to send message to player on the game server via RCON.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    [HttpPost]
    [Route("rcon/{gameServerId}/resolve-player")]
    public async Task<IActionResult> ResolvePlayer(Guid gameServerId, [FromBody] ResolvePlayerRequestDto? request)
    {
        if (request == null)
        {
            return BadRequest(new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Request body cannot be null.")).ToApiResult());
        }

        var response = await ((IRconApi)this).ResolvePlayer(gameServerId, request, HttpContext.RequestAborted);

        return response.ToHttpResult();
    }

    async Task<ApiResult<ResolvePlayerResponseDto>> IRconApi.ResolvePlayer(Guid gameServerId, ResolvePlayerRequestDto request, CancellationToken cancellationToken)
    {
        var playerQuery = request.PlayerQuery?.Trim();
        if (string.IsNullOrWhiteSpace(playerQuery))
        {
            return new ApiResponse<ResolvePlayerResponseDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "Player query cannot be null or empty.")).ToBadRequestResult();
        }

        var maxSuggestions = request.MaxSuggestions ?? 3;
        if (maxSuggestions is < 1 or > 5)
        {
            return new ApiResponse<ResolvePlayerResponseDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "Max suggestions must be between 1 and 5.")).ToBadRequestResult();
        }

        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId, cancellationToken);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse<ResolvePlayerResponseDto>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon", cancellationToken).ConfigureAwait(false);
        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);

        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return new ApiResponse<ResolvePlayerResponseDto>(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult();
        }

        var rconClient = rconClientFactory.CreateInstance(
            gameServerApiResponse.Result.Data.GameType,
            gameServerApiResponse.Result.Data.GameServerId,
            gameServerApiResponse.Result.Data.Hostname,
            gameServerApiResponse.Result.Data.QueryPort,
            rconPassword);

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconResolvePlayer");
        operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
        operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

        try
        {
            var players = rconClient.GetPlayers();
            var resolution = PlayerResolutionMatcher.ResolvePlayer(players, playerQuery, maxSuggestions);

            logger.LogDebug(
                "Resolved player query on game server {GameServerId}: status={Status}, playerCount={PlayerCount}, suggestionCount={SuggestionCount}",
                gameServerId,
                resolution.Status,
                players?.Count ?? 0,
                resolution.Suggestions.Count);

            return new ApiResponse<ResolvePlayerResponseDto>(resolution).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to resolve player for query {PlayerQuery} on game server {GameServerId}", playerQuery, gameServerId);
            return new ApiResponse<ResolvePlayerResponseDto>(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to resolve player on the game server via RCON.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    [HttpPost]
    [Route("rcon/{gameServerId}/screenshot")]
    public async Task<IActionResult> TakeScreenshot(Guid gameServerId, [FromBody] TakeScreenshotRequestDto? request)
    {
        if (request == null)
        {
            return BadRequest(new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Request body cannot be null.")).ToApiResult());
        }

        var response = await ((IRconApi)this).TakeScreenshot(gameServerId, request, HttpContext.RequestAborted);

        return response.ToHttpResult();
    }

    async Task<ApiResult> IRconApi.TakeScreenshot(Guid gameServerId, TakeScreenshotRequestDto request, CancellationToken cancellationToken)
    {
        var playerIdentifier = request.PlayerIdentifier?.Trim();
        if (string.IsNullOrWhiteSpace(playerIdentifier) || !CoD4xPlayerIdentifierRegex.IsMatch(playerIdentifier))
        {
            return new ApiResponse(new ApiError(ErrorCodes.INVALID_PLAYER_IDENTIFIER, "Player identifier must be a numeric CoD4x identifier between 17 and 21 digits.")).ToBadRequestResult();
        }

        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId, cancellationToken);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        if (gameServerApiResponse.Result.Data.GameType != GameType.CallOfDuty4x)
        {
            logger.LogWarning("Screenshot command requested for unsupported game type {GameType} on game server {GameServerId}", gameServerApiResponse.Result.Data.GameType, gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.OPERATION_NOT_SUPPORTED_FOR_GAME_TYPE, "Screenshot command is only supported for CoD4x game servers.")).ToBadRequestResult();
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon", cancellationToken).ConfigureAwait(false);
        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);

        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return new ApiResponse(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult();
        }

        var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, rconPassword);

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconTakeScreenshot");
        operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
        operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

        try
        {
            var result = await rconClient.TakeScreenshot(playerIdentifier, cancellationToken);

            auditLogger.LogAudit(AuditEvent.ServerAction("RconTakeScreenshot", AuditAction.Execute)
                .WithGameContext(gameServerApiResponse.Result.Data.GameType.ToString(), gameServerApiResponse.Result.Data.GameServerId)
                .WithTarget(playerIdentifier, "Player")
                .WithSource("RconController")
                .WithProperty("Result", result.ToString())
                .Build());

            await TryWriteOperatorEventAsync(gameServerApiResponse.Result.Data.GameServerId, "RconTakeScreenshot", new
            {
                PlayerIdentifier = playerIdentifier,
                Result = result.ToString()
            }, cancellationToken).ConfigureAwait(false);

            return new ApiResponse().ToApiResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogWarning(ex, "Take screenshot operation not implemented for game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The take screenshot operation is not implemented for this game server type.")).ToApiResult();
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

            logger.LogError(ex, "Failed to take screenshot for player identifier {PlayerIdentifier} on game server {GameServerId}", playerIdentifier, gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to execute screenshot command on the game server via RCON.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    [HttpGet]
    [Route("rcon/{gameServerId}/dvar/{dvarName}")]
    public async Task<IActionResult> GetDvar(Guid gameServerId, string dvarName)
    {
        var response = await ((IRconApi)this).GetDvar(gameServerId, dvarName);

        return response.ToHttpResult();
    }

    async Task<ApiResult<DvarValueDto>> IRconApi.GetDvar(Guid gameServerId, string dvarName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dvarName) || !Regex.IsMatch(dvarName, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
        {
            return new ApiResponse<DvarValueDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "Dvar name must be a valid identifier (letters, digits, underscores).")).ToBadRequestResult();
        }

        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse<DvarValueDto>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon").ConfigureAwait(false);


        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);


        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return new ApiResponse<DvarValueDto>(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult();
        }

        var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, rconPassword);

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconGetDvar");
        operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
        operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

        try
        {
            var response = await rconClient.GetDvar(dvarName);
            var normalizedResponse = QuakeColorCodeRegex.Replace(response ?? string.Empty, string.Empty).Trim();

            if (normalizedResponse.StartsWith("Bad command or cvar:", StringComparison.OrdinalIgnoreCase))
            {
                return new ApiResponse<DvarValueDto>(new ApiError(ErrorCodes.DVAR_NOT_FOUND, $"The dvar '{dvarName}' was not found on the game server.")).ToNotFoundResult();
            }

            // Parse the dvar response - Quake3 format: "dvarName" is: "value"
            var dvarMatch = DvarResponseRegex.Match(normalizedResponse);
            var dvarValue = dvarMatch.Success ? dvarMatch.Groups[2].Value : normalizedResponse;

            var dto = new DvarValueDto(dvarName, dvarValue);
            return new ApiResponse<DvarValueDto>(dto).ToApiResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogWarning(ex, "Get dvar operation not implemented for game server {GameServerId}", gameServerId);
            return new ApiResponse<DvarValueDto>(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The get dvar operation is not implemented for this game server type.")).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to get dvar on game server {GameServerId}", gameServerId);
            return new ApiResponse<DvarValueDto>(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to get dvar from the game server via RCON.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    [HttpPost]
    [Route("rcon/{gameServerId}/dvar/{dvarName}")]
    public async Task<IActionResult> SetDvar(Guid gameServerId, string dvarName, [FromBody] SetDvarRequest? request)
    {
        if (request == null)
        {
            return BadRequest(new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Request body cannot be null.")).ToApiResult());
        }

        var response = await ((IRconApi)this).SetDvar(gameServerId, dvarName, request.Value);

        return response.ToHttpResult();
    }

    async Task<ApiResult> IRconApi.SetDvar(Guid gameServerId, string dvarName, string value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dvarName) || !Regex.IsMatch(dvarName, @"^[a-zA-Z_][a-zA-Z0-9_]*$"))
        {
            return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Dvar name must be a valid identifier (letters, digits, underscores).")).ToBadRequestResult();
        }

        // Reject values with characters that could break RCON quoting
        if (value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Value must not contain double quotes or newline characters.")).ToBadRequestResult();
        }

        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon").ConfigureAwait(false);


        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);


        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return new ApiResponse(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult();
        }

        var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, rconPassword);

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconSetDvar");
        operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
        operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

        try
        {
            await rconClient.SetDvar(dvarName, value);

            auditLogger.LogAudit(AuditEvent.ServerAction("RconSetDvar", AuditAction.Execute)
                .WithGameContext(gameServerApiResponse.Result.Data.GameType.ToString(), gameServerApiResponse.Result.Data.GameServerId)
                .WithSource("RconController")
                .WithProperty("DvarName", dvarName)
                .WithProperty("Value", value)
                .Build());

            await TryWriteOperatorEventAsync(gameServerApiResponse.Result.Data.GameServerId, "RconSetDvar", new
            {
                DvarName = dvarName,
                Value = value
            }, cancellationToken).ConfigureAwait(false);

            return new ApiResponse().ToApiResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogWarning(ex, "Set dvar operation not implemented for game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The set dvar operation is not implemented for this game server type.")).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to set dvar on game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to set dvar on the game server via RCON.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }
}