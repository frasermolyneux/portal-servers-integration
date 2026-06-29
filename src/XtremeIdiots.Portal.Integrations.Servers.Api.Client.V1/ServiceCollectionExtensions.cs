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
            serviceCollection.AddTypedApiClient<ICoD4xRconApi, CoD4xRconApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(configureOptions);
            serviceCollection.AddTypedApiClient<ICod2RconApi, Cod2RconApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(configureOptions);
            serviceCollection.AddTypedApiClient<ICod4RconApi, Cod4RconApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(configureOptions);
            serviceCollection.AddTypedApiClient<ICod5RconApi, Cod5RconApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(configureOptions);
            serviceCollection.AddTypedApiClient<IInsurgencyRconApi, InsurgencyRconApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(configureOptions);
            serviceCollection.AddTypedApiClient<IRustRconApi, RustRconApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(configureOptions);
            serviceCollection.AddTypedApiClient<IL4d2RconApi, L4d2RconApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(configureOptions);
            serviceCollection.AddTypedApiClient<IMapsApi, MapsApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(configureOptions);

            // Register API info endpoint
            serviceCollection.AddTypedApiClient<IApiInfoApi, ApiInfoApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(configureOptions);

            // Register API health endpoint
            serviceCollection.AddTypedApiClient<IApiHealthApi, ApiHealthApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(configureOptions);

            // Register Config API endpoint
            serviceCollection.AddTypedApiClient<IConfigApi, ConfigApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(configureOptions);

            // Register transport-neutral browse endpoint.
            serviceCollection.AddTypedApiClient<IFileBrowseApi, FileBrowseApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(configureOptions);

            // Register version selectors as scoped
            serviceCollection.AddScoped<IVersionedQueryApi, VersionedQueryApi>();
            serviceCollection.AddScoped<IVersionedRconApi, VersionedRconApi>();
            serviceCollection.AddScoped<IVersionedCoD4xRconApi, VersionedCoD4xRconApi>();
            serviceCollection.AddScoped<IVersionedCod2RconApi, VersionedCod2RconApi>();
            serviceCollection.AddScoped<IVersionedCod4RconApi, VersionedCod4RconApi>();
            serviceCollection.AddScoped<IVersionedCod5RconApi, VersionedCod5RconApi>();
            serviceCollection.AddScoped<IVersionedInsurgencyRconApi, VersionedInsurgencyRconApi>();
            serviceCollection.AddScoped<IVersionedRustRconApi, VersionedRustRconApi>();
            serviceCollection.AddScoped<IVersionedL4d2RconApi, VersionedL4d2RconApi>();
            serviceCollection.AddScoped<IVersionedMapsApi, VersionedMapsApi>();
            serviceCollection.AddScoped<IVersionedApiHealthApi, VersionedApiHealthApi>();
            serviceCollection.AddScoped<IVersionedApiInfoApi, VersionedApiInfoApi>();
            serviceCollection.AddScoped<IVersionedConfigApi, VersionedConfigApi>();
            serviceCollection.AddScoped<IVersionedFileBrowseApi, VersionedFileBrowseApi>();

            // Register the unified client as scoped
            serviceCollection.AddScoped<IServersApiClient, ServersApiClient>();

            return serviceCollection;
        }
    }
}