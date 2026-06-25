using Microsoft.Extensions.Logging;
using MX.Api.Abstractions;
using MX.Api.Client;
using MX.Api.Client.Auth;
using MX.Api.Client.Configuration;
using MX.Api.Client.Extensions;
using RestSharp;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Ftp;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1
{
    public class FileBrowseApi : BaseApi<ServersApiClientOptions>, IFileBrowseApi
    {
        public FileBrowseApi(
            ILogger<BaseApi<ServersApiClientOptions>> logger,
            IApiTokenProvider? apiTokenProvider,
            IRestClientService restClientService,
            ServersApiClientOptions options)
            : base(logger, apiTokenProvider, restClientService, options)
        {
        }

        public async Task<ApiResult<FtpDirectoryListingDto>> BrowseDirectory(Guid gameServerId, string? path = null, CancellationToken cancellationToken = default)
        {
            var request = await CreateRequestAsync($"v1/file-browse/{gameServerId}/browse", Method.Get, cancellationToken);

            if (!string.IsNullOrEmpty(path))
            {
                request.AddQueryParameter("path", path);
            }

            var response = await ExecuteAsync(request, cancellationToken);

            return response.ToApiResult<FtpDirectoryListingDto>();
        }
    }
}