using Microsoft.Extensions.DependencyInjection;

using MxIO.ApiClient.Extensions;

using XtremeIdiots.Portal.ServersApi.Abstractions.Interfaces;
using XtremeIdiots.Portal.ServersApiClient.Api;

namespace XtremeIdiots.Portal.ServersApiClient
{
    public static class ServiceCollectionExtensions
    {
        public static void AddServersApiClient(this IServiceCollection serviceCollection,
            Action<ServersApiClientOptions> configure)
        {
            serviceCollection.AddApiClient();

            serviceCollection.Configure(configure);

            serviceCollection.AddSingleton<IQueryApi, QueryApi>();
            serviceCollection.AddSingleton<IRconApi, RconApi>();
            serviceCollection.AddSingleton<IMapsApi, MapsApi>();

            serviceCollection.AddSingleton<IServersApiClient, ServersApiClient>();
        }
    }
}