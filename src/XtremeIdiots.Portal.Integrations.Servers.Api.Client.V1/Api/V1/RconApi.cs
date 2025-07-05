using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MX.Api.Abstractions;
using MX.Api.Client;
using MX.Api.Client.Auth;
using MX.Api.Client.Configuration;
using MX.Api.Client.Extensions;
using RestSharp;

using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1
{
    public class RconApi : BaseApi, IRconApi
    {
        public RconApi(ILogger<RconApi> logger, IApiTokenProvider apiTokenProvider, IOptions<ApiClientOptions> options, IRestClientService restClientService) : base(logger, apiTokenProvider, restClientService, options)
        {
        }

        public async Task<ApiResult<ServerRconStatusResponseDto>> GetServerStatus(Guid gameServerId)
        {
            var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/status", Method.Get);
            var response = await ExecuteAsync(request);

            return response.ToApiResult<ServerRconStatusResponseDto>();
        }

        public async Task<ApiResult<RconMapCollectionDto>> GetServerMaps(Guid gameServerId)
        {
            var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/maps", Method.Get);
            var response = await ExecuteAsync(request);

            return response.ToApiResult<RconMapCollectionDto>();
        }

        public async Task<ApiResult> KickPlayer(Guid gameServerId, int clientId)
        {
            var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/kick/{clientId}", Method.Post);
            var response = await ExecuteAsync(request);

            return response.ToApiResult();
        }

        public async Task<ApiResult> BanPlayer(Guid gameServerId, int clientId)
        {
            var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/ban/{clientId}", Method.Post);
            var response = await ExecuteAsync(request);

            return response.ToApiResult();
        }
    }
}
