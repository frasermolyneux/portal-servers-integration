using System.Collections.Concurrent;
using System.Net;
using MX.Api.Abstractions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing;

public class FakeCoD4xRconApi : ICoD4xRconApi
{
    private readonly ConcurrentBag<(string Operation, Guid ServerId, object? Params)> _operationLog = [];

    public IReadOnlyCollection<(string Operation, Guid ServerId, object? Params)> OperationLog => _operationLog.ToArray();

    public DefaultBehavior DefaultResponseBehavior { get; private set; } = DefaultBehavior.ReturnGenericSuccess;

    public FakeCoD4xRconApi SetDefaultBehavior(DefaultBehavior behavior)
    {
        DefaultResponseBehavior = behavior;
        return this;
    }

    public void Reset()
    {
        _operationLog.Clear();
        DefaultResponseBehavior = DefaultBehavior.ReturnGenericSuccess;
    }

    private Task<ApiResult<string>> LogAndReturn(string operation, Guid gameServerId, object? parameters = null)
    {
        _operationLog.Add((operation, gameServerId, parameters));
        return Task.FromResult(DefaultResponseBehavior switch
        {
            DefaultBehavior.ReturnGenericSuccess => new ApiResult<string>(HttpStatusCode.OK, new ApiResponse<string>("ok")),
            DefaultBehavior.ReturnError => new ApiResult<string>(HttpStatusCode.InternalServerError, new ApiResponse<string>(new ApiError("FAILED", "Operation failed"))),
            _ => throw new InvalidOperationException($"Unknown default behavior: {DefaultResponseBehavior}")
        });
    }

    public Task<ApiResult<string>> PermBan(Guid gameServerId, CoD4xPermBanRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("PermBan", gameServerId, request);
    public Task<ApiResult<string>> BanUser(Guid gameServerId, CoD4xTargetReasonRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("BanUser", gameServerId, request);
    public Task<ApiResult<string>> BanClient(Guid gameServerId, CoD4xClientReasonRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("BanClient", gameServerId, request);
    public Task<ApiResult<string>> TempBan(Guid gameServerId, CoD4xTempBanRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("TempBan", gameServerId, request);
    public Task<ApiResult<string>> Unban(Guid gameServerId, CoD4xUnbanRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("Unban", gameServerId, request);
    public Task<ApiResult<string>> UnbanUser(Guid gameServerId, CoD4xTargetRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("UnbanUser", gameServerId, request);
    public Task<ApiResult<string>> Kick(Guid gameServerId, CoD4xTargetReasonRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("Kick", gameServerId, request);
    public Task<ApiResult<string>> ClientKick(Guid gameServerId, CoD4xClientReasonRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("ClientKick", gameServerId, request);
    public Task<ApiResult<string>> OnlyKick(Guid gameServerId, CoD4xClientReasonRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("OnlyKick", gameServerId, request);
    public Task<ApiResult<string>> Status(Guid gameServerId, CancellationToken cancellationToken = default) => LogAndReturn("Status", gameServerId);
    public Task<ApiResult<string>> MiniStatus(Guid gameServerId, CancellationToken cancellationToken = default) => LogAndReturn("MiniStatus", gameServerId);
    public Task<ApiResult<string>> DumpUser(Guid gameServerId, CoD4xTargetRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("DumpUser", gameServerId, request);
    public Task<ApiResult<string>> DumpBanList(Guid gameServerId, CancellationToken cancellationToken = default) => LogAndReturn("DumpBanList", gameServerId);
    public Task<ApiResult<string>> ServerInfo(Guid gameServerId, CancellationToken cancellationToken = default) => LogAndReturn("ServerInfo", gameServerId);
    public Task<ApiResult<string>> SystemInfo(Guid gameServerId, CancellationToken cancellationToken = default) => LogAndReturn("SystemInfo", gameServerId);
    public Task<ApiResult<string>> ScreenSay(Guid gameServerId, CoD4xMessageRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("ScreenSay", gameServerId, request);
    public Task<ApiResult<string>> ConSay(Guid gameServerId, CoD4xMessageRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("ConSay", gameServerId, request);
    public Task<ApiResult<string>> Tell(Guid gameServerId, CoD4xTargetMessageRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("Tell", gameServerId, request);
    public Task<ApiResult<string>> ScreenTell(Guid gameServerId, CoD4xTargetMessageRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("ScreenTell", gameServerId, request);
    public Task<ApiResult<string>> ConTell(Guid gameServerId, CoD4xTargetMessageRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("ConTell", gameServerId, request);
    public Task<ApiResult<string>> Map(Guid gameServerId, CoD4xMapRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("Map", gameServerId, request);
    public Task<ApiResult<string>> MapRestart(Guid gameServerId, CancellationToken cancellationToken = default) => LogAndReturn("MapRestart", gameServerId);
    public Task<ApiResult<string>> FastRestart(Guid gameServerId, CancellationToken cancellationToken = default) => LogAndReturn("FastRestart", gameServerId);
    public Task<ApiResult<string>> MapRotate(Guid gameServerId, CancellationToken cancellationToken = default) => LogAndReturn("MapRotate", gameServerId);
    public Task<ApiResult<string>> Gametype(Guid gameServerId, CoD4xGametypeRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("Gametype", gameServerId, request);
    public Task<ApiResult<string>> KillServer(Guid gameServerId, CancellationToken cancellationToken = default) => LogAndReturn("KillServer", gameServerId);
    public Task<ApiResult<string>> Record(Guid gameServerId, CoD4xRecordRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("Record", gameServerId, request);
    public Task<ApiResult<string>> StopRecord(Guid gameServerId, CoD4xTargetRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("StopRecord", gameServerId, request);
    public Task<ApiResult<string>> AdminAddAdmin(Guid gameServerId, CoD4xAdminAddAdminRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("AdminAddAdmin", gameServerId, request);
    public Task<ApiResult<string>> AdminRemoveAdmin(Guid gameServerId, CoD4xAdminUserRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("AdminRemoveAdmin", gameServerId, request);
    public Task<ApiResult<string>> AdminListAdmins(Guid gameServerId, CancellationToken cancellationToken = default) => LogAndReturn("AdminListAdmins", gameServerId);
    public Task<ApiResult<string>> AdminChangePassword(Guid gameServerId, CoD4xAdminChangePasswordRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("AdminChangePassword", gameServerId, request);
    public Task<ApiResult<string>> AdminChangeCommandPower(Guid gameServerId, CoD4xAdminChangeCommandPowerRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("AdminChangeCommandPower", gameServerId, request);
    public Task<ApiResult<string>> AdminListCommands(Guid gameServerId, CancellationToken cancellationToken = default) => LogAndReturn("AdminListCommands", gameServerId);
    public Task<ApiResult<string>> Undercover(Guid gameServerId, CoD4xUndercoverRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("Undercover", gameServerId, request);
    public Task<ApiResult<string>> Set(Guid gameServerId, CoD4xSetDvarRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("Set", gameServerId, request);
    public Task<ApiResult<string>> Seta(Guid gameServerId, CoD4xSetDvarRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("Seta", gameServerId, request);
    public Task<ApiResult<string>> Sets(Guid gameServerId, CoD4xSetDvarRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("Sets", gameServerId, request);
    public Task<ApiResult<string>> Setu(Guid gameServerId, CoD4xSetDvarRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("Setu", gameServerId, request);
    public Task<ApiResult<string>> CvarList(Guid gameServerId, CancellationToken cancellationToken = default) => LogAndReturn("CvarList", gameServerId);
    public Task<ApiResult<string>> CmdList(Guid gameServerId, CancellationToken cancellationToken = default) => LogAndReturn("CmdList", gameServerId);
    public Task<ApiResult<string>> LoadPlugin(Guid gameServerId, CoD4xPluginRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("LoadPlugin", gameServerId, request);
    public Task<ApiResult<string>> UnloadPlugin(Guid gameServerId, CoD4xPluginRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("UnloadPlugin", gameServerId, request);
    public Task<ApiResult<string>> Plugins(Guid gameServerId, CancellationToken cancellationToken = default) => LogAndReturn("Plugins", gameServerId);
    public Task<ApiResult<string>> PluginInfo(Guid gameServerId, CoD4xPluginRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("PluginInfo", gameServerId, request);
    public Task<ApiResult<string>> TakeScreenshot(Guid gameServerId, TakeScreenshotRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("TakeScreenshot", gameServerId, request);
    public Task<ApiResult<string>> BanPlayerByPlayerIdentifier(Guid gameServerId, CoD4xPermBanRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("BanPlayerByPlayerIdentifier", gameServerId, request);
    public Task<ApiResult<string>> TempBanPlayerByPlayerIdentifier(Guid gameServerId, CoD4xTempBanRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("TempBanPlayerByPlayerIdentifier", gameServerId, request);
    public Task<ApiResult<string>> UnbanPlayerByPlayerIdentifier(Guid gameServerId, CoD4xUnbanRequestDto request, CancellationToken cancellationToken = default) => LogAndReturn("UnbanPlayerByPlayerIdentifier", gameServerId, request);
}
