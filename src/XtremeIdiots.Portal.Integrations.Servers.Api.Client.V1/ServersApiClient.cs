namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1
{
    public class ServersApiClient : IServersApiClient
    {
        public ServersApiClient(
            IVersionedQueryApi queryApiClient,
            IVersionedRconApi rconApiClient,
            IVersionedMapsApi mapsApiClient,
            IVersionedApiHealthApi apiHealth,
            IVersionedApiInfoApi apiInfo,
            IVersionedConfigApi config,
            IVersionedFileBrowseApi fileBrowse)
            : this(queryApiClient, rconApiClient, NotSupportedVersionedCoD4xRconApi.Instance, mapsApiClient, apiHealth, apiInfo, config, fileBrowse)
        {
        }

        public ServersApiClient(
            IVersionedQueryApi queryApiClient,
            IVersionedRconApi rconApiClient,
            IVersionedCoD4xRconApi coD4xRconApiClient,
            IVersionedMapsApi mapsApiClient,
            IVersionedApiHealthApi apiHealth,
            IVersionedApiInfoApi apiInfo,
            IVersionedConfigApi config,
            IVersionedFileBrowseApi fileBrowse)
        {
            Query = queryApiClient;
            Rcon = rconApiClient;
            CoD4xRcon = coD4xRconApiClient;
            Maps = mapsApiClient;
            ApiHealth = apiHealth;
            ApiInfo = apiInfo;
            Config = config;
            FileBrowse = fileBrowse;
        }

        public IVersionedQueryApi Query { get; }
        public IVersionedRconApi Rcon { get; }
        public IVersionedCoD4xRconApi CoD4xRcon { get; }
        public IVersionedMapsApi Maps { get; }
        public IVersionedApiHealthApi ApiHealth { get; }
        public IVersionedApiInfoApi ApiInfo { get; }
        public IVersionedConfigApi Config { get; }
        public IVersionedFileBrowseApi FileBrowse { get; }
    }
}