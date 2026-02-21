namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1
{
    public class ServersApiClient : IServersApiClient
    {
        public ServersApiClient(
            IVersionedQueryApi queryApiClient,
            IVersionedRconApi rconApiClient,
            IVersionedMapsApi mapsApiClient,
            IVersionedApiHealthApi apiHealth,
            IVersionedApiInfoApi apiInfo)
        {
            Query = queryApiClient;
            Rcon = rconApiClient;
            Maps = mapsApiClient;
            ApiHealth = apiHealth;
            ApiInfo = apiInfo;
        }

        public IVersionedQueryApi Query { get; }
        public IVersionedRconApi Rcon { get; }
        public IVersionedMapsApi Maps { get; }
        public IVersionedApiHealthApi ApiHealth { get; }
        public IVersionedApiInfoApi ApiInfo { get; }
    }
}