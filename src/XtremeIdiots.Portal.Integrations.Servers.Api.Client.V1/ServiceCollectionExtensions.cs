using Microsoft.Extensions.DependencyInjection;

using MxIO.ApiClient.Extensions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1
{
    public static class ServiceCollectionExtensions
    {
        public static void AddServersApiClient(this IServiceCollection serviceCollection,
            Action<ServersApiClientOptions> configure)
        {
            serviceCollection.AddApiClient();
            serviceCollection.Configure(configure);

            // Register V1 API implementations
            serviceCollection.AddSingleton<IQueryApi, QueryApi>();
            serviceCollection.AddSingleton<IRconApi, RconApi>();
            serviceCollection.AddSingleton<IMapsApi, MapsApi>();

            // Register version selectors
            serviceCollection.AddSingleton<IVersionedQueryApi, VersionedQueryApi>();
            serviceCollection.AddSingleton<IVersionedRconApi, VersionedRconApi>();
            serviceCollection.AddSingleton<IVersionedMapsApi, VersionedMapsApi>();

            // Register the unified client
            serviceCollection.AddSingleton<IServersApiClient, ServersApiClient>();
        }
    }
}