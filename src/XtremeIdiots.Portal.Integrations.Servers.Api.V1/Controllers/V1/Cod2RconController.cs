using Asp.Versioning;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MX.Observability.ApplicationInsights.Auditing;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1;

[ApiController]
[Authorize(Roles = "ServiceAccount")]
[ApiVersion(ApiVersions.V1)]
[Route("v{version:apiVersion}/rcon/{gameServerId}/cod2")]
public class Cod2RconController(
    ILogger<Cod2RconController> logger,
    IRepositoryApiClient repositoryApiClient,
    IRconClientFactory rconClientFactory,
    TelemetryClient telemetryClient,
    IAuditLogger auditLogger)
    : GameScopedRconControllerBase(logger, repositoryApiClient, rconClientFactory, telemetryClient, auditLogger, GameType.CallOfDuty2, nameof(Cod2RconController))
{
    [HttpGet("current-map")]
    public Task<IActionResult> GetCurrentMap(Guid gameServerId) => GetCurrentMap(gameServerId, "RconCod2CurrentMap", HttpContext.RequestAborted);

    [HttpGet("status")]
    public Task<IActionResult> Status(Guid gameServerId) => Status(gameServerId, "RconCod2Status", HttpContext.RequestAborted);

    [HttpGet("maps")]
    public Task<IActionResult> GetMaps(Guid gameServerId) => GetMaps(gameServerId, "RconCod2Maps", HttpContext.RequestAborted);

    [HttpGet("server-info")]
    public Task<IActionResult> ServerInfo(Guid gameServerId) => ServerInfo(gameServerId, "RconCod2ServerInfo", HttpContext.RequestAborted);

    [HttpGet("system-info")]
    public Task<IActionResult> SystemInfo(Guid gameServerId) => SystemInfo(gameServerId, "RconCod2SystemInfo", HttpContext.RequestAborted);

    [HttpGet("cmdlist")]
    public Task<IActionResult> CmdList(Guid gameServerId) => CmdList(gameServerId, "RconCod2CmdList", HttpContext.RequestAborted);

    [HttpGet("cvarlist")]
    public Task<IActionResult> CvarList(Guid gameServerId) => CvarList(gameServerId, "RconCod2CvarList", HttpContext.RequestAborted);

    [HttpGet("dvarlist")]
    public Task<IActionResult> DvarList(Guid gameServerId) => DvarList(gameServerId, "RconCod2DvarList", HttpContext.RequestAborted);

    [HttpPost("say")]
    public Task<IActionResult> Say(Guid gameServerId, [FromBody] SayRequest? request) => Say(gameServerId, request, "RconCod2Say", HttpContext.RequestAborted);

    [HttpPost("tell")]
    public Task<IActionResult> Tell(Guid gameServerId, [FromBody] CoD4xTargetMessageRequestDto? request) => Tell(gameServerId, request, "RconCod2Tell", HttpContext.RequestAborted);

    [HttpPost("map")]
    public Task<IActionResult> Map(Guid gameServerId, [FromBody] ChangeMapRequest? request) => Map(gameServerId, request, "RconCod2Map", HttpContext.RequestAborted);

    [HttpPost("kick")]
    public Task<IActionResult> Kick(Guid gameServerId, [FromBody] ClientSlotRequest? request) => Kick(gameServerId, request, "RconCod2Kick", HttpContext.RequestAborted);

    [HttpPost("temp-ban")]
    public Task<IActionResult> TempBan(Guid gameServerId, [FromBody] ClientSlotRequest? request) => TempBan(gameServerId, request, "RconCod2TempBan", HttpContext.RequestAborted);

    [HttpPost("ban")]
    public Task<IActionResult> Ban(Guid gameServerId, [FromBody] ClientSlotRequest? request) => Ban(gameServerId, request, "RconCod2Ban", HttpContext.RequestAborted);

    [HttpPost("set")]
    public Task<IActionResult> Set(Guid gameServerId, [FromBody] SetDvarRequest? request) => Set(gameServerId, request, "RconCod2Set", HttpContext.RequestAborted);

    [HttpPost("seta")]
    public Task<IActionResult> Seta(Guid gameServerId, [FromBody] SetDvarRequest? request) => Seta(gameServerId, request, "RconCod2Seta", HttpContext.RequestAborted);

    [HttpPost("restart")]
    public Task<IActionResult> Restart(Guid gameServerId) => Restart(gameServerId, "RconCod2Restart", HttpContext.RequestAborted);

    [HttpPost("restart-map")]
    public Task<IActionResult> RestartMap(Guid gameServerId) => RestartMap(gameServerId, "RconCod2RestartMap", HttpContext.RequestAborted);

    [HttpPost("fast-restart-map")]
    public Task<IActionResult> FastRestartMap(Guid gameServerId) => FastRestartMap(gameServerId, "RconCod2FastRestartMap", HttpContext.RequestAborted);

    [HttpPost("next-map")]
    public Task<IActionResult> NextMap(Guid gameServerId) => NextMap(gameServerId, "RconCod2NextMap", HttpContext.RequestAborted);
}
