
using Microsoft.Extensions.Logging;
using MX.Api.Abstractions;
using MX.Api.Client;
using MX.Api.Client.Auth;
using MX.Api.Client.Configuration;
using MX.Api.Client.Extensions;
using RestSharp;

using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Maps;


namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1
{
    public class MapsApi : BaseApi<ServersApiClientOptions>, IMapsApi
    {
        public MapsApi(
            ILogger<BaseApi<ServersApiClientOptions>> logger,
            IApiTokenProvider? apiTokenProvider,
            IRestClientService restClientService,
            ServersApiClientOptions options)
            : base(logger, apiTokenProvider, restClientService, options)
        {
        }

        public async Task<ApiResult<ServerMapsCollectionDto>> GetLoadedServerMapsFromHost(Guid gameServerId)
        {
            var request = await CreateRequestAsync($"v1/maps/{gameServerId}/host/loaded", Method.Get);
            var response = await ExecuteAsync(request);

            return response.ToApiResult<ServerMapsCollectionDto>();
        }

        public async Task<ApiResult> PushServerMapToHost(Guid gameServerId, string mapName)
        {
            var request = await CreateRequestAsync($"v1/maps/{gameServerId}/host/{mapName}", Method.Post);
            var response = await ExecuteAsync(request);

            return response.ToApiResult();
        }

        public async Task<ApiResult> DeleteServerMapFromHost(Guid gameServerId, string mapName)
        {
            var request = await CreateRequestAsync($"v1/maps/{gameServerId}/host/{mapName}", Method.Delete);
            var response = await ExecuteAsync(request);

            return response.ToApiResult();
        }
    }
}
