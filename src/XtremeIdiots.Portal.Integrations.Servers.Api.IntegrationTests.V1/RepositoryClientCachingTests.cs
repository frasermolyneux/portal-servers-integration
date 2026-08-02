using Microsoft.Extensions.DependencyInjection;
using MX.Api.Client.Caching;
using XtremeIdiots.Portal.Repository.Abstractions.Interfaces.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.IntegrationTests.V1;

/// <summary>
/// Confirms the Repository API client is registered with the library caching defaults
/// enabled so single/list game-server reads and map reads use the client L1 cache shipped in 4.2.21.
/// </summary>
[Trait("Category", "Integration")]
public class RepositoryClientCachingTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public RepositoryClientCachingTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _ = factory.CreateClient();
    }

    [Fact]
    public void GameServersApi_LibraryCacheDefaults_AreRegistered()
    {
        using var scope = _factory.Services.CreateScope();
        var defaults = scope.ServiceProvider.GetService<DefaultCachePolicies<IGameServersApi>>();

        Assert.NotNull(defaults);
        Assert.NotEmpty(defaults!.Policies);
    }

    [Fact]
    public void MapsApi_LibraryCacheDefaults_AreRegistered()
    {
        using var scope = _factory.Services.CreateScope();
        var defaults = scope.ServiceProvider.GetService<DefaultCachePolicies<IMapsApi>>();

        Assert.NotNull(defaults);
        Assert.NotEmpty(defaults!.Policies);
    }

    [Fact]
    public void RepositoryClient_CachingParticipation_IsEnabled()
    {
        // IMxCache is only wired into DI when the Repository client is registered with
        // WithCaching(...). If the .WithCaching(...) call is removed this resolution fails.
        var mxCacheType = Type.GetType("MX.Caching.Abstractions.IMxCache, MX.Caching.Abstractions");
        Assert.NotNull(mxCacheType);

        using var scope = _factory.Services.CreateScope();
        var mxCache = scope.ServiceProvider.GetService(mxCacheType!);

        Assert.NotNull(mxCache);
    }
}

