using Microsoft.Extensions.Diagnostics.HealthChecks;
using XtremeIdiots.Portal.Repository.Abstractions.Interfaces.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.HealthChecks;

public class RepositoryApiHealthCheck : IHealthCheck
{
    private readonly IApiHealthApi _apiHealthApi;

    public RepositoryApiHealthCheck(IApiHealthApi apiHealthApi)
    {
        _apiHealthApi = apiHealthApi;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _apiHealthApi.CheckHealth(cancellationToken);

            if (result.IsSuccess)
            {
                return HealthCheckResult.Healthy("Repository API is reachable");
            }

            return HealthCheckResult.Unhealthy($"Repository API returned {result.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Repository API is unreachable", ex);
        }
    }
}
