using Microsoft.Extensions.Logging;
using MX.Api.Abstractions;
using MX.Api.Client;
using MX.Api.Client.Auth;
using MX.Api.Client.Configuration;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1;

public class Cod2RconApi : GameScopedRconApiBase, ICod2RconApi
{
    public Cod2RconApi(
        ILogger<BaseApi<ServersApiClientOptions>> logger,
        IApiTokenProvider? apiTokenProvider,
        IRestClientService restClientService,
        ServersApiClientOptions options)
        : base(logger, apiTokenProvider, restClientService, options, "cod2")
    {
    }

    public Task<ApiResult<RconCurrentMapDto>> GetCurrentMap(Guid gameServerId, CancellationToken cancellationToken = default) =>
        GetCurrentMapCore(gameServerId, cancellationToken);

    public Task<ApiResult> Say(Guid gameServerId, SayRequest request, CancellationToken cancellationToken = default) =>
        SayCore(gameServerId, request, cancellationToken);

    public Task<ApiResult<string>> Restart(Guid gameServerId, CancellationToken cancellationToken = default) =>
        RestartCore(gameServerId, cancellationToken);

    public Task<ApiResult<string>> RestartMap(Guid gameServerId, CancellationToken cancellationToken = default) =>
        RestartMapCore(gameServerId, cancellationToken);

    public Task<ApiResult<string>> FastRestartMap(Guid gameServerId, CancellationToken cancellationToken = default) =>
        FastRestartMapCore(gameServerId, cancellationToken);

    public Task<ApiResult<string>> NextMap(Guid gameServerId, CancellationToken cancellationToken = default) =>
        NextMapCore(gameServerId, cancellationToken);
}
