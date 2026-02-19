using System.Net;
using XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing.Tests;

[Trait("Category", "Unit")]
public class FakeQueryApiTests
{
    private readonly FakeQueryApi _fakeApi = new();

    [Fact]
    public async Task GetServerStatus_WithConfiguredResponse_ReturnsCannedData()
    {
        var serverId = Guid.NewGuid();
        var dto = ServersDtoFactory.CreateQueryStatusResponse(serverName: "My Server", map: "mp_harbor");

        _fakeApi.AddResponse(serverId, dto);

        var result = await _fakeApi.GetServerStatus(serverId);

        Assert.True(result.IsSuccess);
        Assert.Equal("My Server", result.Result!.Data!.ServerName);
        Assert.Equal("mp_harbor", result.Result.Data.Map);
    }

    [Fact]
    public async Task GetServerStatus_WithUnconfiguredServerId_ReturnsDefaultSuccess()
    {
        var result = await _fakeApi.GetServerStatus(Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Result?.Data);
    }

    [Fact]
    public async Task GetServerStatus_WithErrorBehavior_ReturnsError()
    {
        _fakeApi.SetDefaultBehavior(DefaultBehavior.ReturnError);

        var result = await _fakeApi.GetServerStatus(Guid.NewGuid());

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task GetServerStatus_WithConfiguredError_ReturnsSpecificError()
    {
        var serverId = Guid.NewGuid();
        _fakeApi.AddErrorResponse(serverId, HttpStatusCode.ServiceUnavailable, "OFFLINE", "Server is offline");

        var result = await _fakeApi.GetServerStatus(serverId);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task GetServerStatus_TracksQueriedServerIds()
    {
        var serverId1 = Guid.NewGuid();
        var serverId2 = Guid.NewGuid();

        await _fakeApi.GetServerStatus(serverId1);
        await _fakeApi.GetServerStatus(serverId2);
        await _fakeApi.GetServerStatus(serverId1);

        Assert.Equal(3, _fakeApi.QueriedServerIds.Count);
        Assert.Contains(serverId1, _fakeApi.QueriedServerIds);
        Assert.Contains(serverId2, _fakeApi.QueriedServerIds);
    }

    [Fact]
    public async Task GetServerStatus_ErrorResponseTakesPrecedenceOverConfiguredResponse()
    {
        var serverId = Guid.NewGuid();
        _fakeApi.AddResponse(serverId, ServersDtoFactory.CreateQueryStatusResponse());
        _fakeApi.AddErrorResponse(serverId, HttpStatusCode.InternalServerError, "ERR", "Error");

        var result = await _fakeApi.GetServerStatus(serverId);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        var serverId = Guid.NewGuid();
        _fakeApi.AddResponse(serverId, ServersDtoFactory.CreateQueryStatusResponse());
        _fakeApi.SetDefaultBehavior(DefaultBehavior.ReturnError);

        _fakeApi.Reset();

        Assert.Empty(_fakeApi.QueriedServerIds);
        Assert.Equal(DefaultBehavior.ReturnGenericSuccess, _fakeApi.DefaultResponseBehavior);
    }

    [Fact]
    public void FluentApi_SupportsChainingCalls()
    {
        var serverId = Guid.NewGuid();
        var result = _fakeApi
            .AddResponse(serverId, ServersDtoFactory.CreateQueryStatusResponse())
            .SetDefaultBehavior(DefaultBehavior.ReturnError);

        Assert.Same(_fakeApi, result);
    }
}
