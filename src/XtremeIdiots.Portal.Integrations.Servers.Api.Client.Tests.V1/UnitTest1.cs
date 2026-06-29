using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MX.Api.Client.Auth;
using MX.Api.Client.Configuration;
using System.Net;
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
        Assert.NotNull(client.ApiHealth);
        Assert.NotNull(client.ApiInfo);
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
    public async Task LegacyServersApiClient_DefaultCoD4xSelector_ReturnsApiResultFailure()
    {
        IServersApiClient client = new LegacyServersApiClient();

        var response = await client.CoD4xRcon.V1.Status(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("OPERATION_NOT_IMPLEMENTED", response.Result?.Errors?.FirstOrDefault()?.Code);
    }

    [Fact]
    public async Task ServersApiClient_LegacyConstructor_UsesNotSupportedCoD4xFallback()
    {
        var query = new Mock<IVersionedQueryApi>().Object;
        var rcon = new Mock<IVersionedRconApi>().Object;
        var maps = new Mock<IVersionedMapsApi>().Object;
        var apiHealth = new Mock<IVersionedApiHealthApi>().Object;
        var apiInfo = new Mock<IVersionedApiInfoApi>().Object;
        var config = new Mock<IVersionedConfigApi>().Object;
        var fileBrowse = new Mock<IVersionedFileBrowseApi>().Object;

        var client = new ServersApiClient(query, rcon, maps, apiHealth, apiInfo, config, fileBrowse);

        var response = await client.CoD4xRcon.V1.Status(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("OPERATION_NOT_IMPLEMENTED", response.Result?.Errors?.FirstOrDefault()?.Code);
    }

    private sealed class LegacyServersApiClient : IServersApiClient
    {
        public IVersionedQueryApi Query => throw new NotImplementedException();
        public IVersionedRconApi Rcon => throw new NotImplementedException();
        public IVersionedMapsApi Maps => throw new NotImplementedException();
        public IVersionedApiHealthApi ApiHealth => throw new NotImplementedException();
        public IVersionedApiInfoApi ApiInfo => throw new NotImplementedException();
        public IVersionedConfigApi Config => throw new NotImplementedException();
        public IVersionedFileBrowseApi FileBrowse => throw new NotImplementedException();
    }
}
