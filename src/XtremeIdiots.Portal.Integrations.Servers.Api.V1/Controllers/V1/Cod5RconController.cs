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
[Route("v{version:apiVersion}/rcon/{gameServerId}/cod5")]
public class Cod5RconController(
    ILogger<Cod5RconController> logger,
    IRepositoryApiClient repositoryApiClient,
    IRconClientFactory rconClientFactory,
    TelemetryClient telemetryClient,
    IAuditLogger auditLogger)
    : GameScopedRconControllerBase(logger, repositoryApiClient, rconClientFactory, telemetryClient, auditLogger, GameType.CallOfDuty5, nameof(Cod5RconController))
{
    [HttpGet("current-map")]
    public Task<IActionResult> GetCurrentMap(Guid gameServerId) => GetCurrentMap(gameServerId, "RconCod5CurrentMap", HttpContext.RequestAborted);

    [HttpPost("say")]
    public Task<IActionResult> Say(Guid gameServerId, [FromBody] SayRequest? request) => Say(gameServerId, request, "RconCod5Say", HttpContext.RequestAborted);

    [HttpPost("restart")]
    public Task<IActionResult> Restart(Guid gameServerId) => Restart(gameServerId, "RconCod5Restart", HttpContext.RequestAborted);

    [HttpPost("restart-map")]
    public Task<IActionResult> RestartMap(Guid gameServerId) => RestartMap(gameServerId, "RconCod5RestartMap", HttpContext.RequestAborted);

    [HttpPost("fast-restart-map")]
    public Task<IActionResult> FastRestartMap(Guid gameServerId) => FastRestartMap(gameServerId, "RconCod5FastRestartMap", HttpContext.RequestAborted);

    [HttpPost("next-map")]
    public Task<IActionResult> NextMap(Guid gameServerId) => NextMap(gameServerId, "RconCod5NextMap", HttpContext.RequestAborted);
}
