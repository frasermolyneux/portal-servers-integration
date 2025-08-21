using System.Net;
using Asp.Versioning;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MX.Api.Abstractions;
using MX.Api.Web.Extensions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Constants;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1
{
    [ApiController]
    [Authorize(Roles = "ServiceAccount")]
    [ApiVersion(ApiVersions.V1)]
    [Route("api/v{version:apiVersion}")]
    public class RconController : Controller, IRconApi
    {
        private readonly ILogger<RconController> logger;
        private readonly IRepositoryApiClient repositoryApiClient;
        private readonly IRconClientFactory rconClientFactory;
        private readonly TelemetryClient telemetryClient;

        public RconController(
            ILogger<RconController> logger,
            IRepositoryApiClient repositoryApiClient,
            IRconClientFactory rconClientFactory,
            TelemetryClient telemetryClient)
        {
            this.logger = logger;
            this.repositoryApiClient = repositoryApiClient;
            this.rconClientFactory = rconClientFactory;
            this.telemetryClient = telemetryClient;
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
                return new ApiResponse<ServerRconStatusResponseDto>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

            if (string.IsNullOrWhiteSpace(gameServerApiResponse.Result.Data.RconPassword))
                return new ApiResponse<ServerRconStatusResponseDto>(new ApiError(ErrorCodes.RCON_PASSWORD_NOT_CONFIGURED, "The game server does not have an RCON password configured.")).ToBadRequestResult();

            var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, gameServerApiResponse.Result.Data.RconPassword);

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
                return new ApiResponse<RconMapCollectionDto>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

            if (string.IsNullOrWhiteSpace(gameServerApiResponse.Result.Data.RconPassword))
                return new ApiResponse<RconMapCollectionDto>(new ApiError(ErrorCodes.RCON_PASSWORD_NOT_CONFIGURED, "The game server does not have an RCON password configured.")).ToBadRequestResult();

            var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, gameServerApiResponse.Result.Data.RconPassword);

            var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconMapRotation");
            operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
            operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

            try
            {
                var statusResult = await rconClient.GetMaps();

                if (statusResult != null && statusResult.Any())
                {
                    var maps = statusResult.Select(m => new RconMapDto(m.GameType, m.MapName)).ToList();
                    var result = new RconMapCollectionDto(maps, maps.Count, maps.Count);

                    return new ApiResponse<RconMapCollectionDto>(result).ToApiResult();
                }
                else
                {
                    var emptyResult = new RconMapCollectionDto(new List<RconMapDto>(), 0, 0);
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
                return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

            if (string.IsNullOrWhiteSpace(gameServerApiResponse.Result.Data.RconPassword))
                return new ApiResponse(new ApiError(ErrorCodes.RCON_PASSWORD_NOT_CONFIGURED, "The game server does not have an RCON password configured.")).ToBadRequestResult();

            var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, gameServerApiResponse.Result.Data.RconPassword);

            var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconKickPlayer");
            operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
            operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

            try
            {
                var result = await rconClient.KickPlayer(clientId);

                telemetryClient.TrackEvent("RconKickPlayer", new Dictionary<string, string>
                {
                    { "GameServerId", gameServerApiResponse.Result.Data.GameServerId.ToString() },
                    { "ClientId", clientId.ToString() },
                    { "Result", result.ToString() }
                });

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
                return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

            if (string.IsNullOrWhiteSpace(gameServerApiResponse.Result.Data.RconPassword))
                return new ApiResponse(new ApiError(ErrorCodes.RCON_PASSWORD_NOT_CONFIGURED, "The game server does not have an RCON password configured.")).ToBadRequestResult();

            var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, gameServerApiResponse.Result.Data.RconPassword);

            var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconBanPlayer");
            operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
            operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

            try
            {
                var result = await rconClient.BanPlayer(clientId);

                telemetryClient.TrackEvent("RconBanPlayer", new Dictionary<string, string>
                {
                    { "GameServerId", gameServerApiResponse.Result.Data.GameServerId.ToString() },
                    { "ClientId", clientId.ToString() },
                    { "Result", result.ToString() }
                });

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
    }
}
