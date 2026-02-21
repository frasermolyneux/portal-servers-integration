using System.Net;
using XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing.Tests;

[Trait("Category", "Unit")]
public class FakeApiHealthApiTests
{
    [Fact]
    public async Task CheckHealth_DefaultStatusCode_ReturnsSuccess()
    {
        var fakeApi = new FakeApiHealthApi();

        var result = await fakeApi.CheckHealth();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CheckHealth_WithErrorStatusCode_ReturnsError()
    {
        var fakeApi = new FakeApiHealthApi();
        fakeApi.WithStatusCode(HttpStatusCode.ServiceUnavailable);

        var result = await fakeApi.CheckHealth();

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Reset_RestoresDefaultStatusCode()
    {
        var fakeApi = new FakeApiHealthApi();
        fakeApi.WithStatusCode(HttpStatusCode.InternalServerError);

        fakeApi.Reset();

        Assert.Equal(HttpStatusCode.OK, fakeApi.StatusCode);
    }

    [Fact]
    public void FluentApi_SupportsChainingCalls()
    {
        var fakeApi = new FakeApiHealthApi();

        var result = fakeApi.WithStatusCode(HttpStatusCode.OK);

        Assert.Same(fakeApi, result);
    }
}
