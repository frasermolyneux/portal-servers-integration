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
    public void ServersApiClient_Cod2RconApi_CanBeResolved()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServersApiClient(options =>
        {
            options.WithBaseUrl("https://localhost");
        });

        var provider = services.BuildServiceProvider();
        var api = provider.GetRequiredService<IVersionedCod2RconApi>();

        Assert.NotNull(api);
        Assert.NotNull(api.V1);
    }

    [Fact]
    public void ServersApiClient_Cod4RconApi_CanBeResolved()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServersApiClient(options =>
        {
            options.WithBaseUrl("https://localhost");
        });

        var provider = services.BuildServiceProvider();
        var api = provider.GetRequiredService<IVersionedCod4RconApi>();

        Assert.NotNull(api);
        Assert.NotNull(api.V1);
    }

    [Fact]
    public void ServersApiClient_Cod5RconApi_CanBeResolved()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServersApiClient(options =>
        {
            options.WithBaseUrl("https://localhost");
        });

        var provider = services.BuildServiceProvider();
        var api = provider.GetRequiredService<IVersionedCod5RconApi>();

        Assert.NotNull(api);
        Assert.NotNull(api.V1);
    }

    [Fact]
    public void ServersApiClient_InsurgencyRconApi_CanBeResolved()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServersApiClient(options =>
        {
            options.WithBaseUrl("https://localhost");
        });

        var provider = services.BuildServiceProvider();
        var api = provider.GetRequiredService<IVersionedInsurgencyRconApi>();

        Assert.NotNull(api);
        Assert.NotNull(api.V1);
    }

    [Fact]
    public void ServersApiClient_RustRconApi_CanBeResolved()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServersApiClient(options =>
        {
            options.WithBaseUrl("https://localhost");
        });

        var provider = services.BuildServiceProvider();
        var api = provider.GetRequiredService<IVersionedRustRconApi>();

        Assert.NotNull(api);
        Assert.NotNull(api.V1);
    }

    [Fact]
    public void ServersApiClient_L4d2RconApi_CanBeResolved()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServersApiClient(options =>
        {
            options.WithBaseUrl("https://localhost");
        });

        var provider = services.BuildServiceProvider();
        var api = provider.GetRequiredService<IVersionedL4d2RconApi>();

        Assert.NotNull(api);
        Assert.NotNull(api.V1);
    }

    [Fact]
    public void ServersApiClient_ApiHealthApi_CanBeResolved()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServersApiClient(options =>
        {
            options.WithBaseUrl("https://localhost");
        });

        var provider = services.BuildServiceProvider();
        var apiHealth = provider.GetRequiredService<IVersionedApiHealthApi>();

        Assert.NotNull(apiHealth);
        Assert.NotNull(apiHealth.V1);
    }

    [Fact]
    public void ServersApiClient_ApiInfoApi_CanBeResolved()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServersApiClient(options =>
        {
            options.WithBaseUrl("https://localhost");
        });

        var provider = services.BuildServiceProvider();
        var apiInfo = provider.GetRequiredService<IVersionedApiInfoApi>();

        Assert.NotNull(apiInfo);
        Assert.NotNull(apiInfo.V1);
    }

    [Fact]
    public void ServersApiClient_FilesApi_CanBeResolved()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddServersApiClient(options =>
        {
            options.WithBaseUrl("https://localhost");
        });

        var provider = services.BuildServiceProvider();
        var filesApi = provider.GetRequiredService<IVersionedFilesApi>();

        Assert.NotNull(filesApi);
        Assert.NotNull(filesApi.V1);
    }
}
