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

            // Register V1 API implementations as scoped to match IRestClientService lifetime
            serviceCollection.AddScoped<IQueryApi, QueryApi>();
            serviceCollection.AddScoped<IRconApi, RconApi>();
            serviceCollection.AddScoped<IMapsApi, MapsApi>();
            serviceCollection.AddScoped<IRootApi, RootApi>();

            // Register version selectors as scoped to match V1 API lifetime
            serviceCollection.AddScoped<IVersionedQueryApi, VersionedQueryApi>();
            serviceCollection.AddScoped<IVersionedRconApi, VersionedRconApi>();
            serviceCollection.AddScoped<IVersionedMapsApi, VersionedMapsApi>();
            serviceCollection.AddScoped<IVersionedRootApi, VersionedRootApi>();

            // Register the unified client as scoped to match versioned API lifetime
            serviceCollection.AddScoped<IServersApiClient, ServersApiClient>();

            return serviceCollection;
        }
    }
}