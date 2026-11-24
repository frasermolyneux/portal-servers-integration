using Microsoft.Extensions.Logging;
using System.Net;
using MX.Api.Abstractions;
using MX.Api.Client;
using MX.Api.Client.Auth;
using MX.Api.Client.Configuration;
using MX.Api.Client.Extensions;
using RestSharp;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.RulesEngine;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1;

public class RulesNativeActionApi : BaseApi<ServersApiClientOptions>, IRulesNativeActionApi
{
    private readonly IRestClientService _restClientService;

    public RulesNativeActionApi(
        ILogger<BaseApi<ServersApiClientOptions>> logger,
        IApiTokenProvider? apiTokenProvider,
        IRestClientService restClientService,
        ServersApiClientOptions options)
        : base(logger, apiTokenProvider, restClientService, options)
    {
        _restClientService = restClientService;
    }

    public async Task<ApiResult<RuleActionResult>> Execute(Guid gameServerId, RuleActionCommand request, CancellationToken cancellationToken = default)
    {
        var restRequest = await CreateRequestAsync($"v1/rcon/{gameServerId}/rules-action", Method.Post, cancellationToken).ConfigureAwait(false);
        restRequest.AddJsonBody(request);
        var response = await _restClientService.ExecuteAsync(Options.BaseUrl, restRequest, cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            throw new HttpRequestException("Rules action returned no response; dispatch outcome is unknown.");
        }

        if (response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.Created or HttpStatusCode.NoContent or HttpStatusCode.NotFound))
        {
            throw new HttpRequestException("Rules action request failed; dispatch outcome is unknown.", response.ErrorException, response.StatusCode);
        }
        return response.ToApiResult<RuleActionResult>();
    }
}
