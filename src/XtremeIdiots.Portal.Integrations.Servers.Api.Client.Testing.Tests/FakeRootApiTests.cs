using System.Net;
using XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing.Tests;

[Trait("Category", "Unit")]
public class FakeRootApiTests
{
    [Fact]
    public async Task GetRoot_DefaultStatusCode_ReturnsSuccess()
    {
        var fakeApi = new FakeRootApi();

        var result = await fakeApi.GetRoot();

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GetRoot_WithErrorStatusCode_ReturnsError()
    {
        var fakeApi = new FakeRootApi();
        fakeApi.WithStatusCode(HttpStatusCode.ServiceUnavailable);

        var result = await fakeApi.GetRoot();

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Reset_RestoresDefaultStatusCode()
    {
        var fakeApi = new FakeRootApi();
        fakeApi.WithStatusCode(HttpStatusCode.InternalServerError);

        fakeApi.Reset();

        Assert.Equal(HttpStatusCode.OK, fakeApi.StatusCode);
    }

    [Fact]
    public void FluentApi_SupportsChainingCalls()
    {
        var fakeApi = new FakeRootApi();

        var result = fakeApi.WithStatusCode(HttpStatusCode.OK);

        Assert.Same(fakeApi, result);
    }
}
