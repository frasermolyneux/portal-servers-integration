using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MxIO.ApiClient;
using MxIO.ApiClient.Abstractions;
using MxIO.ApiClient.Extensions;

using RestSharp;

using XtremeIdiots.Portal.ServersApi.Abstractions.Interfaces;
using XtremeIdiots.Portal.ServersApi.Abstractions.Models;

namespace XtremeIdiots.Portal.ServersApiClient.Api
{
    public class QueryApi : BaseApi, IQueryApi
    {
        public QueryApi(ILogger<QueryApi> logger, IApiTokenProvider apiTokenProvider, IOptions<ServersApiClientOptions> options) : base(logger, apiTokenProvider, options)
        {
        }

        public async Task<ApiResponseDto<ServerQueryStatusResponseDto>> GetServerStatus(Guid gameServerId)
        {
            var request = await CreateRequest($"query/{gameServerId}/status", Method.Get);
            var response = await ExecuteAsync(request);

            return response.ToApiResponse<ServerQueryStatusResponseDto>();
        }
    }
}
