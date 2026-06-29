using Microsoft.Extensions.Logging;
using MX.Api.Abstractions;
using MX.Api.Client;
using MX.Api.Client.Auth;
using MX.Api.Client.Configuration;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1;

public class InsurgencyRconApi : GameScopedRconApiBase, IInsurgencyRconApi
{
    public InsurgencyRconApi(
        ILogger<BaseApi<ServersApiClientOptions>> logger,
        IApiTokenProvider? apiTokenProvider,
        IRestClientService restClientService,
        ServersApiClientOptions options)
        : base(logger, apiTokenProvider, restClientService, options, "insurgency")
    {
    }

    public Task<ApiResult<RconCurrentMapDto>> GetCurrentMap(Guid gameServerId, CancellationToken cancellationToken = default) =>
        GetCurrentMapCore(gameServerId, cancellationToken);

    public Task<ApiResult> Say(Guid gameServerId, SayRequest request, CancellationToken cancellationToken = default) =>
        SayCore(gameServerId, request, cancellationToken);
}
