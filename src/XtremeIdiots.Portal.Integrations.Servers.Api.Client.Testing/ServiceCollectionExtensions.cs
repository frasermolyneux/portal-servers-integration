using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing;

/// <summary>
/// DI extension methods for registering fake API client implementations in test containers.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Replaces all real <see cref="IServersApiClient"/> registrations with in-memory fakes.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="configure">Optional callback to pre-configure the fake client with canned responses.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFakeServersApiClient(
        this IServiceCollection services,
        Action<FakeServersApiClient>? configure = null)
    {
        var fakeClient = new FakeServersApiClient();
        configure?.Invoke(fakeClient);

        // Remove real implementations
        services.RemoveAll<IServersApiClient>();
        services.RemoveAll<IVersionedQueryApi>();
        services.RemoveAll<IVersionedRconApi>();
        services.RemoveAll<IVersionedMapsApi>();
        services.RemoveAll<IVersionedApiHealthApi>();
        services.RemoveAll<IVersionedApiInfoApi>();
        services.RemoveAll<IApiHealthApi>();
        services.RemoveAll<IApiInfoApi>();
        services.RemoveAll<IQueryApi>();
        services.RemoveAll<IRconApi>();
        services.RemoveAll<IMapsApi>();

        // Register fakes as singletons
        services.AddSingleton(fakeClient);
        services.AddSingleton<IServersApiClient>(fakeClient);
        services.AddSingleton<IVersionedQueryApi>(fakeClient.Query);
        services.AddSingleton<IVersionedRconApi>(fakeClient.Rcon);
        services.AddSingleton<IVersionedMapsApi>(fakeClient.Maps);
        services.AddSingleton<IVersionedApiHealthApi>(fakeClient.ApiHealth);
        services.AddSingleton<IVersionedApiInfoApi>(fakeClient.ApiInfo);
        services.AddSingleton<IApiHealthApi>(fakeClient.FakeApiHealth);
        services.AddSingleton<IApiInfoApi>(fakeClient.FakeApiInfo);
        services.AddSingleton<IQueryApi>(fakeClient.FakeQuery);
        services.AddSingleton<IRconApi>(fakeClient.FakeRcon);
        services.AddSingleton<IMapsApi>(fakeClient.FakeMaps);

        return services;
    }
}
