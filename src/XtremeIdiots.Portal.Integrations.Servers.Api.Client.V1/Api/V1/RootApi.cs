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
    public class RootApi : BaseApi, IRootApi
    {
        public RootApi(ILogger<BaseApi> logger, IApiTokenProvider apiTokenProvider, IRestClientService restClientService, IOptionsSnapshot<ApiClientOptions> optionsSnapshot) 
            : base(logger, apiTokenProvider, restClientService, optionsSnapshot, nameof(ServersApiClientOptions))
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