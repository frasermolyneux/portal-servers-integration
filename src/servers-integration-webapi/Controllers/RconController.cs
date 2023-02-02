using System.Net;

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using MxIO.ApiClient.Abstractions;

using XtremeIdiots.Portal.RepositoryApiClient;
using XtremeIdiots.Portal.ServersApi.Abstractions.Interfaces;
using XtremeIdiots.Portal.ServersApi.Abstractions.Models;
using XtremeIdiots.Portal.ServersWebApi.Extensions;
using XtremeIdiots.Portal.ServersWebApi.Interfaces;

namespace XtremeIdiots.Portal.ServersWebApi.Controllers
{
    [ApiController]
    [Authorize(Roles = "ServiceAccount")]
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
            var gameServerApiResponse = await repositoryApiClient.GameServers.GetGameServer(gameServerId);

            if (!gameServerApiResponse.IsSuccess || gameServerApiResponse.Result == null)
                return new ApiResponseDto<ServerRconStatusResponseDto>(HttpStatusCode.InternalServerError);

            if (gameServerApiResponse.IsNotFound)
                return new ApiResponseDto<ServerRconStatusResponseDto>(HttpStatusCode.NotFound);

            if (string.IsNullOrWhiteSpace(gameServerApiResponse.Result.RconPassword))
                return new ApiResponseDto<ServerRconStatusResponseDto>(HttpStatusCode.BadRequest, "The game server does not have an rcon password configured");

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
    }
}
