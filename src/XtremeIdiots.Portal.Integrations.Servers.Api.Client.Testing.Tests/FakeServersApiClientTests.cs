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
    public void Cod2Rcon_DelegatesToFakeCod2Rcon()
    {
        var fake = new FakeServersApiClient();
        Assert.Same(fake.FakeCod2Rcon, fake.Cod2Rcon.V1);
    }

    [Fact]
    public void Cod4Rcon_DelegatesToFakeCod4Rcon()
    {
        var fake = new FakeServersApiClient();
        Assert.Same(fake.FakeCod4Rcon, fake.Cod4Rcon.V1);
    }

    [Fact]
    public void Cod5Rcon_DelegatesToFakeCod5Rcon()
    {
        var fake = new FakeServersApiClient();
        Assert.Same(fake.FakeCod5Rcon, fake.Cod5Rcon.V1);
    }

    [Fact]
    public void InsurgencyRcon_DelegatesToFakeInsurgencyRcon()
    {
        var fake = new FakeServersApiClient();
        Assert.Same(fake.FakeInsurgencyRcon, fake.InsurgencyRcon.V1);
    }

    [Fact]
    public void RustRcon_DelegatesToFakeRustRcon()
    {
        var fake = new FakeServersApiClient();
        Assert.Same(fake.FakeRustRcon, fake.RustRcon.V1);
    }

    [Fact]
    public void L4d2Rcon_DelegatesToFakeL4d2Rcon()
    {
        var fake = new FakeServersApiClient();
        Assert.Same(fake.FakeL4d2Rcon, fake.L4d2Rcon.V1);
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
    public void Config_DelegatesToFakeConfig()
    {
        var fake = new FakeServersApiClient();
        Assert.Same(fake.FakeConfig, fake.Config.V1);
    }

    [Fact]
    public void FileBrowse_DelegatesToSharedBrowseFake()
    {
        var fake = new FakeServersApiClient();
        Assert.Same(fake.FakeFileBrowse, fake.FileBrowse.V1);
    }

    [Fact]
    public void Files_DelegatesToFakeFiles()
    {
        var fake = new FakeServersApiClient();
        Assert.Same(fake.FakeFiles, fake.Files.V1);
    }

    [Fact]
    public void Reset_ClearsAllFakeState()
    {
        var fake = new FakeServersApiClient();
        var serverId = Guid.NewGuid();

        fake.FakeQuery.AddResponse(serverId, ServersDtoFactory.CreateQueryStatusResponse());
        fake.FakeMaps.AddLoadedMapsResponse(serverId, ServersDtoFactory.CreateServerMapsCollection());
        fake.FakeFiles.AddListEntriesResponse(serverId, "/", new("/", null, []));

        fake.Reset();

        Assert.Empty(fake.FakeQuery.QueriedServerIds);
        Assert.Empty(fake.FakeMaps.OperationLog);
        Assert.Empty(fake.FakeFiles.OperationLog);
    }
}
