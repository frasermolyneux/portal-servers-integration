﻿using System.Net;
using Asp.Versioning;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MX.Api.Abstractions;
using MX.Api.Web.Extensions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Constants;
using XtremeIdiots.Portal.RepositoryApi.Abstractions.Constants;
using XtremeIdiots.Portal.RepositoryApiClient.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1
{
    [ApiController]
    [Authorize(Roles = "ServiceAccount")]
    [ApiVersion(ApiVersions.V1)]
    [Route("api/v{version:apiVersion}")]
    public class QueryController : Controller, IQueryApi
    {
        private readonly ILogger<QueryController> logger;
        private readonly IRepositoryApiClient repositoryApiClient;
        private readonly IQueryClientFactory queryClientFactory;
        private readonly TelemetryClient telemetryClient;
        private readonly IMemoryCache memoryCache;

        public QueryController(
            ILogger<QueryController> logger,
            IRepositoryApiClient repositoryApiClient,
            IQueryClientFactory queryClientFactory,
            TelemetryClient telemetryClient,
            IMemoryCache memoryCache)
        {
            this.logger = logger;
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

        async Task<ApiResult<ServerQueryStatusResponseDto>> IQueryApi.GetServerStatus(Guid gameServerId)
        {
            var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

            if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result == null)
                return new ApiResponse<ServerQueryStatusResponseDto>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

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

                    return new ApiResponse<ServerQueryStatusResponseDto>(dto).ToApiResult();
                }
                else
                {
                    return new ApiResponse<ServerQueryStatusResponseDto>(new ServerQueryStatusResponseDto()).ToApiResult();
                }
            }
            catch (Exception ex)
            {
                operation.Telemetry.Success = false;
                operation.Telemetry.ResultCode = ex.Message;
                telemetryClient.TrackException(ex);

                logger.LogError(ex, "Failed to query server status for game server {GameServerId}", gameServerId);
                return new ApiResponse<ServerQueryStatusResponseDto>(new ApiError(ErrorCodes.QUERY_CONNECTION_FAILED, "Failed to query the game server status.")).ToApiResult();
            }
            finally
            {
                telemetryClient.StopOperation(operation);
            }
        }
    }
}
