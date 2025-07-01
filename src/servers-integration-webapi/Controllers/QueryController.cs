using System.Net;

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

using MxIO.ApiClient.Abstractions;
using MxIO.ApiClient.WebExtensions;

using XtremeIdiots.Portal.RepositoryApiClient.V1;
using XtremeIdiots.Portal.ServersApi.Abstractions.Interfaces;
using XtremeIdiots.Portal.ServersApi.Abstractions.Models;
using XtremeIdiots.Portal.ServersWebApi.Interfaces;

namespace XtremeIdiots.Portal.ServersWebApi.Controllers
{
    [ApiController]
    [Authorize(Roles = "ServiceAccount")]
    public class QueryController : Controller, IQueryApi
    {
        private readonly IRepositoryApiClient repositoryApiClient;
        private readonly IQueryClientFactory queryClientFactory;
        private readonly TelemetryClient telemetryClient;
        private readonly IMemoryCache memoryCache;

        public QueryController(
            IRepositoryApiClient repositoryApiClient,
            IQueryClientFactory queryClientFactory,
            TelemetryClient telemetryClient,
            IMemoryCache memoryCache)
        {
            this.repositoryApiClient = repositoryApiClient;
            this.queryClientFactory = queryClientFactory;
            this.telemetryClient = telemetryClient;
            this.memoryCache = memoryCache;
        }

        [HttpGet]
        [Route("query/{gameServerId}/status")]
        public async Task<IActionResult> GetServerStatus(Guid gameServerId)
        {
            var response = await ((IQueryApi)this).GetServerStatus(gameServerId);

            return response.ToHttpResult();
        }

        async Task<ApiResponseDto<ServerQueryStatusResponseDto>> IQueryApi.GetServerStatus(Guid gameServerId)
        {
            var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

            if (!gameServerApiResponse.IsSuccess || gameServerApiResponse.Result == null)
                return new ApiResponseDto<ServerQueryStatusResponseDto>(HttpStatusCode.InternalServerError);

            if (gameServerApiResponse.IsNotFound)
                return new ApiResponseDto<ServerQueryStatusResponseDto>(HttpStatusCode.NotFound);

            var queryClient = queryClientFactory.CreateInstance(gameServerApiResponse.Result.GameType, gameServerApiResponse.Result.Hostname, gameServerApiResponse.Result.QueryPort);

            var operation = telemetryClient.StartOperation<DependencyTelemetry>("QueryServerStatus");
            operation.Telemetry.Type = $"{gameServerApiResponse.Result.GameType}Server";
            operation.Telemetry.Target = $"{gameServerApiResponse.Result.Hostname}:{gameServerApiResponse.Result.QueryPort}";

            try
            {
                if (!memoryCache.TryGetValue($"{gameServerApiResponse.Result.GameServerId}-query-status", out IQueryResponse? statusResult))
                {
                    statusResult = await queryClient.GetServerStatus();

                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetAbsoluteExpiration(TimeSpan.FromSeconds(300));

                    memoryCache.Set($"{gameServerApiResponse.Result.GameServerId}-query-status", statusResult, cacheEntryOptions);
                }

                if (statusResult != null)
                {
                    var dto = new ServerQueryStatusResponseDto
                    {
                        ServerName = statusResult.ServerName,
                        Map = statusResult.Map,
                        Mod = statusResult.Mod,
                        MaxPlayers = statusResult.MaxPlayers,
                        PlayerCount = statusResult.PlayerCount,
                        ServerParams = statusResult.ServerParams,
                        Players = statusResult.Players.Select(p => new ServerQueryPlayerDto
                        {
                            Name = p.Name,
                            Score = p.Score
                        }).ToList()
                    };

                    return new ApiResponseDto<ServerQueryStatusResponseDto>(HttpStatusCode.OK, dto);
                }
                else
                {
                    return new ApiResponseDto<ServerQueryStatusResponseDto>(HttpStatusCode.OK, new ServerQueryStatusResponseDto());
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
