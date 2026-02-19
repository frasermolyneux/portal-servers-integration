using XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing.Tests;

[Trait("Category", "Unit")]
public class FakeMapsApiTests
{
    private readonly FakeMapsApi _fakeApi = new();

    [Fact]
    public async Task GetLoadedServerMapsFromHost_WithConfiguredResponse_ReturnsCannedData()
    {
        var serverId = Guid.NewGuid();
        var dto = ServersDtoFactory.CreateServerMapsCollection();
        _fakeApi.AddLoadedMapsResponse(serverId, dto);

        var result = await _fakeApi.GetLoadedServerMapsFromHost(serverId);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GetLoadedServerMapsFromHost_WithUnconfiguredServer_ReturnsDefaultSuccess()
    {
        var result = await _fakeApi.GetLoadedServerMapsFromHost(Guid.NewGuid());

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task PushServerMapToHost_LogsOperation()
    {
        var serverId = Guid.NewGuid();
        await _fakeApi.PushServerMapToHost(serverId, "mp_crash");

        var log = Assert.Single(_fakeApi.OperationLog);
        Assert.Equal("PushServerMapToHost", log.Operation);
        Assert.Equal(serverId, log.ServerId);
        Assert.Equal("mp_crash", log.MapName);
    }

    [Fact]
    public async Task DeleteServerMapFromHost_LogsOperation()
    {
        var serverId = Guid.NewGuid();
        await _fakeApi.DeleteServerMapFromHost(serverId, "mp_harbor");

        var log = Assert.Single(_fakeApi.OperationLog);
        Assert.Equal("DeleteServerMapFromHost", log.Operation);
        Assert.Equal("mp_harbor", log.MapName);
    }

    [Fact]
    public async Task WithErrorBehavior_OperationsReturnError()
    {
        _fakeApi.SetDefaultBehavior(DefaultBehavior.ReturnError);

        var result = await _fakeApi.PushServerMapToHost(Guid.NewGuid(), "mp_crash");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        _fakeApi.AddLoadedMapsResponse(Guid.NewGuid(), ServersDtoFactory.CreateServerMapsCollection());
        _fakeApi.SetDefaultBehavior(DefaultBehavior.ReturnError);

        _fakeApi.Reset();

        Assert.Empty(_fakeApi.OperationLog);
        Assert.Equal(DefaultBehavior.ReturnGenericSuccess, _fakeApi.DefaultResponseBehavior);
    }

    [Fact]
    public void FluentApi_SupportsChainingCalls()
    {
        var result = _fakeApi
            .AddLoadedMapsResponse(Guid.NewGuid(), ServersDtoFactory.CreateServerMapsCollection())
            .SetDefaultBehavior(DefaultBehavior.ReturnError);

        Assert.Same(_fakeApi, result);
    }
}
