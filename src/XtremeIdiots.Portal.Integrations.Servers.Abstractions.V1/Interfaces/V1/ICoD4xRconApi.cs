using MX.Api.Abstractions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;

public interface ICoD4xRconApi
{
    Task<ApiResult<string>> PermBan(Guid gameServerId, CoD4xPermBanRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> BanUser(Guid gameServerId, CoD4xTargetReasonRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> BanClient(Guid gameServerId, CoD4xClientReasonRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> TempBan(Guid gameServerId, CoD4xTempBanRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> Unban(Guid gameServerId, CoD4xUnbanRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> UnbanUser(Guid gameServerId, CoD4xTargetRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> Kick(Guid gameServerId, CoD4xTargetReasonRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> ClientKick(Guid gameServerId, CoD4xClientReasonRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> OnlyKick(Guid gameServerId, CoD4xClientReasonRequestDto request, CancellationToken cancellationToken = default);

    Task<ApiResult<string>> Status(Guid gameServerId, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> MiniStatus(Guid gameServerId, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> DumpUser(Guid gameServerId, CoD4xTargetRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> ServerInfo(Guid gameServerId, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> SystemInfo(Guid gameServerId, CancellationToken cancellationToken = default);

    Task<ApiResult<string>> ScreenSay(Guid gameServerId, CoD4xMessageRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> ConSay(Guid gameServerId, CoD4xMessageRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> Tell(Guid gameServerId, CoD4xTargetMessageRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> ScreenTell(Guid gameServerId, CoD4xTargetMessageRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> ConTell(Guid gameServerId, CoD4xTargetMessageRequestDto request, CancellationToken cancellationToken = default);

    Task<ApiResult<string>> Map(Guid gameServerId, CoD4xMapRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> MapRestart(Guid gameServerId, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> FastRestart(Guid gameServerId, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> MapRotate(Guid gameServerId, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> Gametype(Guid gameServerId, CoD4xGametypeRequestDto request, CancellationToken cancellationToken = default);

    Task<ApiResult<string>> KillServer(Guid gameServerId, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> Record(Guid gameServerId, CoD4xRecordRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> StopRecord(Guid gameServerId, CoD4xTargetRequestDto request, CancellationToken cancellationToken = default);

    Task<ApiResult<string>> AdminAddAdmin(Guid gameServerId, CoD4xAdminAddAdminRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> AdminRemoveAdmin(Guid gameServerId, CoD4xAdminUserRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> AdminListAdmins(Guid gameServerId, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> AdminChangePassword(Guid gameServerId, CoD4xAdminChangePasswordRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> AdminChangeCommandPower(Guid gameServerId, CoD4xAdminChangeCommandPowerRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> AdminListCommands(Guid gameServerId, CancellationToken cancellationToken = default);

    Task<ApiResult<string>> Undercover(Guid gameServerId, CoD4xUndercoverRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> Set(Guid gameServerId, CoD4xSetDvarRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> Seta(Guid gameServerId, CoD4xSetDvarRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> Sets(Guid gameServerId, CoD4xSetDvarRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> Setu(Guid gameServerId, CoD4xSetDvarRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> CvarList(Guid gameServerId, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> CmdList(Guid gameServerId, CancellationToken cancellationToken = default);

    Task<ApiResult<string>> LoadPlugin(Guid gameServerId, CoD4xPluginRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> UnloadPlugin(Guid gameServerId, CoD4xPluginRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> Plugins(Guid gameServerId, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> PluginInfo(Guid gameServerId, CoD4xPluginRequestDto request, CancellationToken cancellationToken = default);

    Task<ApiResult<string>> TakeScreenshot(Guid gameServerId, TakeScreenshotRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> BanPlayerByPlayerIdentifier(Guid gameServerId, CoD4xPermBanRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> TempBanPlayerByPlayerIdentifier(Guid gameServerId, CoD4xTempBanRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> UnbanPlayerByPlayerIdentifier(Guid gameServerId, CoD4xUnbanRequestDto request, CancellationToken cancellationToken = default);
}
