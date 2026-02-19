using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Moq;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Controllers.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Controllers;

[Trait("Category", "Unit")]
public class HealthControllerTests
{
    [Fact]
    public async Task GetHealth_WhenHealthy_Returns200()
    {
        var mockHealthCheckService = new Mock<HealthCheckService>();
        mockHealthCheckService
            .Setup(x => x.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HealthReport(
                new Dictionary<string, HealthReportEntry>
                {
                    ["test"] = new HealthReportEntry(HealthStatus.Healthy, "All good", TimeSpan.FromMilliseconds(10), null, null)
                },
                TimeSpan.FromMilliseconds(10)));

        var controller = new HealthController(mockHealthCheckService.Object);

        var result = await controller.GetHealth(CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetHealth_WhenUnhealthy_Returns503()
    {
        var mockHealthCheckService = new Mock<HealthCheckService>();
        mockHealthCheckService
            .Setup(x => x.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HealthReport(
                new Dictionary<string, HealthReportEntry>
                {
                    ["test"] = new HealthReportEntry(HealthStatus.Unhealthy, "Failed", TimeSpan.FromMilliseconds(10), null, null)
                },
                TimeSpan.FromMilliseconds(10)));

        var controller = new HealthController(mockHealthCheckService.Object);

        var result = await controller.GetHealth(CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetHealth_WhenDegraded_Returns503()
    {
        var mockHealthCheckService = new Mock<HealthCheckService>();
        mockHealthCheckService
            .Setup(x => x.CheckHealthAsync(It.IsAny<Func<HealthCheckRegistration, bool>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HealthReport(
                new Dictionary<string, HealthReportEntry>
                {
                    ["test"] = new HealthReportEntry(HealthStatus.Degraded, "Degraded", TimeSpan.FromMilliseconds(10), null, null)
                },
                TimeSpan.FromMilliseconds(10)));

        var controller = new HealthController(mockHealthCheckService.Object);

        var result = await controller.GetHealth(CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, objectResult.StatusCode);
    }
}
