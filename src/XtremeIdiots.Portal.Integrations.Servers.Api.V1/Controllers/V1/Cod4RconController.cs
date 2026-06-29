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
[Route("v{version:apiVersion}/rcon/{gameServerId}/cod4")]
public class Cod4RconController(
    ILogger<Cod4RconController> logger,
    IRepositoryApiClient repositoryApiClient,
    IRconClientFactory rconClientFactory,
    TelemetryClient telemetryClient,
    IAuditLogger auditLogger)
    : GameScopedRconControllerBase(logger, repositoryApiClient, rconClientFactory, telemetryClient, auditLogger, GameType.CallOfDuty4, nameof(Cod4RconController))
{
    [HttpGet("current-map")]
    public Task<IActionResult> GetCurrentMap(Guid gameServerId) => GetCurrentMap(gameServerId, "RconCod4CurrentMap", HttpContext.RequestAborted);

    [HttpPost("say")]
    public Task<IActionResult> Say(Guid gameServerId, [FromBody] SayRequest? request) => Say(gameServerId, request, "RconCod4Say", HttpContext.RequestAborted);

    [HttpPost("restart")]
    public Task<IActionResult> Restart(Guid gameServerId) => Restart(gameServerId, "RconCod4Restart", HttpContext.RequestAborted);

    [HttpPost("restart-map")]
    public Task<IActionResult> RestartMap(Guid gameServerId) => RestartMap(gameServerId, "RconCod4RestartMap", HttpContext.RequestAborted);

    [HttpPost("fast-restart-map")]
    public Task<IActionResult> FastRestartMap(Guid gameServerId) => FastRestartMap(gameServerId, "RconCod4FastRestartMap", HttpContext.RequestAborted);

    [HttpPost("next-map")]
    public Task<IActionResult> NextMap(Guid gameServerId) => NextMap(gameServerId, "RconCod4NextMap", HttpContext.RequestAborted);
}
