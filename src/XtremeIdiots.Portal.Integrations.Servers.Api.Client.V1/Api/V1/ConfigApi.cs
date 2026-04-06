using Microsoft.Extensions.Logging;
using MX.Api.Abstractions;
using MX.Api.Client;
using MX.Api.Client.Auth;
using MX.Api.Client.Configuration;
using MX.Api.Client.Extensions;
using RestSharp;

using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Config;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1
{
    public class ConfigApi : BaseApi<ServersApiClientOptions>, IConfigApi
    {
        public ConfigApi(
            ILogger<BaseApi<ServersApiClientOptions>> logger,
            IApiTokenProvider? apiTokenProvider,
            IRestClientService restClientService,
            ServersApiClientOptions options)
            : base(logger, apiTokenProvider, restClientService, options)
        {
        }

        public async Task<ApiResult<ConfigFileContentDto>> GetConfigFile(Guid gameServerId, string filePath, CancellationToken cancellationToken = default)
        {
            var request = await CreateRequestAsync($"v1/config/{gameServerId}/file", Method.Get, cancellationToken);
            request.AddQueryParameter("filePath", filePath);
            var response = await ExecuteAsync(request, cancellationToken);

            return response.ToApiResult<ConfigFileContentDto>();
        }

        public async Task<ApiResult> UpdateConfigVariable(Guid gameServerId, string filePath, string variableName, string value, CancellationToken cancellationToken = default)
        {
            var request = await CreateRequestAsync($"v1/config/{gameServerId}/file/variable", Method.Put, cancellationToken);
            request.AddQueryParameter("filePath", filePath);
            request.AddQueryParameter("variableName", variableName);
            request.AddJsonBody(new UpdateConfigVariableRequest { Value = value });
            var response = await ExecuteAsync(request, cancellationToken);

            return response.ToApiResult();
        }
    }
}
