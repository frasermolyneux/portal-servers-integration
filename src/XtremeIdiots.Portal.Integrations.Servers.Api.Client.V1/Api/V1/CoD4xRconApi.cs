using Microsoft.Extensions.Logging;
using MX.Api.Abstractions;
using MX.Api.Client;
using MX.Api.Client.Auth;
using MX.Api.Client.Configuration;
using MX.Api.Client.Extensions;
using RestSharp;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1;

public class CoD4xRconApi : BaseApi<ServersApiClientOptions>, ICoD4xRconApi
{
    public CoD4xRconApi(
        ILogger<BaseApi<ServersApiClientOptions>> logger,
        IApiTokenProvider? apiTokenProvider,
        IRestClientService restClientService,
        ServersApiClientOptions options)
        : base(logger, apiTokenProvider, restClientService, options)
    {
    }

    private async Task<ApiResult<string>> GetString(Guid gameServerId, string route, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/cod4x/{route}", Method.Get, cancellationToken).ConfigureAwait(false);
        var response = await ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        return response.ToApiResult<string>();
    }

    private async Task<ApiResult<string>> PostString(Guid gameServerId, string route, object? body = null, CancellationToken cancellationToken = default)
    {
        var request = await CreateRequestAsync($"v1/rcon/{gameServerId}/cod4x/{route}", Method.Post, cancellationToken).ConfigureAwait(false);
        if (body != null)
        {
            request.AddJsonBody(body);
        }

        var response = await ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        return response.ToApiResult<string>();
    }

    public Task<ApiResult<string>> PermBan(Guid gameServerId, CoD4xPermBanRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "permban", request, cancellationToken);

    public Task<ApiResult<string>> BanUser(Guid gameServerId, CoD4xTargetReasonRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "ban-user", request, cancellationToken);

    public Task<ApiResult<string>> BanClient(Guid gameServerId, CoD4xClientReasonRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "ban-client", request, cancellationToken);

    public Task<ApiResult<string>> TempBan(Guid gameServerId, CoD4xTempBanRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "tempban", request, cancellationToken);

    public Task<ApiResult<string>> Unban(Guid gameServerId, CoD4xUnbanRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "unban", request, cancellationToken);

    public Task<ApiResult<string>> UnbanUser(Guid gameServerId, CoD4xTargetRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "unban-user", request, cancellationToken);

    public Task<ApiResult<string>> Kick(Guid gameServerId, CoD4xTargetReasonRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "kick", request, cancellationToken);

    public Task<ApiResult<string>> ClientKick(Guid gameServerId, CoD4xClientReasonRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "client-kick", request, cancellationToken);

    public Task<ApiResult<string>> OnlyKick(Guid gameServerId, CoD4xClientReasonRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "only-kick", request, cancellationToken);

    public Task<ApiResult<string>> Status(Guid gameServerId, CancellationToken cancellationToken = default) =>
        GetString(gameServerId, "status", cancellationToken);

    public Task<ApiResult<string>> MiniStatus(Guid gameServerId, CancellationToken cancellationToken = default) =>
        GetString(gameServerId, "ministatus", cancellationToken);

    public Task<ApiResult<string>> DumpUser(Guid gameServerId, CoD4xTargetRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "dump-user", request, cancellationToken);

    public Task<ApiResult<string>> ServerInfo(Guid gameServerId, CancellationToken cancellationToken = default) =>
        GetString(gameServerId, "server-info", cancellationToken);

    public Task<ApiResult<string>> SystemInfo(Guid gameServerId, CancellationToken cancellationToken = default) =>
        GetString(gameServerId, "system-info", cancellationToken);

    public Task<ApiResult<string>> ScreenSay(Guid gameServerId, CoD4xMessageRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "screen-say", request, cancellationToken);

    public Task<ApiResult<string>> ConSay(Guid gameServerId, CoD4xMessageRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "con-say", request, cancellationToken);

    public Task<ApiResult<string>> Tell(Guid gameServerId, CoD4xTargetMessageRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "tell", request, cancellationToken);

    public Task<ApiResult<string>> ScreenTell(Guid gameServerId, CoD4xTargetMessageRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "screen-tell", request, cancellationToken);

    public Task<ApiResult<string>> ConTell(Guid gameServerId, CoD4xTargetMessageRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "con-tell", request, cancellationToken);

    public Task<ApiResult<string>> Map(Guid gameServerId, CoD4xMapRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "map", request, cancellationToken);

    public Task<ApiResult<string>> MapRestart(Guid gameServerId, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "map-restart", cancellationToken: cancellationToken);

    public Task<ApiResult<string>> FastRestart(Guid gameServerId, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "fast-restart", cancellationToken: cancellationToken);

    public Task<ApiResult<string>> MapRotate(Guid gameServerId, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "map-rotate", cancellationToken: cancellationToken);

    public Task<ApiResult<string>> Gametype(Guid gameServerId, CoD4xGametypeRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "gametype", request, cancellationToken);

    public Task<ApiResult<string>> KillServer(Guid gameServerId, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "killserver", cancellationToken: cancellationToken);

    public Task<ApiResult<string>> Record(Guid gameServerId, CoD4xRecordRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "record", request, cancellationToken);

    public Task<ApiResult<string>> StopRecord(Guid gameServerId, CoD4xTargetRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "stop-record", request, cancellationToken);

    public Task<ApiResult<string>> AdminAddAdmin(Guid gameServerId, CoD4xAdminAddAdminRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "admin/add-admin", request, cancellationToken);

    public Task<ApiResult<string>> AdminRemoveAdmin(Guid gameServerId, CoD4xAdminUserRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "admin/remove-admin", request, cancellationToken);

    public Task<ApiResult<string>> AdminListAdmins(Guid gameServerId, CancellationToken cancellationToken = default) =>
        GetString(gameServerId, "admin/list-admins", cancellationToken);

    public Task<ApiResult<string>> AdminChangePassword(Guid gameServerId, CoD4xAdminChangePasswordRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "admin/change-password", request, cancellationToken);

    public Task<ApiResult<string>> AdminChangeCommandPower(Guid gameServerId, CoD4xAdminChangeCommandPowerRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "admin/change-command-power", request, cancellationToken);

    public Task<ApiResult<string>> AdminListCommands(Guid gameServerId, CancellationToken cancellationToken = default) =>
        GetString(gameServerId, "admin/list-commands", cancellationToken);

    public Task<ApiResult<string>> Undercover(Guid gameServerId, CoD4xUndercoverRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "undercover", request, cancellationToken);

    public Task<ApiResult<string>> Set(Guid gameServerId, CoD4xSetDvarRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "set", request, cancellationToken);

    public Task<ApiResult<string>> Seta(Guid gameServerId, CoD4xSetDvarRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "seta", request, cancellationToken);

    public Task<ApiResult<string>> Sets(Guid gameServerId, CoD4xSetDvarRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "sets", request, cancellationToken);

    public Task<ApiResult<string>> Setu(Guid gameServerId, CoD4xSetDvarRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "setu", request, cancellationToken);

    public Task<ApiResult<string>> CvarList(Guid gameServerId, CancellationToken cancellationToken = default) =>
        GetString(gameServerId, "cvarlist", cancellationToken);

    public Task<ApiResult<string>> CmdList(Guid gameServerId, CancellationToken cancellationToken = default) =>
        GetString(gameServerId, "cmdlist", cancellationToken);

    public Task<ApiResult<string>> LoadPlugin(Guid gameServerId, CoD4xPluginRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "plugins/load", request, cancellationToken);

    public Task<ApiResult<string>> UnloadPlugin(Guid gameServerId, CoD4xPluginRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "plugins/unload", request, cancellationToken);

    public Task<ApiResult<string>> Plugins(Guid gameServerId, CancellationToken cancellationToken = default) =>
        GetString(gameServerId, "plugins", cancellationToken);

    public Task<ApiResult<string>> PluginInfo(Guid gameServerId, CoD4xPluginRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "plugins/info", request, cancellationToken);

    public Task<ApiResult<string>> TakeScreenshot(Guid gameServerId, TakeScreenshotRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "screenshot", request, cancellationToken);

    public Task<ApiResult<string>> BanPlayerByPlayerIdentifier(Guid gameServerId, CoD4xPermBanRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "permban", request, cancellationToken);

    public Task<ApiResult<string>> TempBanPlayerByPlayerIdentifier(Guid gameServerId, CoD4xTempBanRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "tempban", request, cancellationToken);

    public Task<ApiResult<string>> UnbanPlayerByPlayerIdentifier(Guid gameServerId, CoD4xUnbanRequestDto request, CancellationToken cancellationToken = default) =>
        PostString(gameServerId, "unban", request, cancellationToken);
}
