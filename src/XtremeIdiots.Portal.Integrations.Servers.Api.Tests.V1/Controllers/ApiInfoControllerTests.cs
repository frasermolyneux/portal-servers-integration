using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Controllers.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Controllers;

[Trait("Category", "Unit")]
public class ApiInfoControllerTests
{
    [Fact]
    public void GetInfo_ReturnsOkResult()
    {
        var controller = new ApiInfoController();

        var result = controller.GetInfo();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
    }

    [Fact]
    public void GetInfo_ReturnsApiInfoDto()
    {
        var controller = new ApiInfoController();

        var result = controller.GetInfo();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var versionProperty = okResult.Value.GetType().GetProperty("Version");
        Assert.NotNull(versionProperty);

        var version = versionProperty.GetValue(okResult.Value) as string;
        Assert.NotNull(version);
        Assert.NotEqual("unknown", version);
    }

    [Fact]
    public void GetInfo_ReturnsBuildVersion()
    {
        var controller = new ApiInfoController();

        var result = controller.GetInfo();

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);

        var buildVersionProperty = okResult.Value.GetType().GetProperty("BuildVersion");
        Assert.NotNull(buildVersionProperty);

        var buildVersion = buildVersionProperty.GetValue(okResult.Value) as string;
        Assert.NotNull(buildVersion);
        Assert.DoesNotContain("+", buildVersion);
    }
}
