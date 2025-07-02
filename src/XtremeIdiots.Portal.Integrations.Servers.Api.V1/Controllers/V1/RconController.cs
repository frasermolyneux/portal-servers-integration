using System.Net;

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using MxIO.ApiClient.Abstractions;
using MxIO.ApiClient.WebExtensions;

using XtremeIdiots.Portal.RepositoryApiClient.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.RepositoryApi.Abstractions.Constants;
using Asp.Versioning;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1
{
    [ApiController]
    [Authorize(Roles = "ServiceAccount")]
    [ApiVersion(ApiVersions.V1)]
    [Route("api/v{version:apiVersion}")]
    public class RconController : Controller, IRconApi
    {
        private readonly IRepositoryApiClient repositoryApiClient;
        private readonly IRconClientFactory rconClientFactory;
        private readonly TelemetryClient telemetryClient;

        public RconController(
            IRepositoryApiClient repositoryApiClient,
            IRconClientFactory rconClientFactory,
            TelemetryClient telemetryClient)
        {
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

        async Task<ApiResponseDto<ServerRconStatusResponseDto>> IRconApi.GetServerStatus(Guid gameServerId)
        {
            var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

            if (!gameServerApiResponse.IsSuccess || gameServerApiResponse.Result == null)
                return new ApiResponseDto<ServerRconStatusResponseDto>(HttpStatusCode.InternalServerError);

            if (gameServerApiResponse.IsNotFound)
                return new ApiResponseDto<ServerRconStatusResponseDto>(HttpStatusCode.NotFound);

            if (string.IsNullOrWhiteSpace(gameServerApiResponse.Result.RconPassword))
                return new ApiResponseDto<ServerRconStatusResponseDto>(HttpStatusCode.BadRequest, null, new List<string> { "The game server does not have an rcon password configured" });

            var queryClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.GameType, gameServerApiResponse.Result.GameServerId, gameServerApiResponse.Result.Hostname, gameServerApiResponse.Result.QueryPort, gameServerApiResponse.Result.RconPassword);

            var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconServerStatus");
            operation.Telemetry.Type = $"{gameServerApiResponse.Result.GameType}Server";
            operation.Telemetry.Target = $"{gameServerApiResponse.Result.Hostname}:{gameServerApiResponse.Result.QueryPort}";

            try
            {
                var statusResult = queryClient.GetPlayers();

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

                    return new ApiResponseDto<ServerRconStatusResponseDto>(HttpStatusCode.OK, dto);
                }
                else
                {
                    return new ApiResponseDto<ServerRconStatusResponseDto>(HttpStatusCode.OK, new ServerRconStatusResponseDto());
                }
            }
            catch (Exception ex)
            {
                operation.Telemetry.Success = false;
                operation.Telemetry.ResultCode = ex.Message;
                telemetryClient.TrackException(ex);
                throw;
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

        async Task<ApiResponseDto<RconMapCollectionDto>> IRconApi.GetServerMaps(Guid gameServerId)
        {
            var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

            if (!gameServerApiResponse.IsSuccess || gameServerApiResponse.Result == null)
                return new ApiResponseDto<RconMapCollectionDto>(HttpStatusCode.InternalServerError);

            if (gameServerApiResponse.IsNotFound)
                return new ApiResponseDto<RconMapCollectionDto>(HttpStatusCode.NotFound);

            if (string.IsNullOrWhiteSpace(gameServerApiResponse.Result.RconPassword))
                return new ApiResponseDto<RconMapCollectionDto>(HttpStatusCode.BadRequest, null, new List<string> { "The game server does not have an rcon password configured" });

            var queryClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.GameType, gameServerApiResponse.Result.GameServerId, gameServerApiResponse.Result.Hostname, gameServerApiResponse.Result.QueryPort, gameServerApiResponse.Result.RconPassword);

            var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconMapRotation");
            operation.Telemetry.Type = $"{gameServerApiResponse.Result.GameType}Server";
            operation.Telemetry.Target = $"{gameServerApiResponse.Result.Hostname}:{gameServerApiResponse.Result.QueryPort}";

            try
            {
                var statusResult = await queryClient.GetMaps();

                if (statusResult != null)
                {
                    var result = new RconMapCollectionDto
                    {
                        TotalRecords = statusResult.Count,
                        FilteredRecords = statusResult.Count,
                        Entries = statusResult.Select(m => new RconMapDto(m.GameType, m.MapName)).ToList()
                    };

                    return new ApiResponseDto<RconMapCollectionDto>(HttpStatusCode.OK, result);
                }
                else
                {
                    return new ApiResponseDto<RconMapCollectionDto>(HttpStatusCode.OK, new RconMapCollectionDto());
                }
            }
            catch (Exception ex)
            {
                operation.Telemetry.Success = false;
                operation.Telemetry.ResultCode = ex.Message;
                telemetryClient.TrackException(ex);
                throw;
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

        async Task<ApiResponseDto> IRconApi.KickPlayer(Guid gameServerId, int clientId)
        {
            var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

            if (!gameServerApiResponse.IsSuccess || gameServerApiResponse.Result == null)
                return new ApiResponseDto(HttpStatusCode.InternalServerError);

            if (gameServerApiResponse.IsNotFound)
                return new ApiResponseDto(HttpStatusCode.NotFound);

            if (string.IsNullOrWhiteSpace(gameServerApiResponse.Result.RconPassword))
                return new ApiResponseDto(HttpStatusCode.BadRequest, new List<string> { "The game server does not have an rcon password configured" });

            var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.GameType, gameServerApiResponse.Result.GameServerId, gameServerApiResponse.Result.Hostname, gameServerApiResponse.Result.QueryPort, gameServerApiResponse.Result.RconPassword);

            var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconKickPlayer");
            operation.Telemetry.Type = $"{gameServerApiResponse.Result.GameType}Server";
            operation.Telemetry.Target = $"{gameServerApiResponse.Result.Hostname}:{gameServerApiResponse.Result.QueryPort}";

            try
            {
                var result = await rconClient.KickPlayer(clientId);

                telemetryClient.TrackEvent("RconKickPlayer", new Dictionary<string, string>
                {
                    { "GameServerId", gameServerApiResponse.Result.GameServerId.ToString() },
                    { "ClientId", clientId.ToString() },
                    { "Result", result.ToString() }
                });

                return new ApiResponseDto(HttpStatusCode.OK);
            }
            catch (NotImplementedException ex)
            {
                operation.Telemetry.Success = false;
                operation.Telemetry.ResultCode = ex.Message;
                telemetryClient.TrackException(ex);
                return new ApiResponseDto(HttpStatusCode.NotImplemented, new List<string> { "The kick player operation is not implemented for this game server type" });
            }
            catch (Exception ex)
            {
                operation.Telemetry.Success = false;
                operation.Telemetry.ResultCode = ex.Message;
                telemetryClient.TrackException(ex);
                throw;
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

        async Task<ApiResponseDto> IRconApi.BanPlayer(Guid gameServerId, int clientId)
        {
            var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

            if (!gameServerApiResponse.IsSuccess || gameServerApiResponse.Result == null)
                return new ApiResponseDto(HttpStatusCode.InternalServerError);

            if (gameServerApiResponse.IsNotFound)
                return new ApiResponseDto(HttpStatusCode.NotFound);

            if (string.IsNullOrWhiteSpace(gameServerApiResponse.Result.RconPassword))
                return new ApiResponseDto(HttpStatusCode.BadRequest, new List<string> { "The game server does not have an rcon password configured" });

            var rconClient = rconClientFactory.CreateInstance(gameServerApiResponse.Result.GameType, gameServerApiResponse.Result.GameServerId, gameServerApiResponse.Result.Hostname, gameServerApiResponse.Result.QueryPort, gameServerApiResponse.Result.RconPassword);

            var operation = telemetryClient.StartOperation<DependencyTelemetry>("RconBanPlayer");
            operation.Telemetry.Type = $"{gameServerApiResponse.Result.GameType}Server";
            operation.Telemetry.Target = $"{gameServerApiResponse.Result.Hostname}:{gameServerApiResponse.Result.QueryPort}";

            try
            {
                var result = await rconClient.BanPlayer(clientId);

                telemetryClient.TrackEvent("RconBanPlayer", new Dictionary<string, string>
                {
                    { "GameServerId", gameServerApiResponse.Result.GameServerId.ToString() },
                    { "ClientId", clientId.ToString() },
                    { "Result", result.ToString() }
                });

                return new ApiResponseDto(HttpStatusCode.OK);
            }
            catch (NotImplementedException ex)
            {
                operation.Telemetry.Success = false;
                operation.Telemetry.ResultCode = ex.Message;
                telemetryClient.TrackException(ex);
                return new ApiResponseDto(HttpStatusCode.NotImplemented, new List<string> { "The ban player operation is not implemented for this game server type" });
            }
            catch (Exception ex)
            {
                operation.Telemetry.Success = false;
                operation.Telemetry.ResultCode = ex.Message;
                telemetryClient.TrackException(ex);
                throw;
            }
            finally
            {
                telemetryClient.StopOperation(operation);
            }
        }
    }
}
