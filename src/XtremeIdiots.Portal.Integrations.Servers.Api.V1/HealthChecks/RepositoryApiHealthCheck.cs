using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.HealthChecks;

public class RepositoryApiHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    public RepositoryApiHealthCheck(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = _configuration["RepositoryApi:BaseUrl"];

        if (string.IsNullOrWhiteSpace(baseUrl))
            return HealthCheckResult.Unhealthy("RepositoryApi:BaseUrl is not configured");

        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var response = await httpClient.GetAsync($"{baseUrl.TrimEnd('/')}/v1.0/info", cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Repository API is reachable")
                : HealthCheckResult.Unhealthy($"Repository API returned {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Repository API is unreachable", ex);
        }
    }
}
