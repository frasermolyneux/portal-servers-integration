using XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing;
using XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing.Tests;

[Trait("Category", "Unit")]
public class FakeServersApiClientTests
{
    [Fact]
    public void ImplementsIServersApiClient()
    {
        var fake = new FakeServersApiClient();
        Assert.IsAssignableFrom<IServersApiClient>(fake);
    }

    [Fact]
    public void Query_DelegatesToFakeQuery()
    {
        var fake = new FakeServersApiClient();
        Assert.Same(fake.FakeQuery, fake.Query.V1);
    }

    [Fact]
    public void Rcon_DelegatesToFakeRcon()
    {
        var fake = new FakeServersApiClient();
        Assert.Same(fake.FakeRcon, fake.Rcon.V1);
    }

    [Fact]
    public void Maps_DelegatesToFakeMaps()
    {
        var fake = new FakeServersApiClient();
        Assert.Same(fake.FakeMaps, fake.Maps.V1);
    }

    [Fact]
    public void ApiHealth_DelegatesToFakeApiHealth()
    {
        var fake = new FakeServersApiClient();
        Assert.Same(fake.FakeApiHealth, fake.ApiHealth.V1);
    }

    [Fact]
    public void ApiInfo_DelegatesToFakeApiInfo()
    {
        var fake = new FakeServersApiClient();
        Assert.Same(fake.FakeApiInfo, fake.ApiInfo.V1);
    }

    [Fact]
    public void Reset_ClearsAllFakeState()
    {
        var fake = new FakeServersApiClient();
        var serverId = Guid.NewGuid();

        fake.FakeQuery.AddResponse(serverId, ServersDtoFactory.CreateQueryStatusResponse());
        fake.FakeRcon.AddStatusResponse(serverId, ServersDtoFactory.CreateRconStatusResponse());
        fake.FakeMaps.AddLoadedMapsResponse(serverId, ServersDtoFactory.CreateServerMapsCollection());

        fake.Reset();

        Assert.Empty(fake.FakeQuery.QueriedServerIds);
        Assert.Empty(fake.FakeRcon.OperationLog);
        Assert.Empty(fake.FakeMaps.OperationLog);
    }
}
