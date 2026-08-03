using Microsoft.Extensions.DependencyInjection;
using MX.Api.Client.Configuration;
using MX.Api.Client.Extensions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddServersApiClient(this IServiceCollection serviceCollection, Action<ServersApiClientOptionsBuilder> configureOptions)
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);
            ArgumentNullException.ThrowIfNull(configureOptions);

            // Run the consumer's configuration against a probe builder so we can lift any WithCaching
            // delegate into a SharedCacheConfiguration. MX.Api.Client scopes each ApiClientOptionsBuilder
            // to a single typed client, so applying the same multi-interface WithCaching expression via
            // AddTypedApiClient per sub-API crashes at startup. WithSharedCaching applies only the
            // operations whose declaring interface matches the current typed client and skips siblings.
            var probe = new ServersApiClientOptionsBuilder();
            configureOptions(probe);
            var capturedCache = probe.CapturedCacheConfigure;
            var sharedCache = capturedCache is null ? null : new SharedCacheConfiguration(capturedCache);

            Action<ServersApiClientOptionsBuilder> perClient = sharedCache is null
                ? configureOptions
                : builder =>
                {
                    configureOptions(builder);
                    builder.WithSharedCaching(sharedCache);
                };

            // Register V1 API implementations using the new typed pattern
            serviceCollection.AddTypedApiClient<IQueryApi, QueryApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(perClient);
            serviceCollection.AddTypedApiClient<ICoD4xRconApi, CoD4xRconApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(perClient);
            serviceCollection.AddTypedApiClient<ICod2RconApi, Cod2RconApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(perClient);
            serviceCollection.AddTypedApiClient<ICod4RconApi, Cod4RconApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(perClient);
            serviceCollection.AddTypedApiClient<ICod5RconApi, Cod5RconApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(perClient);
            serviceCollection.AddTypedApiClient<IInsurgencyRconApi, InsurgencyRconApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(perClient);
            serviceCollection.AddTypedApiClient<IRustRconApi, RustRconApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(perClient);
            serviceCollection.AddTypedApiClient<IL4d2RconApi, L4d2RconApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(perClient);
            serviceCollection.AddTypedApiClient<IMapsApi, MapsApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(perClient);

            // Register API info endpoint
            serviceCollection.AddTypedApiClient<IApiInfoApi, ApiInfoApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(perClient);

            // Register API health endpoint
            serviceCollection.AddTypedApiClient<IApiHealthApi, ApiHealthApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(perClient);

            // Register Config API endpoint
            serviceCollection.AddTypedApiClient<IConfigApi, ConfigApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(perClient);

            // Register transport-neutral browse endpoint.
            serviceCollection.AddTypedApiClient<IFileBrowseApi, FileBrowseApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(perClient);

            // Register transport-neutral generic files endpoints.
            serviceCollection.AddTypedApiClient<IFilesApi, FilesApi, ServersApiClientOptions, ServersApiClientOptionsBuilder>(perClient);

            // Fail fast on typos: any cache operation whose declaring interface never matched a registered
            // typed client (e.g. an unregistered/renamed sub-API) throws InvalidOperationException here
            // rather than silently no-op'ing.
            sharedCache?.ValidateAllOperationsMatched();

            // Register version selectors as scoped
            serviceCollection.AddScoped<IVersionedQueryApi, VersionedQueryApi>();
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
            serviceCollection.AddScoped<IVersionedFilesApi, VersionedFilesApi>();

            // Register the unified client as scoped
            serviceCollection.AddScoped<IServersApiClient, ServersApiClient>();

            return serviceCollection;
        }
    }
}
