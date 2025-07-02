using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MxIO.ApiClient;
using MxIO.ApiClient.Abstractions;
using MxIO.ApiClient.Extensions;
using RestSharp;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1
{
    public class RootApi : BaseApi, IRootApi
    {
        public RootApi(ILogger<BaseApi> logger, IApiTokenProvider apiTokenProvider, IRestClientSingleton restClientSingleton, IOptions<ApiClientOptions> options) : base(logger, apiTokenProvider, restClientSingleton, options)
        {
        }

        public async Task<ApiResponseDto> GetRoot()
        {
            var request = await CreateRequestAsync($"v1/", Method.Post);
            var response = await ExecuteAsync(request);

            return response.ToApiResponse();
        }
    }
}