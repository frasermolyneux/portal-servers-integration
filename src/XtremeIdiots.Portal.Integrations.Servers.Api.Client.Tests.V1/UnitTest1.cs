using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MX.Api.Client.Auth;
using MX.Api.Client.Configuration;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Tests.V1;

[Trait("Category", "Unit")]
public class ServersApiClientDITests
{
    [Fact]
    public void ServersApiClient_CanBeResolvedFromDI_Successfully()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServersApiClient(options =>
        {
            options.WithBaseUrl("https://localhost");
        });

        var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IServersApiClient>();

        Assert.NotNull(client);
        Assert.NotNull(client.Query);
        Assert.NotNull(client.Rcon);
        Assert.NotNull(client.Maps);
        Assert.NotNull(client.Root);
    }

    [Fact]
    public void ServersApiClient_QueryApi_CanBeResolved()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServersApiClient(options =>
        {
            options.WithBaseUrl("https://localhost");
        });

        var provider = services.BuildServiceProvider();
        var queryApi = provider.GetRequiredService<IVersionedQueryApi>();

        Assert.NotNull(queryApi);
        Assert.NotNull(queryApi.V1);
    }

    [Fact]
    public void ServersApiClient_RconApi_CanBeResolved()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServersApiClient(options =>
        {
            options.WithBaseUrl("https://localhost");
        });

        var provider = services.BuildServiceProvider();
        var rconApi = provider.GetRequiredService<IVersionedRconApi>();

        Assert.NotNull(rconApi);
        Assert.NotNull(rconApi.V1);
    }

    [Fact]
    public void ServersApiClient_MapsApi_CanBeResolved()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServersApiClient(options =>
        {
            options.WithBaseUrl("https://localhost");
        });

        var provider = services.BuildServiceProvider();
        var mapsApi = provider.GetRequiredService<IVersionedMapsApi>();

        Assert.NotNull(mapsApi);
        Assert.NotNull(mapsApi.V1);
    }

    [Fact]
    public void ServersApiClient_RootApi_CanBeResolved()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServersApiClient(options =>
        {
            options.WithBaseUrl("https://localhost");
        });

        var provider = services.BuildServiceProvider();
        var rootApi = provider.GetRequiredService<IVersionedRootApi>();

        Assert.NotNull(rootApi);
        Assert.NotNull(rootApi.V1);
    }
}
