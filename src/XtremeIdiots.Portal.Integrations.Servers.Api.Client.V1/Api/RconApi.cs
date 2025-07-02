using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MxIO.ApiClient;
using MxIO.ApiClient.Abstractions;
using MxIO.ApiClient.Extensions;

using RestSharp;

using XtremeIdiots.Portal.ServersApi.Abstractions.Interfaces;
using XtremeIdiots.Portal.ServersApi.Abstractions.Models;
using XtremeIdiots.Portal.ServersApi.Abstractions.Models.Rcon;

namespace XtremeIdiots.Portal.ServersApiClient.Api
{
    public class RconApi : BaseApi, IRconApi
    {
        public RconApi(ILogger<RconApi> logger, IApiTokenProvider apiTokenProvider, IOptions<ServersApiClientOptions> options, IRestClientSingleton restClientSingleton) : base(logger, apiTokenProvider, restClientSingleton, options)
        {
        }

        public async Task<ApiResponseDto<ServerRconStatusResponseDto>> GetServerStatus(Guid gameServerId)
        {
            var request = await CreateRequestAsync($"rcon/{gameServerId}/status", Method.Get);
            var response = await ExecuteAsync(request);

            return response.ToApiResponse<ServerRconStatusResponseDto>();
        }

        public async Task<ApiResponseDto<RconMapCollectionDto>> GetServerMaps(Guid gameServerId)
        {
            var request = await CreateRequestAsync($"rcon/{gameServerId}/maps", Method.Get);
            var response = await ExecuteAsync(request);

            return response.ToApiResponse<RconMapCollectionDto>();
        }

        public async Task<ApiResponseDto> KickPlayer(Guid gameServerId, int clientId)
        {
            var request = await CreateRequestAsync($"rcon/{gameServerId}/kick/{clientId}", Method.Post);
            var response = await ExecuteAsync(request);

            return response.ToApiResponse();
        }

        public async Task<ApiResponseDto> BanPlayer(Guid gameServerId, int clientId)
        {
            var request = await CreateRequestAsync($"rcon/{gameServerId}/ban/{clientId}", Method.Post);
            var response = await ExecuteAsync(request);

            return response.ToApiResponse();
        }
    }
}
