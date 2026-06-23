using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Controllers.V1;

[ApiController]
[AllowAnonymous]
[ApiVersion("1.0")]
[Route("v{version:apiVersion}/health")]
public class HealthController : ControllerBase
{
    private readonly HealthCheckService _healthCheckService;

    public HealthController(HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    [HttpGet("ready")]
    public async Task<IActionResult> GetReady(CancellationToken cancellationToken)
    {
        var result = await _healthCheckService.CheckHealthAsync(cancellationToken);

        var statusCode = result.Status == HealthStatus.Healthy
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable;

        return StatusCode(statusCode, new
        {
            status = result.Status.ToString(),
            checks = result.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description
            })
        });
    }

    [HttpGet("live")]
    public IActionResult GetLive()
    {
        return Ok(new
        {
            status = HealthStatus.Healthy.ToString(),
        });
    }
}
