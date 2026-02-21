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
        Assert.NotNull(provider.GetService<IVersionedRconApi>());
        Assert.NotNull(provider.GetService<IVersionedMapsApi>());
        Assert.NotNull(provider.GetService<IVersionedApiHealthApi>());
        Assert.NotNull(provider.GetService<IVersionedApiInfoApi>());
        Assert.NotNull(provider.GetService<IApiHealthApi>());
        Assert.NotNull(provider.GetService<IApiInfoApi>());
        Assert.NotNull(provider.GetService<IQueryApi>());
        Assert.NotNull(provider.GetService<IRconApi>());
        Assert.NotNull(provider.GetService<IMapsApi>());
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
        Assert.NotNull(client.Rcon);
        Assert.NotNull(client.Maps);
        Assert.NotNull(client.ApiHealth);
        Assert.NotNull(client.ApiInfo);
    }
}
