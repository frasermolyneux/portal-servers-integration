using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MX.Api.Abstractions;
using MX.Api.Client.Auth;
using MX.Api.Client.Configuration;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Maps;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;
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

    /// <summary>
    /// Regression test: registering caching expressions targeting two different sub-API interfaces
    /// (Cod4 Rcon and Maps) must not throw during registration or provider build. Prior to the
    /// SharedCacheConfiguration wiring, MX.Api.Client's per-typed-client scoping would throw
    /// <see cref="ArgumentException"/> during startup on the first sub-API whose declaring type did
    /// not match the expression.
    /// </summary>
    [Fact]
    public void ServersApiClient_CachingAcrossMultipleSubApis_DoesNotThrowAndResolvesAll()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddServersApiClient(options =>
        {
            options
                .WithBaseUrl("https://localhost")
                .WithEntraIdAuthentication("api://servers-tests")
                .WithCachePartition("unit-tests")
                .WithCaching(c => c
                    .NotCached<ICod4RconApi, Task<ApiResult<RconStatusResponseDto>>>(x => x.Status(Guid.Empty, default))
                    .NotCached<IMapsApi, Task<ApiResult<ServerMapsCollectionDto>>>(x => x.GetLoadedServerMapsFromHost(Guid.Empty)));
        });

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IQueryApi>());
        Assert.NotNull(provider.GetRequiredService<ICoD4xRconApi>());
        Assert.NotNull(provider.GetRequiredService<ICod2RconApi>());
        Assert.NotNull(provider.GetRequiredService<ICod4RconApi>());
        Assert.NotNull(provider.GetRequiredService<ICod5RconApi>());
        Assert.NotNull(provider.GetRequiredService<IInsurgencyRconApi>());
        Assert.NotNull(provider.GetRequiredService<IRustRconApi>());
        Assert.NotNull(provider.GetRequiredService<IL4d2RconApi>());
        Assert.NotNull(provider.GetRequiredService<IMapsApi>());
        Assert.NotNull(provider.GetRequiredService<IApiInfoApi>());
        Assert.NotNull(provider.GetRequiredService<IApiHealthApi>());
        Assert.NotNull(provider.GetRequiredService<IConfigApi>());
        Assert.NotNull(provider.GetRequiredService<IFileBrowseApi>());
        Assert.NotNull(provider.GetRequiredService<IFilesApi>());
        Assert.NotNull(provider.GetRequiredService<IServersApiClient>());
    }

    /// <summary>
    /// Typo-guard: a cache expression targeting an interface that is never registered as a typed
    /// sub-API must surface an <see cref="InvalidOperationException"/> from
    /// <c>SharedCacheConfiguration.ValidateAllOperationsMatched()</c> during registration, rather
    /// than silently no-op'ing.
    /// </summary>
    [Fact]
    public void ServersApiClient_CachingWithUnregisteredInterface_ThrowsInvalidOperation()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var thrown = Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddServersApiClient(options =>
            {
                options
                    .WithBaseUrl("https://localhost")
                    .WithCachePartition("unit-tests")
                    .WithCaching(c => c
                        .NotCached<IBogusUnregisteredApi, Task<string>>(x => x.DoThing()));
            });
        });

        Assert.NotNull(thrown);
    }

    /// <summary>
    /// No-caching path: registration without <c>WithCaching</c> must still resolve all typed
    /// sub-APIs and the unified client.
    /// </summary>
    [Fact]
    public void ServersApiClient_NoCachingConfigured_ResolvesAllSubApis()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddServersApiClient(options =>
        {
            options
                .WithBaseUrl("https://localhost")
                .WithEntraIdAuthentication("api://servers-tests");
        });

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IQueryApi>());
        Assert.NotNull(provider.GetRequiredService<ICoD4xRconApi>());
        Assert.NotNull(provider.GetRequiredService<ICod2RconApi>());
        Assert.NotNull(provider.GetRequiredService<ICod4RconApi>());
        Assert.NotNull(provider.GetRequiredService<ICod5RconApi>());
        Assert.NotNull(provider.GetRequiredService<IInsurgencyRconApi>());
        Assert.NotNull(provider.GetRequiredService<IRustRconApi>());
        Assert.NotNull(provider.GetRequiredService<IL4d2RconApi>());
        Assert.NotNull(provider.GetRequiredService<IMapsApi>());
        Assert.NotNull(provider.GetRequiredService<IApiInfoApi>());
        Assert.NotNull(provider.GetRequiredService<IApiHealthApi>());
        Assert.NotNull(provider.GetRequiredService<IConfigApi>());
        Assert.NotNull(provider.GetRequiredService<IFileBrowseApi>());
        Assert.NotNull(provider.GetRequiredService<IFilesApi>());
        Assert.NotNull(provider.GetRequiredService<IServersApiClient>());
    }

    // Deliberately unregistered interface used only by the typo-guard test above.
    public interface IBogusUnregisteredApi
    {
        Task<string> DoThing();
    }
}
