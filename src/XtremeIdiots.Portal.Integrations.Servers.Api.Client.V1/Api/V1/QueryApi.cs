using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MxIO.ApiClient;
using MxIO.ApiClient.Abstractions;
using MxIO.ApiClient.Extensions;

using RestSharp;

using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1
{
    public class QueryApi : BaseApi, IQueryApi
    {
        public QueryApi(ILogger<QueryApi> logger, IApiTokenProvider apiTokenProvider, IOptions<ServersApiClientOptions> options, IRestClientSingleton restClientSingleton) : base(logger, apiTokenProvider, restClientSingleton, options)
        {
        }

        public async Task<ApiResponseDto<ServerQueryStatusResponseDto>> GetServerStatus(Guid gameServerId)
        {
            var request = await CreateRequestAsync($"query/{gameServerId}/status", Method.Get);
            var response = await ExecuteAsync(request);

            return response.ToApiResponse<ServerQueryStatusResponseDto>();
        }
    }
}
