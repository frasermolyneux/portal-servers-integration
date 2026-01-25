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
                return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

            if (string.IsNullOrWhiteSpace(gameServerApiResponse.Result.Data.RconPassword))
                return new ApiResponse(new ApiError(ErrorCodes.RCON_PASSWORD_NOT_CONFIGURED, "The game server does not have an RCON password configured.")).ToBadRequestResult();

            var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, gameServerApiResponse.Result.Data.RconPassword);

            var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconRestart");
            operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
            operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

            try
            {
                await rconClient.Restart();
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
                return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

            if (string.IsNullOrWhiteSpace(gameServerApiResponse.Result.Data.RconPassword))
                return new ApiResponse(new ApiError(ErrorCodes.RCON_PASSWORD_NOT_CONFIGURED, "The game server does not have an RCON password configured.")).ToBadRequestResult();

            var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, gameServerApiResponse.Result.Data.RconPassword);

            var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconRestartMap");
            operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
            operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

            try
            {
                await rconClient.RestartMap();
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
                return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

            if (string.IsNullOrWhiteSpace(gameServerApiResponse.Result.Data.RconPassword))
                return new ApiResponse(new ApiError(ErrorCodes.RCON_PASSWORD_NOT_CONFIGURED, "The game server does not have an RCON password configured.")).ToBadRequestResult();

            var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, gameServerApiResponse.Result.Data.RconPassword);

            var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconFastRestartMap");
            operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
            operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

            try
            {
                await rconClient.FastRestartMap();
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
                return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

            if (string.IsNullOrWhiteSpace(gameServerApiResponse.Result.Data.RconPassword))
                return new ApiResponse(new ApiError(ErrorCodes.RCON_PASSWORD_NOT_CONFIGURED, "The game server does not have an RCON password configured.")).ToBadRequestResult();

            var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, gameServerApiResponse.Result.Data.RconPassword);

            var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconNextMap");
            operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
            operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

            try
            {
                await rconClient.NextMap();
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
        public async Task<IActionResult> Say(Guid gameServerId, [FromBody] string message)
        {
            var response = await ((IRconApi)this).Say(gameServerId, message);

            return response.ToHttpResult();
        }

        async Task<ApiResult> IRconApi.Say(Guid gameServerId, string message)
        {
            var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

            if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
                return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

            if (string.IsNullOrWhiteSpace(gameServerApiResponse.Result.Data.RconPassword))
                return new ApiResponse(new ApiError(ErrorCodes.RCON_PASSWORD_NOT_CONFIGURED, "The game server does not have an RCON password configured.")).ToBadRequestResult();

            var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, gameServerApiResponse.Result.Data.RconPassword);

            var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconSay");
            operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
            operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

            try
            {
                await rconClient.Say(message);
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
            var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

            if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
                return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

            if (string.IsNullOrWhiteSpace(gameServerApiResponse.Result.Data.RconPassword))
                return new ApiResponse(new ApiError(ErrorCodes.RCON_PASSWORD_NOT_CONFIGURED, "The game server does not have an RCON password configured.")).ToBadRequestResult();

            var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, gameServerApiResponse.Result.Data.RconPassword);

            var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconTellPlayer");
            operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
            operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

            try
            {
                await rconClient.TellPlayer(clientId, message);
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
        public async Task<IActionResult> ChangeMap(Guid gameServerId, [FromBody] string mapName)
        {
            var response = await ((IRconApi)this).ChangeMap(gameServerId, mapName);

            return response.ToHttpResult();
        }

        async Task<ApiResult> IRconApi.ChangeMap(Guid gameServerId, string mapName)
        {
            var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

            if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
                return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

            if (string.IsNullOrWhiteSpace(gameServerApiResponse.Result.Data.RconPassword))
                return new ApiResponse(new ApiError(ErrorCodes.RCON_PASSWORD_NOT_CONFIGURED, "The game server does not have an RCON password configured.")).ToBadRequestResult();

            var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, gameServerApiResponse.Result.Data.RconPassword);

            var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconChangeMap");
            operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
            operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

            try
            {
                await rconClient.ChangeMap(mapName);
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
            var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

            if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
                return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

            if (string.IsNullOrWhiteSpace(gameServerApiResponse.Result.Data.RconPassword))
                return new ApiResponse(new ApiError(ErrorCodes.RCON_PASSWORD_NOT_CONFIGURED, "The game server does not have an RCON password configured.")).ToBadRequestResult();

            var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, gameServerApiResponse.Result.Data.RconPassword);

            var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconKickPlayerByName");
            operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
            operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

            try
            {
                await rconClient.KickPlayerByName(name);
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
                return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

            if (string.IsNullOrWhiteSpace(gameServerApiResponse.Result.Data.RconPassword))
                return new ApiResponse(new ApiError(ErrorCodes.RCON_PASSWORD_NOT_CONFIGURED, "The game server does not have an RCON password configured.")).ToBadRequestResult();

            var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, gameServerApiResponse.Result.Data.RconPassword);

            var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconKickAllPlayers");
            operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
            operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

            try
            {
                await rconClient.KickAllPlayers();
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
            var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

            if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
                return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

            if (string.IsNullOrWhiteSpace(gameServerApiResponse.Result.Data.RconPassword))
                return new ApiResponse(new ApiError(ErrorCodes.RCON_PASSWORD_NOT_CONFIGURED, "The game server does not have an RCON password configured.")).ToBadRequestResult();

            var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, gameServerApiResponse.Result.Data.RconPassword);

            var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconBanPlayerByName");
            operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
            operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

            try
            {
                await rconClient.BanPlayerByName(name);
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
                return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

            if (string.IsNullOrWhiteSpace(gameServerApiResponse.Result.Data.RconPassword))
                return new ApiResponse(new ApiError(ErrorCodes.RCON_PASSWORD_NOT_CONFIGURED, "The game server does not have an RCON password configured.")).ToBadRequestResult();

            var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, gameServerApiResponse.Result.Data.RconPassword);

            var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconTempBanPlayer");
            operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
            operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

            try
            {
                await rconClient.TempBanPlayer(clientId);
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
            var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

            if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
                return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

            if (string.IsNullOrWhiteSpace(gameServerApiResponse.Result.Data.RconPassword))
                return new ApiResponse(new ApiError(ErrorCodes.RCON_PASSWORD_NOT_CONFIGURED, "The game server does not have an RCON password configured.")).ToBadRequestResult();

            var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, gameServerApiResponse.Result.Data.RconPassword);

            var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconTempBanPlayerByName");
            operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
            operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

            try
            {
                await rconClient.TempBanPlayerByName(name);
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
            var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

            if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
                return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

            if (string.IsNullOrWhiteSpace(gameServerApiResponse.Result.Data.RconPassword))
                return new ApiResponse(new ApiError(ErrorCodes.RCON_PASSWORD_NOT_CONFIGURED, "The game server does not have an RCON password configured.")).ToBadRequestResult();

            var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, gameServerApiResponse.Result.Data.RconPassword);

            var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconUnbanPlayer");
            operation.Telemetry.Type = $"{gameServerApiResponse.Result.Data.GameType}Server";
            operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.Hostname}:{gameServerApiResponse.Result.Data.QueryPort}";

            try
            {
                await rconClient.UnbanPlayer(name);
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
                return new ApiResponse<string>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

            if (string.IsNullOrWhiteSpace(gameServerApiResponse.Result.Data.RconPassword))
                return new ApiResponse<string>(new ApiError(ErrorCodes.RCON_PASSWORD_NOT_CONFIGURED, "The game server does not have an RCON password configured.")).ToBadRequestResult();

            var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, gameServerApiResponse.Result.Data.RconPassword);

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
                return new ApiResponse<string>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

            if (string.IsNullOrWhiteSpace(gameServerApiResponse.Result.Data.RconPassword))
                return new ApiResponse<string>(new ApiError(ErrorCodes.RCON_PASSWORD_NOT_CONFIGURED, "The game server does not have an RCON password configured.")).ToBadRequestResult();

            var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, gameServerApiResponse.Result.Data.RconPassword);

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
                return new ApiResponse<string>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

            if (string.IsNullOrWhiteSpace(gameServerApiResponse.Result.Data.RconPassword))
                return new ApiResponse<string>(new ApiError(ErrorCodes.RCON_PASSWORD_NOT_CONFIGURED, "The game server does not have an RCON password configured.")).ToBadRequestResult();

            var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.Data.GameType, gameServerApiResponse.Result.Data.GameServerId, gameServerApiResponse.Result.Data.Hostname, gameServerApiResponse.Result.Data.QueryPort, gameServerApiResponse.Result.Data.RconPassword);

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
    }
}
