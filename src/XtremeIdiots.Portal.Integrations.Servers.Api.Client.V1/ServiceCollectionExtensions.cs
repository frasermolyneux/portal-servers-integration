using Microsoft.Extensions.DependencyInjection;
using MX.Api.Client.Extensions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddServersApiClient(this IServiceCollection serviceCollection, Action<ServersApiClientOptionsBuilder> configureOptions)
        {
            // Register V1 API implementations using the new typed pattern
            serviceCollection.AddTypedApiClient<IQueryApi, QueryApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(configureOptions);
            serviceCollection.AddTypedApiClient<IRconApi, RconApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(configureOptions);
            serviceCollection.AddTypedApiClient<IMapsApi, MapsApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(configureOptions);

            // Register API info endpoint
            serviceCollection.AddTypedApiClient<IApiInfoApi, ApiInfoApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(configureOptions);

            // Register API health endpoint
            serviceCollection.AddTypedApiClient<IApiHealthApi, ApiHealthApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(configureOptions);

            // Register version selectors as scoped
            serviceCollection.AddScoped<IVersionedQueryApi, VersionedQueryApi>();
            serviceCollection.AddScoped<IVersionedRconApi, VersionedRconApi>();
            serviceCollection.AddScoped<IVersionedMapsApi, VersionedMapsApi>();
            serviceCollection.AddScoped<IVersionedApiHealthApi, VersionedApiHealthApi>();
            serviceCollection.AddScoped<IVersionedApiInfoApi, VersionedApiInfoApi>();

            // Register the unified client as scoped
            serviceCollection.AddScoped<IServersApiClient, ServersApiClient>();

            return serviceCollection;
        }
    }
}