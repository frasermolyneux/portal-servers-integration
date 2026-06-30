using Microsoft.Extensions.Logging;
using MX.Api.Abstractions;
using MX.Api.Client;
using MX.Api.Client.Auth;
using MX.Api.Client.Configuration;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1;

public class Cod4RconApi : GameScopedRconApiBase, ICod4RconApi
{
    public Cod4RconApi(
        ILogger<BaseApi<ServersApiClientOptions>> logger,
        IApiTokenProvider? apiTokenProvider,
        IRestClientService restClientService,
        ServersApiClientOptions options)
        : base(logger, apiTokenProvider, restClientService, options, "cod4")
    {
    }

    public Task<ApiResult<RconCurrentMapDto>> GetCurrentMap(Guid gameServerId, CancellationToken cancellationToken = default) =>
        GetCurrentMapCore(gameServerId, cancellationToken);

    public Task<ApiResult<RconStatusResponseDto>> Status(Guid gameServerId, CancellationToken cancellationToken = default) =>
        StatusCore(gameServerId, cancellationToken);

    public Task<ApiResult<RconMapCollectionDto>> GetMaps(Guid gameServerId, CancellationToken cancellationToken = default) =>
        GetMapsCore(gameServerId, cancellationToken);

    public Task<ApiResult<string>> ServerInfo(Guid gameServerId, CancellationToken cancellationToken = default) =>
        ServerInfoCore(gameServerId, cancellationToken);

    public Task<ApiResult<string>> SystemInfo(Guid gameServerId, CancellationToken cancellationToken = default) =>
        SystemInfoCore(gameServerId, cancellationToken);

    public Task<ApiResult<string>> CmdList(Guid gameServerId, CancellationToken cancellationToken = default) =>
        CmdListCore(gameServerId, cancellationToken);

    public Task<ApiResult<string>> CvarList(Guid gameServerId, CancellationToken cancellationToken = default) =>
        CvarListCore(gameServerId, cancellationToken);

    public Task<ApiResult<string>> DvarList(Guid gameServerId, CancellationToken cancellationToken = default) =>
        DvarListCore(gameServerId, cancellationToken);

    public Task<ApiResult> Say(Guid gameServerId, SayRequest request, CancellationToken cancellationToken = default) =>
        SayCore(gameServerId, request, cancellationToken);

    public Task<ApiResult<string>> Tell(Guid gameServerId, CoD4xTargetMessageRequestDto request, CancellationToken cancellationToken = default) =>
        TellCore(gameServerId, request, cancellationToken);

    public Task<ApiResult<string>> Map(Guid gameServerId, ChangeMapRequest request, CancellationToken cancellationToken = default) =>
        MapCore(gameServerId, request, cancellationToken);

    public Task<ApiResult<string>> Kick(Guid gameServerId, ClientSlotRequest request, CancellationToken cancellationToken = default) =>
        KickCore(gameServerId, request, cancellationToken);

    public Task<ApiResult<string>> TempBan(Guid gameServerId, ClientSlotRequest request, CancellationToken cancellationToken = default) =>
        TempBanCore(gameServerId, request, cancellationToken);

    public Task<ApiResult<string>> Ban(Guid gameServerId, ClientSlotRequest request, CancellationToken cancellationToken = default) =>
        BanCore(gameServerId, request, cancellationToken);

    public Task<ApiResult<string>> Set(Guid gameServerId, SetDvarRequest request, CancellationToken cancellationToken = default) =>
        SetCore(gameServerId, request, cancellationToken);

    public Task<ApiResult<string>> Seta(Guid gameServerId, SetDvarRequest request, CancellationToken cancellationToken = default) =>
        SetaCore(gameServerId, request, cancellationToken);

    public Task<ApiResult<string>> Restart(Guid gameServerId, CancellationToken cancellationToken = default) =>
        RestartCore(gameServerId, cancellationToken);

    public Task<ApiResult<string>> RestartMap(Guid gameServerId, CancellationToken cancellationToken = default) =>
        RestartMapCore(gameServerId, cancellationToken);

    public Task<ApiResult<string>> FastRestartMap(Guid gameServerId, CancellationToken cancellationToken = default) =>
        FastRestartMapCore(gameServerId, cancellationToken);

    public Task<ApiResult<string>> NextMap(Guid gameServerId, CancellationToken cancellationToken = default) =>
        NextMapCore(gameServerId, cancellationToken);
}
