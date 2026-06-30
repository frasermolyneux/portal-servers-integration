using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;
using System.Net;
using Asp.Versioning;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MX.Api.Abstractions;
using MX.Api.Web.Extensions;
using MX.Observability.ApplicationInsights.Auditing;
using MX.Observability.ApplicationInsights.Auditing.Models;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Constants;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.GameServers;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1;

[ApiController]
[Authorize(Roles = "ServiceAccount")]
[ApiVersion(ApiVersions.V1)]
[Route("v{version:apiVersion}")]
public class CoD4xRconController(
    ILogger<CoD4xRconController> logger,
    IRepositoryApiClient repositoryApiClient,
    IRconClientFactory rconClientFactory,
    TelemetryClient telemetryClient,
    IAuditLogger auditLogger) : Controller
{
    private const int MaxCoD4xTempBanDurationMinutes = 525600;
    private static readonly Regex CoD4xPlayerIdentifierRegex = new(@"^[0-9]{17,21}$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex CoD4xStatusPlayerRegex = new(@"^\s*(?<num>\d+)\s+(?<score>-?\d+)\s+(?<ping>CNCT|ZMBI|PRIM|\d+)\s+(?<playerid>\d{6,20}|\[U:\d+:\d+\]|STEAM_\d:\d:\d+)\s+(?<steamid>\d{1,20}|\[U:\d+:\d+\]|STEAM_\d:\d:\d+)\s+(?<name>.+?)\s+(?<lastmsg>\d+)\s+(?<address>\[?[0-9a-fA-F:.]+\]?:\d+)\s+(?<qport>\d+)\s+(?<rate>\d+)\s*$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex CoD4xDumpBanListEntryRegex = new(@"^(?<index>\d+) playerid: (?<playerid>\d{6,20}|\[U:\d+:\d+\]|STEAM_\d:\d:\d+); nick: (?<nick>.*?); adminsteamid: (?<admin>System/Rcon|\d{1,20}|\[U:\d+:\d+\]|STEAM_\d:\d:\d+); expire: (?<expire>Never|.+?); reason: (?<reason>.*)$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex CoD4xDumpBanListCountRegex = new(@"^(?<count>\d+) Active bans$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex CoD4xBanAddedOnlineRegex = new(@"^(?:attempting to add Banrecord for player:|Banrecord added for player:)\s*(?<name>.+)\s+id:\s+(?<playerid>\d{6,20}|\[U:\d+:\d+\]|STEAM_\d:\d:\d+)\s*$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex CoD4xBanAddedOfflineRegex = new(@"^Banrecord added for id:\s+(?<playerid>\d{6,20}|\[U:\d+:\d+\]|STEAM_\d:\d:\d+)\s*$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex CoD4xUnbanRemovedRegex = new(@"^Removing ban for Nick:\s*(?<nick>.*),\s*PlayerID:\s*(?<playerid>\d{6,20}|\[U:\d+:\d+\]|STEAM_\d:\d:\d+),\s*Banreason:\s*(?<reason>.*)$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex CoD4xErrorRegex = new(@"^Error:\s*(?<error>.+)$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly Regex QuakeColorCodeRegex = new(@"\^[0-9A-Za-z]", RegexOptions.Compiled, TimeSpan.FromSeconds(1));
    private static readonly char[] InvalidTargetCharacters = ['"', ';', '\r', '\n'];
    private static readonly char[] InvalidCommandTextCharacters = [';', '\r', '\n'];

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/permban")]
    public Task<IActionResult> PermBan(Guid gameServerId, [FromBody] CoD4xPermBanRequestDto? request) =>
        ExecuteStructuredAction(
            gameServerId,
            "RconCoD4xPermBan",
            AuditAction.Moderate,
            request,
            RequireCoD4xPermBan,
            (client, dto, ct) => client.BanPlayerByPlayerIdentifier(dto.PlayerIdentifier!),
            result => ParseBanCommandResponse(result, "PermBan"),
            HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/tempban")]
    public Task<IActionResult> TempBan(Guid gameServerId, [FromBody] CoD4xTempBanRequestDto? request) =>
        ExecuteStructuredAction(
            gameServerId,
            "RconCoD4xTempBan",
            AuditAction.Moderate,
            request,
            RequireCoD4xTempBan,
            (client, dto, ct) => client.TempBanPlayerByPlayerIdentifier(dto.PlayerIdentifier!, dto.DurationMinutes),
            result => ParseBanCommandResponse(result, "TempBan"),
            HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/unban")]
    public Task<IActionResult> Unban(Guid gameServerId, [FromBody] CoD4xUnbanRequestDto? request) =>
        ExecuteStructuredAction(
            gameServerId,
            "RconCoD4xUnban",
            AuditAction.Moderate,
            request,
            RequireCoD4xUnban,
            (client, dto, ct) => client.UnbanPlayerByPlayerIdentifier(dto.PlayerIdentifier!),
            result => ParseBanCommandResponse(result, "Unban"),
            HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/ban-user")]
    public Task<IActionResult> BanUser(Guid gameServerId, [FromBody] CoD4xTargetReasonRequestDto? request) =>
        ExecuteAction(gameServerId, "RconCoD4xBanUser", AuditAction.Moderate, request, RequireTargetAndReason, (client, dto, ct) => client.BanUser(dto.Target!, dto.Reason!), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/ban-client")]
    public Task<IActionResult> BanClient(Guid gameServerId, [FromBody] CoD4xClientReasonRequestDto? request) =>
        ExecuteAction(gameServerId, "RconCoD4xBanClient", AuditAction.Moderate, request, RequireClientAndReason, (client, dto, ct) => client.BanClient(dto.ClientId, dto.Reason!), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/unban-user")]
    public Task<IActionResult> UnbanUser(Guid gameServerId, [FromBody] CoD4xTargetRequestDto? request) =>
        ExecuteAction(gameServerId, "RconCoD4xUnbanUser", AuditAction.Moderate, request, RequireTarget, (client, dto, ct) => client.UnbanUser(dto.Target!), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/kick")]
    public Task<IActionResult> Kick(Guid gameServerId, [FromBody] CoD4xTargetReasonRequestDto? request) =>
        ExecuteAction(gameServerId, "RconCoD4xKick", AuditAction.Moderate, request, RequireTargetAndReason, (client, dto, ct) => client.Kick(dto.Target!, dto.Reason!), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/client-kick")]
    public Task<IActionResult> ClientKick(Guid gameServerId, [FromBody] CoD4xClientReasonRequestDto? request) =>
        ExecuteAction(gameServerId, "RconCoD4xClientKick", AuditAction.Moderate, request, RequireClientAndReason, (client, dto, ct) => client.ClientKick(dto.ClientId, dto.Reason!), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/only-kick")]
    public Task<IActionResult> OnlyKick(Guid gameServerId, [FromBody] CoD4xClientReasonRequestDto? request) =>
        ExecuteAction(gameServerId, "RconCoD4xOnlyKick", AuditAction.Moderate, request, RequireClientAndReason, (client, dto, ct) => client.OnlyKick(dto.ClientId, dto.Reason!), HttpContext.RequestAborted);

    [HttpGet]
    [Route("rcon/{gameServerId}/cod4x/status")]
    public Task<IActionResult> Status(Guid gameServerId) =>
        ExecuteStructuredAction(
            gameServerId,
            "RconCoD4xStatus",
            null,
            (client, ct) => client.Status(),
            ParseStatusResponse,
            HttpContext.RequestAborted);

    [HttpGet]
    [Route("rcon/{gameServerId}/cod4x/ministatus")]
    public Task<IActionResult> MiniStatus(Guid gameServerId) =>
        ExecuteAction(gameServerId, "RconCoD4xMiniStatus", null, ct => ct.MiniStatus(), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/dump-user")]
    public Task<IActionResult> DumpUser(Guid gameServerId, [FromBody] CoD4xTargetRequestDto? request) =>
        ExecuteAction(gameServerId, "RconCoD4xDumpUser", null, request, RequireTarget, (client, dto, ct) => client.DumpUser(dto.Target!), HttpContext.RequestAborted);

    [HttpGet]
    [Route("rcon/{gameServerId}/cod4x/dumpbanlist")]
    public Task<IActionResult> DumpBanList(Guid gameServerId) =>
        ExecuteStructuredAction(
            gameServerId,
            "RconCoD4xDumpBanList",
            null,
            (client, ct) => client.DumpBanList(),
            ParseBanListResponse,
            HttpContext.RequestAborted);

    [HttpGet]
    [Route("rcon/{gameServerId}/cod4x/server-info")]
    public Task<IActionResult> ServerInfo(Guid gameServerId) =>
        ExecuteAction(gameServerId, "RconCoD4xServerInfo", null, ct => ct.ServerInfo(), HttpContext.RequestAborted);

    [HttpGet]
    [Route("rcon/{gameServerId}/cod4x/system-info")]
    public Task<IActionResult> SystemInfo(Guid gameServerId) =>
        ExecuteAction(gameServerId, "RconCoD4xSystemInfo", null, ct => ct.SystemInfo(), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/screen-say")]
    public Task<IActionResult> ScreenSay(Guid gameServerId, [FromBody] CoD4xMessageRequestDto? request) =>
        ExecuteAction(gameServerId, "RconCoD4xScreenSay", null, request, RequireMessage, (client, dto, ct) => client.ScreenSay(dto.Message!), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/con-say")]
    public Task<IActionResult> ConSay(Guid gameServerId, [FromBody] CoD4xMessageRequestDto? request) =>
        ExecuteAction(gameServerId, "RconCoD4xConSay", null, request, RequireMessage, (client, dto, ct) => client.ConSay(dto.Message!), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/tell")]
    public Task<IActionResult> Tell(Guid gameServerId, [FromBody] CoD4xTargetMessageRequestDto? request) =>
        ExecuteAction(gameServerId, "RconCoD4xTell", null, request, RequireTargetAndMessage, (client, dto, ct) => client.Tell(dto.Target!, dto.Message!), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/screen-tell")]
    public Task<IActionResult> ScreenTell(Guid gameServerId, [FromBody] CoD4xTargetMessageRequestDto? request) =>
        ExecuteAction(gameServerId, "RconCoD4xScreenTell", null, request, RequireTargetAndMessage, (client, dto, ct) => client.ScreenTell(dto.Target!, dto.Message!), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/con-tell")]
    public Task<IActionResult> ConTell(Guid gameServerId, [FromBody] CoD4xTargetMessageRequestDto? request) =>
        ExecuteAction(gameServerId, "RconCoD4xConTell", null, request, RequireTargetAndMessage, (client, dto, ct) => client.ConTell(dto.Target!, dto.Message!), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/map")]
    public Task<IActionResult> Map(Guid gameServerId, [FromBody] CoD4xMapRequestDto? request) =>
        ExecuteAction(gameServerId, "RconCoD4xMap", AuditAction.Execute, request, RequireMap, (client, dto, ct) => client.Map(dto.MapName!), HttpContext.RequestAborted);

    [HttpGet]
    [Route("rcon/{gameServerId}/cod4x/maps")]
    public Task<IActionResult> GetMaps(Guid gameServerId) =>
        ExecuteStructuredAction(
            gameServerId,
            "RconCoD4xMaps",
            null,
            (client, ct) => client.GetMaps(),
            maps => new RconMapCollectionDto(maps.Select(map => new RconMapDto(map.GameType, map.MapName))),
            HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/map-restart")]
    public Task<IActionResult> MapRestart(Guid gameServerId) =>
        ExecuteAction(gameServerId, "RconCoD4xMapRestart", AuditAction.Execute, ct => ct.MapRestart(), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/fast-restart")]
    public Task<IActionResult> FastRestart(Guid gameServerId) =>
        ExecuteAction(gameServerId, "RconCoD4xFastRestart", AuditAction.Execute, ct => ct.FastRestart(), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/map-rotate")]
    public Task<IActionResult> MapRotate(Guid gameServerId) =>
        ExecuteAction(gameServerId, "RconCoD4xMapRotate", AuditAction.Execute, ct => ct.MapRotate(), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/gametype")]
    public Task<IActionResult> Gametype(Guid gameServerId, [FromBody] CoD4xGametypeRequestDto? request) =>
        ExecuteAction(gameServerId, "RconCoD4xGametype", AuditAction.Execute, request, RequireGametype, (client, dto, ct) => client.Gametype(dto.GameType!), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/killserver")]
    public Task<IActionResult> KillServer(Guid gameServerId) =>
        ExecuteAction(gameServerId, "RconCoD4xKillServer", AuditAction.Moderate, ct => ct.KillServer(), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/record")]
    public Task<IActionResult> Record(Guid gameServerId, [FromBody] CoD4xRecordRequestDto? request) =>
        ExecuteAction(gameServerId, "RconCoD4xRecord", AuditAction.Execute, request, RequireRecordRequest, (client, dto, ct) => client.Record(dto.Target!, dto.DemoName), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/stop-record")]
    public Task<IActionResult> StopRecord(Guid gameServerId, [FromBody] CoD4xTargetRequestDto? request) =>
        ExecuteAction(gameServerId, "RconCoD4xStopRecord", AuditAction.Execute, request, RequireTarget, (client, dto, ct) => client.StopRecord(dto.Target!), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/admin/add-admin")]
    public Task<IActionResult> AdminAddAdmin(Guid gameServerId, [FromBody] CoD4xAdminAddAdminRequestDto? request) =>
        ExecuteAction(gameServerId, "RconCoD4xAdminAddAdmin", AuditAction.Moderate, request, RequireAdminAdd, (client, dto, ct) => client.AdminAddAdmin(dto.User!, dto.Power), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/admin/remove-admin")]
    public Task<IActionResult> AdminRemoveAdmin(Guid gameServerId, [FromBody] CoD4xAdminUserRequestDto? request) =>
        ExecuteAction(gameServerId, "RconCoD4xAdminRemoveAdmin", AuditAction.Moderate, request, RequireAdminUser, (client, dto, ct) => client.AdminRemoveAdmin(dto.User!), HttpContext.RequestAborted);

    [HttpGet]
    [Route("rcon/{gameServerId}/cod4x/admin/list-admins")]
    public Task<IActionResult> AdminListAdmins(Guid gameServerId) =>
        ExecuteAction(gameServerId, "RconCoD4xAdminListAdmins", null, ct => ct.AdminListAdmins(), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/admin/change-password")]
    public Task<IActionResult> AdminChangePassword(Guid gameServerId, [FromBody] CoD4xAdminChangePasswordRequestDto? request) =>
        ExecuteAction(gameServerId, "RconCoD4xAdminChangePassword", AuditAction.Moderate, request, RequireAdminPasswordChange, (client, dto, ct) => client.AdminChangePassword(dto.User!, dto.NewPassword!), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/admin/change-command-power")]
    public Task<IActionResult> AdminChangeCommandPower(Guid gameServerId, [FromBody] CoD4xAdminChangeCommandPowerRequestDto? request) =>
        ExecuteAction(gameServerId, "RconCoD4xAdminChangeCommandPower", AuditAction.Moderate, request, RequireCommandPowerChange, (client, dto, ct) => client.AdminChangeCommandPower(dto.Command!, dto.MinPower), HttpContext.RequestAborted);

    [HttpGet]
    [Route("rcon/{gameServerId}/cod4x/admin/list-commands")]
    public Task<IActionResult> AdminListCommands(Guid gameServerId) =>
        ExecuteAction(gameServerId, "RconCoD4xAdminListCommands", null, ct => ct.AdminListCommands(), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/undercover")]
    public Task<IActionResult> Undercover(Guid gameServerId, [FromBody] CoD4xUndercoverRequestDto? request) =>
        ExecuteAction(gameServerId, "RconCoD4xUndercover", AuditAction.Execute, request, _ => null, (client, dto, ct) => client.Undercover(dto.Enabled), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/set")]
    public Task<IActionResult> Set(Guid gameServerId, [FromBody] CoD4xSetDvarRequestDto? request) =>
        ExecuteAction(gameServerId, "RconCoD4xSet", AuditAction.Execute, request, RequireSetRequest, (client, dto, ct) => client.Set(dto.DvarName!, dto.Value!), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/seta")]
    public Task<IActionResult> Seta(Guid gameServerId, [FromBody] CoD4xSetDvarRequestDto? request) =>
        ExecuteAction(gameServerId, "RconCoD4xSeta", AuditAction.Execute, request, RequireSetRequest, (client, dto, ct) => client.Seta(dto.DvarName!, dto.Value!), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/sets")]
    public Task<IActionResult> Sets(Guid gameServerId, [FromBody] CoD4xSetDvarRequestDto? request) =>
        ExecuteAction(gameServerId, "RconCoD4xSets", AuditAction.Execute, request, RequireSetRequest, (client, dto, ct) => client.Sets(dto.DvarName!, dto.Value!), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/setu")]
    public Task<IActionResult> Setu(Guid gameServerId, [FromBody] CoD4xSetDvarRequestDto? request) =>
        ExecuteAction(gameServerId, "RconCoD4xSetu", AuditAction.Execute, request, RequireSetRequest, (client, dto, ct) => client.Setu(dto.DvarName!, dto.Value!), HttpContext.RequestAborted);

    [HttpGet]
    [Route("rcon/{gameServerId}/cod4x/cvarlist")]
    public Task<IActionResult> CvarList(Guid gameServerId) =>
        ExecuteAction(gameServerId, "RconCoD4xCvarList", null, ct => ct.CvarList(), HttpContext.RequestAborted);

    [HttpGet]
    [Route("rcon/{gameServerId}/cod4x/cmdlist")]
    public Task<IActionResult> CmdList(Guid gameServerId) =>
        ExecuteAction(gameServerId, "RconCoD4xCmdList", null, ct => ct.CmdList(), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/plugins/load")]
    public Task<IActionResult> LoadPlugin(Guid gameServerId, [FromBody] CoD4xPluginRequestDto? request) =>
        ExecuteAction(gameServerId, "RconCoD4xLoadPlugin", AuditAction.Execute, request, RequirePluginRequest, (client, dto, ct) => client.LoadPlugin(dto.PluginName!), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/plugins/unload")]
    public Task<IActionResult> UnloadPlugin(Guid gameServerId, [FromBody] CoD4xPluginRequestDto? request) =>
        ExecuteAction(gameServerId, "RconCoD4xUnloadPlugin", AuditAction.Execute, request, RequirePluginRequest, (client, dto, ct) => client.UnloadPlugin(dto.PluginName!), HttpContext.RequestAborted);

    [HttpGet]
    [Route("rcon/{gameServerId}/cod4x/plugins")]
    public Task<IActionResult> Plugins(Guid gameServerId) =>
        ExecuteAction(gameServerId, "RconCoD4xPlugins", null, ct => ct.Plugins(), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/plugins/info")]
    public Task<IActionResult> PluginInfo(Guid gameServerId, [FromBody] CoD4xPluginRequestDto? request) =>
        ExecuteAction(gameServerId, "RconCoD4xPluginInfo", null, request, RequirePluginRequest, (client, dto, ct) => client.PluginInfo(dto.PluginName!), HttpContext.RequestAborted);

    [HttpPost]
    [Route("rcon/{gameServerId}/cod4x/screenshot")]
    public Task<IActionResult> TakeScreenshot(Guid gameServerId, [FromBody] TakeScreenshotRequestDto? request) =>
        ExecuteAction(gameServerId, "RconCoD4xScreenshot", AuditAction.Execute, request, RequirePlayerIdentifier, (client, dto, ct) => client.TakeScreenshot(dto.PlayerIdentifier!, ct), HttpContext.RequestAborted);

    private async Task<IActionResult> ExecuteAction<TRequest>(
        Guid gameServerId,
        string operationName,
        AuditAction? auditAction,
        TRequest? request,
        Func<TRequest, ApiResult<string>?> validate,
        Func<ICallOfDuty4xRconClient, TRequest, CancellationToken, Task<string>> execute,
        CancellationToken cancellationToken)
        where TRequest : class
    {
        if (request == null)
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, "Request body cannot be null.")).ToBadRequestResult().ToHttpResult();
        }

        var validation = validate(request);
        if (validation != null)
        {
            return validation.ToHttpResult();
        }

        var normalizedRequest = NormalizeRequest(request);
        var operationContext = BuildOperationContext(normalizedRequest);
        var response = await ExecuteAction(gameServerId, operationName, auditAction, (client, ct) => execute(client, normalizedRequest, ct), operationContext, cancellationToken).ConfigureAwait(false);
        return response.ToHttpResult();
    }

    private async Task<IActionResult> ExecuteStructuredAction<TRequest, TResponse>(
        Guid gameServerId,
        string operationName,
        AuditAction? auditAction,
        TRequest? request,
        Func<TRequest, ApiResult<string>?> validate,
        Func<ICallOfDuty4xRconClient, TRequest, CancellationToken, Task<string>> execute,
        Func<string, TResponse> parse,
        CancellationToken cancellationToken)
        where TRequest : class
    {
        if (request == null)
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, "Request body cannot be null.")).ToBadRequestResult().ToHttpResult();
        }

        var validation = validate(request);
        if (validation != null)
        {
            return validation.ToHttpResult();
        }

        var normalizedRequest = NormalizeRequest(request);
        var operationContext = BuildOperationContext(normalizedRequest);
        var rawResult = await ExecuteAction(gameServerId, operationName, auditAction, (client, ct) => execute(client, normalizedRequest, ct), operationContext, cancellationToken).ConfigureAwait(false);
        return MapStructuredResult(rawResult, parse).ToHttpResult();
    }

    private async Task<IActionResult> ExecuteStructuredAction<TResponse>(
        Guid gameServerId,
        string operationName,
        AuditAction? auditAction,
        Func<ICallOfDuty4xRconClient, CancellationToken, Task<string>> execute,
        Func<string, TResponse> parse,
        CancellationToken cancellationToken)
    {
        var rawResult = await ExecuteAction(gameServerId, operationName, auditAction, execute, null, cancellationToken).ConfigureAwait(false);
        return MapStructuredResult(rawResult, parse).ToHttpResult();
    }

    private async Task<IActionResult> ExecuteStructuredAction<TData, TResponse>(
        Guid gameServerId,
        string operationName,
        AuditAction? auditAction,
        Func<ICallOfDuty4xRconClient, CancellationToken, Task<TData>> execute,
        Func<TData, TResponse> parse,
        CancellationToken cancellationToken)
    {
        _ = auditAction;

        var contextResult = await TryGetCoD4xContext(gameServerId, cancellationToken).ConfigureAwait(false);
        if (contextResult.Error != null)
        {
            return contextResult.Error.ToHttpResult();
        }

        var context = contextResult.Context!;
        var operation = telemetryClient.StartOperation<DependencyTelemetry>(operationName);
        operation.Telemetry.Type = $"{context.GameType}Server";
        operation.Telemetry.Target = $"{context.Hostname}:{context.QueryPort}";

        try
        {
            var rawData = await execute(context.Client, cancellationToken).ConfigureAwait(false);
            var mappedResult = parse(rawData);
            return new ApiResponse<TResponse>(mappedResult).ToApiResult().ToHttpResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogWarning(ex, "{OperationName} is not implemented for game server {GameServerId}", operationName, gameServerId);
            return new ApiResponse<TResponse>(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The requested CoD4x operation is not implemented for this game server type."))
                .ToApiResult()
                .ToHttpResult();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = "Cancelled";
            throw;
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to execute {OperationName} on game server {GameServerId}", operationName, gameServerId);
            return new ApiResponse<TResponse>(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to execute CoD4x command via RCON."))
                .ToApiResult()
                .ToHttpResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    private static ApiResult<TResponse> MapStructuredResult<TResponse>(ApiResult<string> rawResult, Func<string, TResponse> parse)
    {
        if (!rawResult.IsSuccess)
        {
            var error = rawResult.Result?.Errors?.FirstOrDefault();
            if (error != null)
            {
                return new ApiResult<TResponse>(rawResult.StatusCode, new ApiResponse<TResponse>(new ApiError(error.Code, error.Message)));
            }

            return new ApiResult<TResponse>(rawResult.StatusCode, new ApiResponse<TResponse>(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to execute CoD4x command via RCON.")));
        }

        try
        {
            var structuredResult = parse(rawResult.Result?.Data ?? string.Empty);
            return new ApiResult<TResponse>(rawResult.StatusCode, new ApiResponse<TResponse>(structuredResult));
        }
        catch (RegexMatchTimeoutException)
        {
            return new ApiResult<TResponse>(HttpStatusCode.InternalServerError, new ApiResponse<TResponse>(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to parse CoD4x command response due to regex timeout.")));
        }
        catch (Exception)
        {
            return new ApiResult<TResponse>(HttpStatusCode.InternalServerError, new ApiResponse<TResponse>(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to parse CoD4x command response.")));
        }
    }

    private static CoD4xBanCommandResponseDto ParseBanCommandResponse(string result, string operation)
    {
        var lines = result.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            var errorMatch = CoD4xErrorRegex.Match(line);
            if (errorMatch.Success)
            {
                return new CoD4xBanCommandResponseDto
                {
                    Outcome = "Error",
                    IsSuccess = false,
                    ErrorMessage = errorMatch.Groups["error"].Value,
                    RawResponse = result
                };
            }
        }

        foreach (var line in lines)
        {
            var addedOnlineMatch = CoD4xBanAddedOnlineRegex.Match(line);
            if (addedOnlineMatch.Success)
            {
                return new CoD4xBanCommandResponseDto
                {
                    Outcome = "AddedOnline",
                    IsSuccess = true,
                    PlayerIdentifier = addedOnlineMatch.Groups["playerid"].Value,
                    PlayerName = addedOnlineMatch.Groups["name"].Value,
                    RawResponse = result
                };
            }

            var addedOfflineMatch = CoD4xBanAddedOfflineRegex.Match(line);
            if (addedOfflineMatch.Success)
            {
                return new CoD4xBanCommandResponseDto
                {
                    Outcome = "AddedOffline",
                    IsSuccess = true,
                    PlayerIdentifier = addedOfflineMatch.Groups["playerid"].Value,
                    RawResponse = result
                };
            }

            var removedMatch = CoD4xUnbanRemovedRegex.Match(line);
            if (removedMatch.Success)
            {
                return new CoD4xBanCommandResponseDto
                {
                    Outcome = "Removed",
                    IsSuccess = true,
                    PlayerIdentifier = removedMatch.Groups["playerid"].Value,
                    PlayerName = removedMatch.Groups["nick"].Value,
                    BanReason = removedMatch.Groups["reason"].Value,
                    RawResponse = result
                };
            }
        }

        if (string.Equals(operation, "Unban", StringComparison.OrdinalIgnoreCase)
            && lines.Length == 0)
        {
            return new CoD4xBanCommandResponseDto
            {
                Outcome = "NoMatch",
                IsSuccess = false,
                RawResponse = result
            };
        }

        return new CoD4xBanCommandResponseDto
        {
            Outcome = lines.Length == 0 ? "Empty" : "Unknown",
            IsSuccess = false,
            RawResponse = result
        };
    }

    private static CoD4xBanListResponseDto ParseBanListResponse(string result)
    {
        var response = new CoD4xBanListResponseDto { RawResponse = result };
        var lines = result.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            var entryMatch = CoD4xDumpBanListEntryRegex.Match(line);
            if (entryMatch.Success)
            {
                _ = int.TryParse(entryMatch.Groups["index"].Value, out var index);

                response.Entries.Add(new CoD4xBanEntryDto
                {
                    Index = index,
                    PlayerIdentifier = entryMatch.Groups["playerid"].Value,
                    Nick = entryMatch.Groups["nick"].Value,
                    AdminSteamId = entryMatch.Groups["admin"].Value,
                    Expire = entryMatch.Groups["expire"].Value,
                    IsPermanent = string.Equals(entryMatch.Groups["expire"].Value, "Never", StringComparison.OrdinalIgnoreCase),
                    Reason = entryMatch.Groups["reason"].Value
                });

                continue;
            }

            var countMatch = CoD4xDumpBanListCountRegex.Match(line);
            if (countMatch.Success && int.TryParse(countMatch.Groups["count"].Value, out var activeCount))
            {
                response.ActiveBanCount = activeCount;
            }
        }

        if (response.ActiveBanCount == 0 && response.Entries.Count > 0)
        {
            response.ActiveBanCount = response.Entries.Count;
        }

        return response;
    }

    private static CoD4xStatusResponseDto ParseStatusResponse(string result)
    {
        var response = new CoD4xStatusResponseDto { RawResponse = result };
        var lines = result.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex > 0)
            {
                var key = line[..separatorIndex].Trim();
                var value = line[(separatorIndex + 1)..].Trim();

                switch (key.ToLowerInvariant())
                {
                    case "hostname":
                        response.Hostname = value;
                        break;
                    case "version":
                        response.Version = value;
                        break;
                    case "udp/ip":
                        response.UdpEndpoint = value;
                        break;
                    case "os":
                        response.OperatingSystem = value;
                        break;
                    case "type":
                        response.ServerType = value;
                        break;
                    case "map":
                        response.MapName = value;
                        break;
                    default:
                        break;
                }
            }

            var playerMatch = CoD4xStatusPlayerRegex.Match(line);
            if (!playerMatch.Success)
            {
                continue;
            }

            _ = int.TryParse(playerMatch.Groups["num"].Value, out var num);
            _ = int.TryParse(playerMatch.Groups["score"].Value, out var score);
            var pingRaw = playerMatch.Groups["ping"].Value;
            _ = int.TryParse(pingRaw, out var pingValue);
            _ = int.TryParse(playerMatch.Groups["lastmsg"].Value, out var lastMessageSeconds);
            _ = int.TryParse(playerMatch.Groups["qport"].Value, out var qport);
            _ = int.TryParse(playerMatch.Groups["rate"].Value, out var rate);

            var rawName = playerMatch.Groups["name"].Value.Trim();

            response.Players.Add(new CoD4xStatusPlayerDto
            {
                Num = num,
                Score = score,
                PingRaw = pingRaw,
                Ping = int.TryParse(pingRaw, out _) ? pingValue : null,
                PlayerIdentifier = playerMatch.Groups["playerid"].Value,
                SteamId = playerMatch.Groups["steamid"].Value,
                RawName = rawName,
                Name = QuakeColorCodeRegex.Replace(rawName, string.Empty),
                LastMessageSeconds = lastMessageSeconds,
                Address = playerMatch.Groups["address"].Value,
                QPort = qport,
                Rate = rate
            });
        }

        return response;
    }

    private async Task<IActionResult> ExecuteAction(
        Guid gameServerId,
        string operationName,
        AuditAction? auditAction,
        Func<ICallOfDuty4xRconClient, Task<string>> execute,
        CancellationToken cancellationToken)
    {
        var response = await ExecuteAction(gameServerId, operationName, auditAction, (client, _) => execute(client), null, cancellationToken).ConfigureAwait(false);
        return response.ToHttpResult();
    }

    private async Task<ApiResult<string>> ExecuteAction(
        Guid gameServerId,
        string operationName,
        AuditAction? auditAction,
        Func<ICallOfDuty4xRconClient, CancellationToken, Task<string>> execute,
        OperationContext? operationContext,
        CancellationToken cancellationToken)
    {
        var contextResult = await TryGetCoD4xContext(gameServerId, cancellationToken).ConfigureAwait(false);
        if (contextResult.Error != null)
        {
            if (auditAction is not null)
            {
                var contextError = contextResult.Error.Result?.Errors?.FirstOrDefault();
                LogFailureAudit(
                    gameServerId,
                    "CallOfDuty4x",
                    operationName,
                    auditAction.Value,
                    operationContext,
                    contextError?.Code ?? ErrorCodes.RCON_OPERATION_FAILED,
                    contextError?.Message ?? "Failed to resolve CoD4x RCON context.");
            }

            return contextResult.Error;
        }

        var context = contextResult.Context!;
        var operation = telemetryClient.StartOperation<DependencyTelemetry>(operationName);
        operation.Telemetry.Type = $"{context.GameType}Server";
        operation.Telemetry.Target = $"{context.Hostname}:{context.QueryPort}";

        try
        {
            var commandResult = await execute(context.Client, cancellationToken).ConfigureAwait(false);

            if (auditAction is not null)
            {
                var auditEvent = AuditEvent.ServerAction(operationName, auditAction.Value)
                    .WithGameContext(context.GameType, context.GameServerId)
                    .WithSource("CoD4xRconController");

                if (!string.IsNullOrWhiteSpace(operationContext?.Target))
                {
                    auditEvent = auditEvent.WithTarget(operationContext.Target!, operationContext.TargetType);
                }

                if (operationContext != null)
                {
                    foreach (var property in operationContext.AuditProperties)
                    {
                        auditEvent = auditEvent.WithProperty(property.Key, property.Value);
                    }
                }

                auditLogger.LogAudit(auditEvent
                    .WithProperty("Result", commandResult)
                    .Build());

                var operatorData = operationContext == null
                    ? new Dictionary<string, object?>()
                    : new Dictionary<string, object?>(operationContext.OperatorData);
                operatorData["Result"] = commandResult;

                await TryWriteOperatorEventAsync(context.GameServerId, operationName, operatorData, cancellationToken).ConfigureAwait(false);
            }

            return new ApiResponse<string>(commandResult).ToApiResult();
        }
        catch (NotImplementedException ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            if (auditAction is not null)
            {
                LogFailureAudit(context.GameServerId, context.GameType, operationName, auditAction.Value, operationContext, ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The requested CoD4x operation is not implemented for this game server type.");
            }

            logger.LogWarning(ex, "{OperationName} is not implemented for game server {GameServerId}", operationName, gameServerId);
            return new ApiResponse<string>(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The requested CoD4x operation is not implemented for this game server type.")).ToApiResult();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = "Cancelled";
            throw;
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            if (auditAction is not null)
            {
                LogFailureAudit(context.GameServerId, context.GameType, operationName, auditAction.Value, operationContext, ErrorCodes.RCON_OPERATION_FAILED, "Failed to execute CoD4x command via RCON.");
            }

            logger.LogError(ex, "Failed to execute {OperationName} on game server {GameServerId}", operationName, gameServerId);
            return new ApiResponse<string>(new ApiError(ErrorCodes.RCON_OPERATION_FAILED, "Failed to execute CoD4x command via RCON."))
                .ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    private async Task TryWriteOperatorEventAsync(Guid gameServerId, string eventType, object data, CancellationToken cancellationToken = default)
    {
        var eventData = JsonSerializer.Serialize(data);
        var effectiveCancellationToken = cancellationToken == default
            ? HttpContext.RequestAborted
            : cancellationToken;

        try
        {
            await repositoryApiClient.GameServersEvents.V1
                .CreateGameServerEvent(new CreateGameServerEventDto(gameServerId, eventType, eventData), effectiveCancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to write {EventType} operator event for game server {GameServerId}",
                eventType,
                gameServerId);
        }
    }

    private void LogFailureAudit(
        Guid gameServerId,
        string gameType,
        string operationName,
        AuditAction auditAction,
        OperationContext? operationContext,
        string errorCode,
        string errorMessage)
    {
        var auditEvent = AuditEvent.ServerAction(operationName, auditAction)
            .WithGameContext(gameType, gameServerId)
            .WithSource("CoD4xRconController");

        if (!string.IsNullOrWhiteSpace(operationContext?.Target))
        {
            auditEvent = auditEvent.WithTarget(operationContext.Target!, operationContext.TargetType);
        }

        if (operationContext != null)
        {
            foreach (var property in operationContext.AuditProperties)
            {
                auditEvent = auditEvent.WithProperty(property.Key, property.Value);
            }
        }

        auditLogger.LogAudit(auditEvent
            .WithProperty("ErrorCode", errorCode)
            .WithProperty("ErrorMessage", errorMessage)
            .Build());
    }

    private async Task<(ApiResult<string>? Error, CoD4xContext? Context)> TryGetCoD4xContext(Guid gameServerId, CancellationToken cancellationToken)
    {
        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId, cancellationToken).ConfigureAwait(false);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return (new ApiResponse<string>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult(), null);
        }

        if (gameServerApiResponse.Result.Data.GameType != GameType.CallOfDuty4x)
        {
            logger.LogWarning("CoD4x operation requested for unsupported game type {GameType} on game server {GameServerId}", gameServerApiResponse.Result.Data.GameType, gameServerId);
            return (new ApiResponse<string>(new ApiError(ErrorCodes.OPERATION_NOT_SUPPORTED_FOR_GAME_TYPE, "This operation is only supported for CoD4x game servers.")).ToBadRequestResult(), null);
        }

        var rconConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon", cancellationToken).ConfigureAwait(false);
        var rconPassword = RconConfigResolver.ParsePasswordFromConfig(rconConfigResult?.Result?.Data?.Configuration);

        if (string.IsNullOrWhiteSpace(rconPassword))
        {
            return (new ApiResponse<string>(new ApiError(ErrorCodes.RCON_CREDENTIALS_MISSING, "The game server does not have RCON credentials configured.")).ToBadRequestResult(), null);
        }

        var rconClient = rconClientFactory.CreateInstance(
            gameServerApiResponse.Result.Data.GameType,
            gameServerApiResponse.Result.Data.GameServerId,
            gameServerApiResponse.Result.Data.Hostname,
            gameServerApiResponse.Result.Data.QueryPort,
            rconPassword);

        if (rconClient is not ICallOfDuty4xRconClient cod4xRconClient)
        {
            logger.LogWarning("CoD4x client is not implemented for game server {GameServerId}", gameServerId);
            return (new ApiResponse<string>(new ApiError(ErrorCodes.OPERATION_NOT_IMPLEMENTED, "The requested CoD4x operation is not implemented for this game server type.")).ToApiResult(), null);
        }

        return (null, new CoD4xContext(
            gameServerApiResponse.Result.Data.GameServerId,
            gameServerApiResponse.Result.Data.GameType.ToString(),
            gameServerApiResponse.Result.Data.Hostname,
            gameServerApiResponse.Result.Data.QueryPort,
            cod4xRconClient));
    }

    private sealed record CoD4xContext(Guid GameServerId, string GameType, string Hostname, int QueryPort, ICallOfDuty4xRconClient Client);

    private sealed record OperationContext(
        string? Target,
        string TargetType,
        IReadOnlyDictionary<string, string> AuditProperties,
        IReadOnlyDictionary<string, object?> OperatorData);

    private static OperationContext? BuildOperationContext<TRequest>(TRequest request)
        where TRequest : class
    {
        return request switch
        {
            CoD4xPermBanRequestDto dto => new OperationContext(
                dto.PlayerIdentifier,
                "Player",
                new Dictionary<string, string>(),
                new Dictionary<string, object?> { ["PlayerIdentifier"] = dto.PlayerIdentifier }),

            CoD4xTempBanRequestDto dto => new OperationContext(
                dto.PlayerIdentifier,
                "Player",
                new Dictionary<string, string> { ["DurationMinutes"] = dto.DurationMinutes.ToString() },
                new Dictionary<string, object?>
                {
                    ["PlayerIdentifier"] = dto.PlayerIdentifier,
                    ["DurationMinutes"] = dto.DurationMinutes
                }),

            CoD4xUnbanRequestDto dto => new OperationContext(
                dto.PlayerIdentifier,
                "Player",
                new Dictionary<string, string>(),
                new Dictionary<string, object?> { ["PlayerIdentifier"] = dto.PlayerIdentifier }),

            CoD4xTargetReasonRequestDto dto => new OperationContext(
                dto.Target,
                "Target",
                BuildAuditProperties(("Reason", TrimOrNull(dto.Reason))),
                BuildOperatorProperties(("Target", TrimOrNull(dto.Target)), ("Reason", TrimOrNull(dto.Reason)))),

            CoD4xClientReasonRequestDto dto => new OperationContext(
                dto.ClientId.ToString(),
                "ClientId",
                BuildAuditProperties(("Reason", TrimOrNull(dto.Reason))),
                BuildOperatorProperties(("ClientId", dto.ClientId), ("Reason", TrimOrNull(dto.Reason)))),

            CoD4xTargetRequestDto dto => new OperationContext(
                dto.Target,
                "Target",
                new Dictionary<string, string>(),
                BuildOperatorProperties(("Target", TrimOrNull(dto.Target)))),

            CoD4xMapRequestDto dto => new OperationContext(
                dto.MapName,
                "Map",
                new Dictionary<string, string>(),
                BuildOperatorProperties(("MapName", TrimOrNull(dto.MapName)))),

            CoD4xGametypeRequestDto dto => new OperationContext(
                dto.GameType,
                "Gametype",
                new Dictionary<string, string>(),
                BuildOperatorProperties(("GameType", TrimOrNull(dto.GameType)))),

            CoD4xRecordRequestDto dto => new OperationContext(
                dto.Target,
                "Target",
                BuildAuditProperties(("DemoName", TrimOrNull(dto.DemoName))),
                BuildOperatorProperties(("Target", TrimOrNull(dto.Target)), ("DemoName", TrimOrNull(dto.DemoName)))),

            CoD4xAdminAddAdminRequestDto dto => new OperationContext(
                dto.User,
                "User",
                new Dictionary<string, string> { ["Power"] = dto.Power.ToString() },
                BuildOperatorProperties(("User", TrimOrNull(dto.User)), ("Power", dto.Power))),

            CoD4xAdminUserRequestDto dto => new OperationContext(
                dto.User,
                "User",
                new Dictionary<string, string>(),
                BuildOperatorProperties(("User", TrimOrNull(dto.User)))),

            CoD4xAdminChangePasswordRequestDto dto => new OperationContext(
                dto.User,
                "User",
                new Dictionary<string, string>(),
                BuildOperatorProperties(("User", TrimOrNull(dto.User)))),

            CoD4xAdminChangeCommandPowerRequestDto dto => new OperationContext(
                dto.Command,
                "Command",
                new Dictionary<string, string> { ["MinPower"] = dto.MinPower.ToString() },
                BuildOperatorProperties(("Command", TrimOrNull(dto.Command)), ("MinPower", dto.MinPower))),

            CoD4xSetDvarRequestDto dto => new OperationContext(
                dto.DvarName,
                "Dvar",
                new Dictionary<string, string>(),
                BuildOperatorProperties(("DvarName", TrimOrNull(dto.DvarName)))),

            CoD4xPluginRequestDto dto => new OperationContext(
                dto.PluginName,
                "Plugin",
                new Dictionary<string, string>(),
                BuildOperatorProperties(("PluginName", TrimOrNull(dto.PluginName)))),

            CoD4xTargetMessageRequestDto dto => new OperationContext(
                dto.Target,
                "Target",
                BuildAuditProperties(("MessageLength", (dto.Message ?? string.Empty).Length.ToString())),
                BuildOperatorProperties(("Target", TrimOrNull(dto.Target)), ("MessageLength", (dto.Message ?? string.Empty).Length))),

            CoD4xMessageRequestDto dto => new OperationContext(
                null,
                "Target",
                BuildAuditProperties(("MessageLength", (dto.Message ?? string.Empty).Length.ToString())),
                BuildOperatorProperties(("MessageLength", (dto.Message ?? string.Empty).Length))),

            TakeScreenshotRequestDto dto => new OperationContext(
                dto.PlayerIdentifier,
                "Player",
                new Dictionary<string, string>(),
                BuildOperatorProperties(("PlayerIdentifier", TrimOrNull(dto.PlayerIdentifier)))),

            _ => null
        };
    }

    private static TRequest NormalizeRequest<TRequest>(TRequest request)
        where TRequest : class
    {
        object normalized = request switch
        {
            CoD4xPermBanRequestDto dto => dto with { PlayerIdentifier = TrimOrNull(dto.PlayerIdentifier) ?? dto.PlayerIdentifier },
            CoD4xTempBanRequestDto dto => dto with { PlayerIdentifier = TrimOrNull(dto.PlayerIdentifier) ?? dto.PlayerIdentifier },
            CoD4xUnbanRequestDto dto => dto with { PlayerIdentifier = TrimOrNull(dto.PlayerIdentifier) ?? dto.PlayerIdentifier },
            CoD4xTargetReasonRequestDto dto => dto with { Target = TrimOrNull(dto.Target), Reason = TrimOrNull(dto.Reason) },
            CoD4xClientReasonRequestDto dto => dto with { Reason = TrimOrNull(dto.Reason) },
            CoD4xTargetRequestDto dto => dto with { Target = TrimOrNull(dto.Target) },
            CoD4xMapRequestDto dto => dto with { MapName = TrimOrNull(dto.MapName) },
            CoD4xGametypeRequestDto dto => dto with { GameType = TrimOrNull(dto.GameType) },
            CoD4xRecordRequestDto dto => dto with { Target = TrimOrNull(dto.Target), DemoName = TrimOrNull(dto.DemoName) },
            CoD4xAdminAddAdminRequestDto dto => dto with { User = TrimOrNull(dto.User) },
            CoD4xAdminUserRequestDto dto => dto with { User = TrimOrNull(dto.User) },
            CoD4xAdminChangePasswordRequestDto dto => dto with { User = TrimOrNull(dto.User) },
            CoD4xAdminChangeCommandPowerRequestDto dto => dto with { Command = TrimOrNull(dto.Command) },
            CoD4xSetDvarRequestDto dto => dto with { DvarName = TrimOrNull(dto.DvarName) },
            CoD4xPluginRequestDto dto => dto with { PluginName = TrimOrNull(dto.PluginName) },
            CoD4xTargetMessageRequestDto dto => dto with { Target = TrimOrNull(dto.Target), Message = TrimOrNull(dto.Message) },
            CoD4xMessageRequestDto dto => dto with { Message = TrimOrNull(dto.Message) },
            TakeScreenshotRequestDto dto => dto with { PlayerIdentifier = TrimOrNull(dto.PlayerIdentifier) ?? dto.PlayerIdentifier },
            _ => request
        };

        return (TRequest)normalized;
    }

    private static IReadOnlyDictionary<string, string> BuildAuditProperties(params (string Key, string? Value)[] values)
    {
        var properties = new Dictionary<string, string>();
        foreach (var (key, value) in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                properties[key] = value;
            }
        }

        return properties;
    }

    private static IReadOnlyDictionary<string, object?> BuildOperatorProperties(params (string Key, object? Value)[] values)
    {
        var properties = new Dictionary<string, object?>();
        foreach (var (key, value) in values)
        {
            if (value is string stringValue)
            {
                if (!string.IsNullOrWhiteSpace(stringValue))
                {
                    properties[key] = stringValue;
                }

                continue;
            }

            if (value != null)
            {
                properties[key] = value;
            }
        }

        return properties;
    }

    private static string? TrimOrNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static ApiResult<string>? RequireCoD4xPermBan(CoD4xPermBanRequestDto request)
    {
        var playerIdentifier = TrimOrNull(request.PlayerIdentifier);
        if (playerIdentifier == null || !CoD4xPlayerIdentifierRegex.IsMatch(playerIdentifier))
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_PLAYER_IDENTIFIER, "Player identifier must be a numeric CoD4x identifier between 17 and 21 digits."))
                .ToBadRequestResult();
        }

        return null;
    }

    private static ApiResult<string>? RequireCoD4xTempBan(CoD4xTempBanRequestDto request)
    {
        var playerIdentifier = TrimOrNull(request.PlayerIdentifier);
        if (playerIdentifier == null || !CoD4xPlayerIdentifierRegex.IsMatch(playerIdentifier))
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_PLAYER_IDENTIFIER, "Player identifier must be a numeric CoD4x identifier between 17 and 21 digits."))
                .ToBadRequestResult();
        }

        if (request.DurationMinutes <= 0 || request.DurationMinutes > MaxCoD4xTempBanDurationMinutes)
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, $"Duration minutes must be between 1 and {MaxCoD4xTempBanDurationMinutes}."))
                .ToBadRequestResult();
        }

        return null;
    }

    private static ApiResult<string>? RequireCoD4xUnban(CoD4xUnbanRequestDto request)
    {
        var playerIdentifier = TrimOrNull(request.PlayerIdentifier);
        if (playerIdentifier == null || !CoD4xPlayerIdentifierRegex.IsMatch(playerIdentifier))
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_PLAYER_IDENTIFIER, "Player identifier must be a numeric CoD4x identifier between 17 and 21 digits."))
                .ToBadRequestResult();
        }

        return null;
    }

    private static ApiResult<string>? RequireTarget(CoD4xTargetRequestDto request)
    {
        var target = TrimOrNull(request.Target);
        if (target == null)
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, "Target cannot be null or empty.")).ToBadRequestResult();
        }

        return IsValidCommandTarget(target)
            ? null
            : InvalidTargetResult();
    }

    private static ApiResult<string>? RequireTargetAndReason(CoD4xTargetReasonRequestDto request)
    {
        var target = TrimOrNull(request.Target);
        var reason = TrimOrNull(request.Reason);

        if (target == null)
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, "Target cannot be null or empty.")).ToBadRequestResult();
        }

        if (!IsValidCommandTarget(target))
        {
            return InvalidTargetResult();
        }

        if (reason == null)
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, "Reason cannot be null or empty.")).ToBadRequestResult();
        }

        if (!IsValidCommandText(reason))
        {
            return InvalidRequestCharactersResult("Reason");
        }

        return null;
    }

    private static ApiResult<string>? RequireClientAndReason(CoD4xClientReasonRequestDto request)
    {
        var reason = TrimOrNull(request.Reason);
        if (request.ClientId <= 0)
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, "ClientId must be greater than zero.")).ToBadRequestResult();
        }

        if (reason == null)
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, "Reason cannot be null or empty.")).ToBadRequestResult();
        }

        if (!IsValidCommandText(reason))
        {
            return InvalidRequestCharactersResult("Reason");
        }

        return null;
    }

    private static ApiResult<string>? RequireMessage(CoD4xMessageRequestDto request)
    {
        var message = TrimOrNull(request.Message);
        if (message == null)
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, "Message cannot be null or empty.")).ToBadRequestResult();
        }

        return IsValidCommandText(message)
            ? null
            : InvalidRequestCharactersResult("Message");
    }

    private static ApiResult<string>? RequireTargetAndMessage(CoD4xTargetMessageRequestDto request)
    {
        var target = TrimOrNull(request.Target);
        var message = TrimOrNull(request.Message);

        if (target == null)
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, "Target cannot be null or empty.")).ToBadRequestResult();
        }

        if (!IsValidCommandTarget(target))
        {
            return InvalidTargetResult();
        }

        if (message == null)
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, "Message cannot be null or empty.")).ToBadRequestResult();
        }

        if (!IsValidCommandText(message))
        {
            return InvalidRequestCharactersResult("Message");
        }

        return null;
    }

    private static ApiResult<string>? RequireMap(CoD4xMapRequestDto request)
    {
        var mapName = TrimOrNull(request.MapName);
        if (mapName == null)
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, "MapName cannot be null or empty.")).ToBadRequestResult();
        }

        return IsValidCommandToken(mapName)
            ? null
            : InvalidRequestCharactersResult("MapName");
    }

    private static ApiResult<string>? RequireGametype(CoD4xGametypeRequestDto request)
    {
        var gameType = TrimOrNull(request.GameType);
        if (gameType == null)
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, "GameType cannot be null or empty.")).ToBadRequestResult();
        }

        return IsValidCommandToken(gameType)
            ? null
            : InvalidRequestCharactersResult("GameType");
    }

    private static ApiResult<string>? RequireRecordRequest(CoD4xRecordRequestDto request)
    {
        var target = TrimOrNull(request.Target);
        var demoName = TrimOrNull(request.DemoName);

        if (target == null)
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, "Target cannot be null or empty.")).ToBadRequestResult();
        }

        if (demoName != null && !IsValidCommandText(demoName))
        {
            return InvalidRequestCharactersResult("DemoName");
        }

        return IsValidCommandTarget(target)
            ? null
            : InvalidTargetResult();
    }

    private static bool IsValidCommandTarget(string target)
    {
        var normalized = target.Trim();

        // Keep backward compatibility for callers that send a wrapped quoted target,
        // while still rejecting nested quotes and command-separator characters.
        if (normalized.Length > 1 && normalized[0] == '"' && normalized[^1] == '"')
        {
            normalized = normalized[1..^1].Trim();
            if (string.IsNullOrWhiteSpace(normalized) || normalized.Contains('"'))
            {
                return false;
            }
        }

        return normalized.IndexOfAny(InvalidTargetCharacters) < 0;
    }

    private static bool IsValidCommandToken(string token)
    {
        if (token.Contains('"'))
        {
            return false;
        }

        return IsValidCommandTarget(token)
            && !token.Any(char.IsWhiteSpace);
    }

    private static bool IsValidCommandText(string value)
    {
        return value.IndexOfAny(InvalidCommandTextCharacters) < 0;
    }

    private static ApiResult<string> InvalidTargetResult()
    {
        return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, "Target contains unsupported characters.")).ToBadRequestResult();
    }

    private static ApiResult<string>? RequireAdminAdd(CoD4xAdminAddAdminRequestDto request)
    {
        var user = TrimOrNull(request.User);

        if (user == null)
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, "User cannot be null or empty.")).ToBadRequestResult();
        }

        if (!IsValidCommandTarget(user))
        {
            return InvalidRequestCharactersResult("User");
        }

        return request.Power < 0
            ? new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, "Power must be zero or greater.")).ToBadRequestResult()
            : null;
    }

    private static ApiResult<string>? RequireAdminUser(CoD4xAdminUserRequestDto request)
    {
        var user = TrimOrNull(request.User);
        if (user == null)
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, "User cannot be null or empty.")).ToBadRequestResult();
        }

        return IsValidCommandTarget(user)
            ? null
            : InvalidRequestCharactersResult("User");
    }

    private static ApiResult<string>? RequireAdminPasswordChange(CoD4xAdminChangePasswordRequestDto request)
    {
        var user = TrimOrNull(request.User);
        var newPassword = TrimOrNull(request.NewPassword);

        if (user == null)
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, "User cannot be null or empty.")).ToBadRequestResult();
        }

        if (!IsValidCommandTarget(user))
        {
            return InvalidRequestCharactersResult("User");
        }

        return newPassword == null
            ? new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, "NewPassword cannot be null or empty.")).ToBadRequestResult()
            : IsValidCommandText(newPassword)
                ? null
                : InvalidRequestCharactersResult("NewPassword");
    }

    private static ApiResult<string>? RequireCommandPowerChange(CoD4xAdminChangeCommandPowerRequestDto request)
    {
        var command = TrimOrNull(request.Command);

        if (command == null)
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, "Command cannot be null or empty.")).ToBadRequestResult();
        }

        if (!IsValidCommandTarget(command))
        {
            return InvalidRequestCharactersResult("Command");
        }

        if (!IsValidCommandToken(command))
        {
            return InvalidRequestCharactersResult("Command");
        }

        return request.MinPower < 0
            ? new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, "MinPower must be zero or greater.")).ToBadRequestResult()
            : null;
    }

    private static ApiResult<string>? RequireSetRequest(CoD4xSetDvarRequestDto request)
    {
        var dvarName = TrimOrNull(request.DvarName);
        var value = TrimOrNull(request.Value);

        if (dvarName == null)
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, "DvarName cannot be null or empty.")).ToBadRequestResult();
        }

        if (!IsValidCommandToken(dvarName))
        {
            return InvalidRequestCharactersResult("DvarName");
        }

        return value == null
            ? new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, "Value cannot be null or empty.")).ToBadRequestResult()
            : IsValidCommandText(value)
                ? null
                : InvalidRequestCharactersResult("Value");
    }

    private static ApiResult<string>? RequirePluginRequest(CoD4xPluginRequestDto request)
    {
        var pluginName = TrimOrNull(request.PluginName);
        if (pluginName == null)
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, "PluginName cannot be null or empty.")).ToBadRequestResult();
        }

        return IsValidCommandToken(pluginName)
            ? null
            : InvalidRequestCharactersResult("PluginName");
    }

    private static ApiResult<string>? RequirePlayerIdentifier(TakeScreenshotRequestDto request)
    {
        var playerIdentifier = TrimOrNull(request.PlayerIdentifier);

        if (playerIdentifier == null || !CoD4xPlayerIdentifierRegex.IsMatch(playerIdentifier))
        {
            return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_PLAYER_IDENTIFIER, "Player identifier must be a numeric CoD4x identifier between 17 and 21 digits."))
                .ToBadRequestResult();
        }

        return null;
    }

    private static ApiResult<string> InvalidRequestCharactersResult(string fieldName)
    {
        return new ApiResponse<string>(new ApiError(ErrorCodes.INVALID_REQUEST, $"{fieldName} contains unsupported characters.")).ToBadRequestResult();
    }
}
