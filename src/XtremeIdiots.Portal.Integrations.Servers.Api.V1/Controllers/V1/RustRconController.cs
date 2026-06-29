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
[Route("v{version:apiVersion}/rcon/{gameServerId}/rust")]
public class RustRconController(
    ILogger<RustRconController> logger,
    IRepositoryApiClient repositoryApiClient,
    IRconClientFactory rconClientFactory,
    TelemetryClient telemetryClient,
    IAuditLogger auditLogger)
    : GameScopedRconControllerBase(logger, repositoryApiClient, rconClientFactory, telemetryClient, auditLogger, GameType.Rust, nameof(RustRconController))
{
    [HttpGet("current-map")]
    public Task<IActionResult> GetCurrentMap(Guid gameServerId) => GetCurrentMap(gameServerId, "RconRustCurrentMap", HttpContext.RequestAborted);

    [HttpPost("say")]
    public Task<IActionResult> Say(Guid gameServerId, [FromBody] SayRequest? request) => Say(gameServerId, request, "RconRustSay", HttpContext.RequestAborted);
}
