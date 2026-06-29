using Microsoft.Extensions.DependencyInjection;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing;
using XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing.Tests;

[Trait("Category", "Unit")]
public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddFakeServersApiClient_RegistersAllServices()
    {
        var services = new ServiceCollection();
        services.AddFakeServersApiClient();

        var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<IServersApiClient>());
        Assert.NotNull(provider.GetService<FakeServersApiClient>());
        Assert.NotNull(provider.GetService<IVersionedQueryApi>());
        Assert.NotNull(provider.GetService<IVersionedCoD4xRconApi>());
        Assert.NotNull(provider.GetService<IVersionedCod2RconApi>());
        Assert.NotNull(provider.GetService<IVersionedCod4RconApi>());
        Assert.NotNull(provider.GetService<IVersionedCod5RconApi>());
        Assert.NotNull(provider.GetService<IVersionedInsurgencyRconApi>());
        Assert.NotNull(provider.GetService<IVersionedRustRconApi>());
        Assert.NotNull(provider.GetService<IVersionedL4d2RconApi>());
        Assert.NotNull(provider.GetService<IVersionedMapsApi>());
        Assert.NotNull(provider.GetService<IVersionedApiHealthApi>());
        Assert.NotNull(provider.GetService<IVersionedApiInfoApi>());
        Assert.NotNull(provider.GetService<IVersionedConfigApi>());
        Assert.NotNull(provider.GetService<IVersionedFileBrowseApi>());
        Assert.NotNull(provider.GetService<IVersionedFilesApi>());
        Assert.NotNull(provider.GetService<IApiHealthApi>());
        Assert.NotNull(provider.GetService<IApiInfoApi>());
        Assert.NotNull(provider.GetService<IQueryApi>());
        Assert.NotNull(provider.GetService<ICoD4xRconApi>());
        Assert.NotNull(provider.GetService<ICod2RconApi>());
        Assert.NotNull(provider.GetService<ICod4RconApi>());
        Assert.NotNull(provider.GetService<ICod5RconApi>());
        Assert.NotNull(provider.GetService<IInsurgencyRconApi>());
        Assert.NotNull(provider.GetService<IRustRconApi>());
        Assert.NotNull(provider.GetService<IL4d2RconApi>());
        Assert.NotNull(provider.GetService<IMapsApi>());
        Assert.NotNull(provider.GetService<IConfigApi>());
        Assert.NotNull(provider.GetService<IFileBrowseApi>());
        Assert.NotNull(provider.GetService<IFilesApi>());
    }

    [Fact]
    public async Task AddFakeServersApiClient_WithConfiguration_AppliesConfig()
    {
        var serverId = Guid.NewGuid();
        var services = new ServiceCollection();
        services.AddFakeServersApiClient(fake =>
        {
            fake.FakeQuery.AddResponse(serverId, ServersDtoFactory.CreateQueryStatusResponse(serverName: "Configured Server"));
        });

        var provider = services.BuildServiceProvider();
        var fakeClient = provider.GetRequiredService<FakeServersApiClient>();

        var result = await fakeClient.FakeQuery.GetServerStatus(serverId);
        Assert.Equal("Configured Server", result.Result!.Data!.ServerName);
    }

    [Fact]
    public void AddFakeServersApiClient_WithoutConfiguration_WorksWithDefaults()
    {
        var services = new ServiceCollection();
        services.AddFakeServersApiClient();

        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IServersApiClient>();

        Assert.NotNull(client.Query);
        Assert.NotNull(client.CoD4xRcon);
        Assert.NotNull(client.Cod2Rcon);
        Assert.NotNull(client.Cod4Rcon);
        Assert.NotNull(client.Cod5Rcon);
        Assert.NotNull(client.InsurgencyRcon);
        Assert.NotNull(client.RustRcon);
        Assert.NotNull(client.L4d2Rcon);
        Assert.NotNull(client.Maps);
        Assert.NotNull(client.ApiHealth);
        Assert.NotNull(client.ApiInfo);
        Assert.NotNull(client.Config);
        Assert.NotNull(client.FileBrowse);
        Assert.NotNull(client.Files);
    }
}
