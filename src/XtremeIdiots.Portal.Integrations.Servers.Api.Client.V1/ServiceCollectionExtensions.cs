using Microsoft.Extensions.DependencyInjection;
using MX.Api.Client.Extensions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddServersApiClient(this IServiceCollection serviceCollection,
            Action<ServersApiClientOptions>? configure = null)
        {
            serviceCollection.AddApiClient();

            if (configure != null)
            {
                serviceCollection.Configure(configure);
            }

            // Register V1 API implementations
            serviceCollection.AddSingleton<IQueryApi, QueryApi>();
            serviceCollection.AddSingleton<IRconApi, RconApi>();
            serviceCollection.AddSingleton<IMapsApi, MapsApi>();
            serviceCollection.AddSingleton<IRootApi, RootApi>();

            // Register version selectors
            serviceCollection.AddSingleton<IVersionedQueryApi, VersionedQueryApi>();
            serviceCollection.AddSingleton<IVersionedRconApi, VersionedRconApi>();
            serviceCollection.AddSingleton<IVersionedMapsApi, VersionedMapsApi>();
            serviceCollection.AddSingleton<IVersionedRootApi, VersionedRootApi>();

            // Register the unified client
            serviceCollection.AddSingleton<IServersApiClient, ServersApiClient>();

            return serviceCollection;
        }
    }
}