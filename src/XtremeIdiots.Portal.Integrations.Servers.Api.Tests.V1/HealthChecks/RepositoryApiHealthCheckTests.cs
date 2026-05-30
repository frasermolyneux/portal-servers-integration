using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.HealthChecks;
using XtremeIdiots.Portal.Repository.Abstractions.Interfaces.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.HealthChecks;

[Trait("Category", "Unit")]
public class RepositoryApiHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_WhenRequestIsCanceled_ThrowsOperationCanceledException()
    {
        var mockApiHealthApi = new Mock<IApiHealthApi>();
        mockApiHealthApi
            .Setup(x => x.CheckHealth(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("Request canceled"));

        var healthCheck = new RepositoryApiHealthCheck(mockApiHealthApi.Object);
        var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            healthCheck.CheckHealthAsync(new HealthCheckContext(), cancellationTokenSource.Token));
    }

    [Fact]
    public async Task CheckHealthAsync_WhenRepositoryCallFails_ReturnsUnhealthyWithException()
    {
        var mockApiHealthApi = new Mock<IApiHealthApi>();
        var exception = new HttpRequestException("Repository unavailable");
        mockApiHealthApi
            .Setup(x => x.CheckHealth(It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        var healthCheck = new RepositoryApiHealthCheck(mockApiHealthApi.Object);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal("Repository API is unreachable", result.Description);
        Assert.Same(exception, result.Exception);
    }
}
