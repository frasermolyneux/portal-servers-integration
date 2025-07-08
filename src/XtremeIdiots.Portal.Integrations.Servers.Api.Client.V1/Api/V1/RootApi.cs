using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MX.Api.Abstractions;
using MX.Api.Client;
using MX.Api.Client.Auth;
using MX.Api.Client.Configuration;
using MX.Api.Client.Extensions;
using RestSharp;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1
{
    public class RootApi : BaseApi<ServersApiClientOptions>, IRootApi
    {
        public RootApi(
            ILogger<BaseApi<ServersApiClientOptions>> logger, 
            IApiTokenProvider? apiTokenProvider, 
            IRestClientService restClientService, 
            IOptions<ServersApiClientOptions> options) 
            : base(logger, apiTokenProvider, restClientService, options)
        {
        }

        public async Task<ApiResult> GetRoot()
        {
            var request = await CreateRequestAsync($"v1/", Method.Post);
            var response = await ExecuteAsync(request);

            return response.ToApiResult();
        }
    }
}