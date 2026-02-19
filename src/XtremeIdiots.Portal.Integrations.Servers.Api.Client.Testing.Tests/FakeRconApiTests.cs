using XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing.Tests;

[Trait("Category", "Unit")]
public class FakeRconApiTests
{
    private readonly FakeRconApi _fakeApi = new();

    [Fact]
    public async Task GetServerStatus_WithConfiguredResponse_ReturnsCannedData()
    {
        var serverId = Guid.NewGuid();
        var dto = ServersDtoFactory.CreateRconStatusResponse();
        _fakeApi.AddStatusResponse(serverId, dto);

        var result = await _fakeApi.GetServerStatus(serverId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Result!.Data!.Players);
    }

    [Fact]
    public async Task GetServerMaps_WithConfiguredResponse_ReturnsCannedData()
    {
        var serverId = Guid.NewGuid();
        var dto = ServersDtoFactory.CreateRconMapCollection();
        _fakeApi.AddMapsResponse(serverId, dto);

        var result = await _fakeApi.GetServerMaps(serverId);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GetCurrentMap_WithConfiguredResponse_ReturnsCannedData()
    {
        var serverId = Guid.NewGuid();
        var dto = ServersDtoFactory.CreateRconCurrentMap("mp_backlot");
        _fakeApi.AddCurrentMapResponse(serverId, dto);

        var result = await _fakeApi.GetCurrentMap(serverId);

        Assert.True(result.IsSuccess);
        Assert.Equal("mp_backlot", result.Result!.Data!.MapName);
    }

    [Fact]
    public async Task KickPlayer_LogsOperation()
    {
        var serverId = Guid.NewGuid();
        await _fakeApi.KickPlayer(serverId, 5);

        var log = Assert.Single(_fakeApi.OperationLog);
        Assert.Equal("KickPlayer", log.Operation);
        Assert.Equal(serverId, log.ServerId);
    }

    [Fact]
    public async Task BanPlayer_LogsOperation()
    {
        var serverId = Guid.NewGuid();
        await _fakeApi.BanPlayer(serverId, 3);

        var log = Assert.Single(_fakeApi.OperationLog);
        Assert.Equal("BanPlayer", log.Operation);
    }

    [Fact]
    public async Task Say_LogsOperation()
    {
        var serverId = Guid.NewGuid();
        await _fakeApi.Say(serverId, "Hello World");

        var log = Assert.Single(_fakeApi.OperationLog);
        Assert.Equal("Say", log.Operation);
    }

    [Fact]
    public async Task ChangeMap_LogsOperation()
    {
        var serverId = Guid.NewGuid();
        await _fakeApi.ChangeMap(serverId, "mp_crash");

        var log = Assert.Single(_fakeApi.OperationLog);
        Assert.Equal("ChangeMap", log.Operation);
    }

    [Fact]
    public async Task KickPlayerWithVerification_LogsOperation()
    {
        var serverId = Guid.NewGuid();
        await _fakeApi.KickPlayerWithVerification(serverId, 1, "TestPlayer");

        var log = Assert.Single(_fakeApi.OperationLog);
        Assert.Equal("KickPlayerWithVerification", log.Operation);
    }

    [Fact]
    public async Task MultipleOperations_AllTracked()
    {
        var serverId = Guid.NewGuid();
        await _fakeApi.KickPlayer(serverId, 1);
        await _fakeApi.BanPlayer(serverId, 2);
        await _fakeApi.Say(serverId, "test");

        Assert.Equal(3, _fakeApi.OperationLog.Count);
    }

    [Fact]
    public async Task WithErrorBehavior_OperationsReturnError()
    {
        _fakeApi.SetDefaultBehavior(DefaultBehavior.ReturnError);

        var result = await _fakeApi.KickPlayer(Guid.NewGuid(), 1);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        _fakeApi.AddStatusResponse(Guid.NewGuid(), ServersDtoFactory.CreateRconStatusResponse());
        _fakeApi.SetDefaultBehavior(DefaultBehavior.ReturnError);

        _fakeApi.Reset();

        Assert.Empty(_fakeApi.OperationLog);
        Assert.Equal(DefaultBehavior.ReturnGenericSuccess, _fakeApi.DefaultResponseBehavior);
    }

    [Fact]
    public void FluentApi_SupportsChainingCalls()
    {
        var result = _fakeApi
            .AddStatusResponse(Guid.NewGuid(), ServersDtoFactory.CreateRconStatusResponse())
            .SetDefaultBehavior(DefaultBehavior.ReturnError);

        Assert.Same(_fakeApi, result);
    }
}
