using Microsoft.Extensions.Logging;
using MX.Api.Abstractions;
using MX.Api.Client;
using MX.Api.Client.Auth;
using MX.Api.Client.Configuration;
using MX.Api.Client.Extensions;
using RestSharp;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1;

public abstract class GameScopedRconApiBase : BaseApi<ServersApiClientOptions>
{
    private readonly string _routePrefix;

    protected GameScopedRconApiBase(
        ILogger<BaseApi<ServersApiClientOptions>> logger,
        IApiTokenProvider? apiTokenProvider,
        IRestClientService restClientService,
        ServersApiClientOptions options,
        string routePrefix)
        : base(logger, apiTokenProvider, restClientService, options)
    {
        _routePrefix = routePrefix;
    }

    protected Task<ApiResult<RconCurrentMapDto>> GetCurrentMapCore(Guid gameServerId, CancellationToken cancellationToken = default) =>
        GetResponse<RconCurrentMapDto>(gameServerId, "current-map", cancellationToken);

    protected Task<ApiResult> SayCore(Guid gameServerId, SayRequest request, CancellationToken cancellationToken = default) =>
        PostNoResponse(gameServerId, "say", request, cancellationToken);

    protected Task<ApiResult<string>> RestartCore(Guid gameServerId, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "restart", cancellationToken: cancellationToken);

    protected Task<ApiResult<string>> RestartMapCore(Guid gameServerId, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "restart-map", cancellationToken: cancellationToken);

    protected Task<ApiResult<string>> FastRestartMapCore(Guid gameServerId, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "fast-restart-map", cancellationToken: cancellationToken);

    protected Task<ApiResult<string>> NextMapCore(Guid gameServerId, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "next-map", cancellationToken: cancellationToken);

    private async Task<ApiResult<TResponse>> GetResponse<TResponse>(Guid gameServerId, string route, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/{_routePrefix}/{route}", Method.Get, cancellationToken).ConfigureAwait(false);
        var response = await ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        return response.ToApiResult<TResponse>();
    }

    private async Task<ApiResult> PostNoResponse(Guid gameServerId, string route, object? body = null, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/{_routePrefix}/{route}", Method.Post, cancellationToken).ConfigureAwait(false);

        if (body != null)
        {
            request.AddJsonBody(body);
        }

        var response = await ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        return response.ToApiResult();
    }

    private async Task<ApiResult<string>> PostString(Guid gameServerId, string route, object? body = null, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/{_routePrefix}/{route}", Method.Post, cancellationToken).ConfigureAwait(false);

        if (body != null)
        {
            request.AddJsonBody(body);
        }

        var response = await ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        return response.ToApiResult<string>();
    }
}
