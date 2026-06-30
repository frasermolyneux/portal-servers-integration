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

    protected Task<ApiResult<RconStatusResponseDto>> StatusCore(Guid gameServerId, CancellationToken cancellationToken = default) =>
        GetResponse<RconStatusResponseDto>(gameServerId, "status", cancellationToken);

    protected Task<ApiResult<RconMapCollectionDto>> GetMapsCore(Guid gameServerId, CancellationToken cancellationToken = default) =>
        GetResponse<RconMapCollectionDto>(gameServerId, "maps", cancellationToken);

    protected Task<ApiResult<string>> ServerInfoCore(Guid gameServerId, CancellationToken cancellationToken = default) =>
        GetResponse<string>(gameServerId, "server-info", cancellationToken);

    protected Task<ApiResult<string>> SystemInfoCore(Guid gameServerId, CancellationToken cancellationToken = default) =>
        GetResponse<string>(gameServerId, "system-info", cancellationToken);

    protected Task<ApiResult<string>> CmdListCore(Guid gameServerId, CancellationToken cancellationToken = default) =>
        GetResponse<string>(gameServerId, "cmdlist", cancellationToken);

    protected Task<ApiResult<string>> CvarListCore(Guid gameServerId, CancellationToken cancellationToken = default) =>
        GetResponse<string>(gameServerId, "cvarlist", cancellationToken);

    protected Task<ApiResult<string>> DvarListCore(Guid gameServerId, CancellationToken cancellationToken = default) =>
        GetResponse<string>(gameServerId, "dvarlist", cancellationToken);

    protected Task<ApiResult> SayCore(Guid gameServerId, SayRequest request, CancellationToken cancellationToken = default) =>
        PostNoResponse(gameServerId, "say", request, cancellationToken);

    protected Task<ApiResult<string>> MapCore(Guid gameServerId, ChangeMapRequest request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "map", request, cancellationToken);

    protected Task<ApiResult<string>> KickCore(Guid gameServerId, ClientSlotRequest request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "kick", request, cancellationToken);

    protected Task<ApiResult<string>> TempBanCore(Guid gameServerId, ClientSlotRequest request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "temp-ban", request, cancellationToken);

    protected Task<ApiResult<string>> BanCore(Guid gameServerId, ClientSlotRequest request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "ban", request, cancellationToken);

    protected Task<ApiResult<string>> SetCore(Guid gameServerId, SetDvarRequest request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "set", request, cancellationToken);

    protected Task<ApiResult<string>> SetaCore(Guid gameServerId, SetDvarRequest request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "seta", request, cancellationToken);

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
