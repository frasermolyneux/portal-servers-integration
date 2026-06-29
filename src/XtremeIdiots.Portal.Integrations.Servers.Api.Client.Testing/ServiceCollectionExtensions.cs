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
        services.RemoveAll<IVersionedCoD4xRconApi>();
        services.RemoveAll<IVersionedCod2RconApi>();
        services.RemoveAll<IVersionedCod4RconApi>();
        services.RemoveAll<IVersionedCod5RconApi>();
        services.RemoveAll<IVersionedInsurgencyRconApi>();
        services.RemoveAll<IVersionedRustRconApi>();
        services.RemoveAll<IVersionedL4d2RconApi>();
        services.RemoveAll<IVersionedMapsApi>();
        services.RemoveAll<IVersionedApiHealthApi>();
        services.RemoveAll<IVersionedApiInfoApi>();
        services.RemoveAll<IVersionedConfigApi>();
        services.RemoveAll<IVersionedFileBrowseApi>();
        services.RemoveAll<IApiHealthApi>();
        services.RemoveAll<IApiInfoApi>();
        services.RemoveAll<IQueryApi>();
        services.RemoveAll<IRconApi>();
        services.RemoveAll<ICoD4xRconApi>();
        services.RemoveAll<ICod2RconApi>();
        services.RemoveAll<ICod4RconApi>();
        services.RemoveAll<ICod5RconApi>();
        services.RemoveAll<IInsurgencyRconApi>();
        services.RemoveAll<IRustRconApi>();
        services.RemoveAll<IL4d2RconApi>();
        services.RemoveAll<IMapsApi>();
        services.RemoveAll<IConfigApi>();
        services.RemoveAll<IFileBrowseApi>();

        // Register fakes as singletons
        services.AddSingleton(fakeClient);
        services.AddSingleton<IServersApiClient>(fakeClient);
        services.AddSingleton(fakeClient.Query);
        services.AddSingleton(fakeClient.Rcon);
        services.AddSingleton(fakeClient.CoD4xRcon);
        services.AddSingleton(fakeClient.Cod2Rcon);
        services.AddSingleton(fakeClient.Cod4Rcon);
        services.AddSingleton(fakeClient.Cod5Rcon);
        services.AddSingleton(fakeClient.InsurgencyRcon);
        services.AddSingleton(fakeClient.RustRcon);
        services.AddSingleton(fakeClient.L4d2Rcon);
        services.AddSingleton(fakeClient.Maps);
        services.AddSingleton(fakeClient.ApiHealth);
        services.AddSingleton(fakeClient.ApiInfo);
        services.AddSingleton(fakeClient.Config);
        services.AddSingleton(fakeClient.FileBrowse);
        services.AddSingleton<IApiHealthApi>(fakeClient.FakeApiHealth);
        services.AddSingleton<IApiInfoApi>(fakeClient.FakeApiInfo);
        services.AddSingleton<IQueryApi>(fakeClient.FakeQuery);
        services.AddSingleton<IRconApi>(fakeClient.FakeRcon);
        services.AddSingleton<ICoD4xRconApi>(fakeClient.FakeCoD4xRcon);
        services.AddSingleton<ICod2RconApi>(fakeClient.FakeCod2Rcon);
        services.AddSingleton<ICod4RconApi>(fakeClient.FakeCod4Rcon);
        services.AddSingleton<ICod5RconApi>(fakeClient.FakeCod5Rcon);
        services.AddSingleton<IInsurgencyRconApi>(fakeClient.FakeInsurgencyRcon);
        services.AddSingleton<IRustRconApi>(fakeClient.FakeRustRcon);
        services.AddSingleton<IL4d2RconApi>(fakeClient.FakeL4d2Rcon);
        services.AddSingleton<IMapsApi>(fakeClient.FakeMaps);
        services.AddSingleton<IConfigApi>(fakeClient.FakeConfig);
        services.AddSingleton<IFileBrowseApi>(fakeClient.FakeFileBrowse);

        return services;
    }
}
