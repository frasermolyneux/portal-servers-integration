using System.Net;
using XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing.Tests;

[Trait("Category", "Unit")]
public class FakeApiInfoApiTests
{
    [Fact]
    public async Task GetApiInfo_DefaultStatusCode_ReturnsSuccess()
    {
        var fakeApi = new FakeApiInfoApi();

        var result = await fakeApi.GetApiInfo();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GetApiInfo_WithErrorStatusCode_ReturnsError()
    {
        var fakeApi = new FakeApiInfoApi();
        fakeApi.WithStatusCode(HttpStatusCode.ServiceUnavailable);

        var result = await fakeApi.GetApiInfo();

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Reset_RestoresDefaultStatusCode()
    {
        var fakeApi = new FakeApiInfoApi();
        fakeApi.WithStatusCode(HttpStatusCode.InternalServerError);

        fakeApi.Reset();

        Assert.Equal(HttpStatusCode.OK, fakeApi.StatusCode);
    }

    [Fact]
    public void FluentApi_SupportsChainingCalls()
    {
        var fakeApi = new FakeApiInfoApi();

        var result = fakeApi.WithStatusCode(HttpStatusCode.OK);

        Assert.Same(fakeApi, result);
    }
}
