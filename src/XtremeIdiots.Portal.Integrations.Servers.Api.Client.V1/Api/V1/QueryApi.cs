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

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1
{
    public class QueryApi : BaseApi, IQueryApi
    {
        public QueryApi(ILogger<QueryApi> logger, IApiTokenProvider apiTokenProvider, IOptions<ApiClientOptions> options, IRestClientService restClientService) : base(logger, apiTokenProvider, restClientService, options)
        {
        }

        public async Task<ApiResult<ServerQueryStatusResponseDto>> GetServerStatus(Guid gameServerId)
        {
            var request = await CreateRequestAsync($"v1/query/{gameServerId}/status", Method.Get);
            var response = await ExecuteAsync(request);

            return response.ToApiResult<ServerQueryStatusResponseDto>();
        }
    }
}
