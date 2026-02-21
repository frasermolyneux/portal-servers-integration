using Microsoft.Extensions.Logging;

using MX.Api.Abstractions;
using MX.Api.Client;
using MX.Api.Client.Auth;
using MX.Api.Client.Extensions;

using RestSharp;

using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1;

public class ApiInfoApi : BaseApi<ServersApiClientOptions>, IApiInfoApi
{
    public ApiInfoApi(
        ILogger<BaseApi<ServersApiClientOptions>> logger,
        IApiTokenProvider? apiTokenProvider,
        IRestClientService restClientService,
        ServersApiClientOptions options)
        : base(logger, apiTokenProvider, restClientService, options)
    {
    }

    public async Task<ApiResult<ApiInfoDto>> GetApiInfo(CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync("v1/info", Method.Get, cancellationToken);
        var response = await ExecuteAsync(request, cancellationToken);

        return response.ToApiResult<ApiInfoDto>();
    }
}
