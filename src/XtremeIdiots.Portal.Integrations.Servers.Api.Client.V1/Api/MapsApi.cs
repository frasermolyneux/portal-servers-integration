
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MxIO.ApiClient;
using MxIO.ApiClient.Abstractions;
using MxIO.ApiClient.Extensions;

using RestSharp;

using XtremeIdiots.Portal.ServersApi.Abstractions.Interfaces;
using XtremeIdiots.Portal.ServersApi.Abstractions.Models.Maps;

namespace XtremeIdiots.Portal.ServersApiClient.Api
{
    public class MapsApi : BaseApi, IMapsApi
    {
        public MapsApi(ILogger<MapsApi> logger, IApiTokenProvider apiTokenProvider, IOptions<ServersApiClientOptions> options, IRestClientSingleton restClientSingleton) : base(logger, apiTokenProvider, restClientSingleton, options)
        {
        }

        public async Task<ApiResponseDto<ServerMapsCollectionDto>> GetLoadedServerMapsFromHost(Guid gameServerId)
        {
            var request = await CreateRequestAsync($"maps/{gameServerId}/host/loaded", Method.Get);
            var response = await ExecuteAsync(request);

            return response.ToApiResponse<ServerMapsCollectionDto>();
        }

        public async Task<ApiResponseDto> PushServerMapToHost(Guid gameServerId, string mapName)
        {
            var request = await CreateRequestAsync($"maps/{gameServerId}/host/{mapName}", Method.Post);
            var response = await ExecuteAsync(request);

            return response.ToApiResponse();
        }

        public async Task<ApiResponseDto> DeleteServerMapFromHost(Guid gameServerId, string mapName)
        {
            var request = await CreateRequestAsync($"maps/{gameServerId}/host/{mapName}", Method.Delete);
            var response = await ExecuteAsync(request);

            return response.ToApiResponse();
        }
    }
}
