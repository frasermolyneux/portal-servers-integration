namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1
{
    public class ServersApiClient : IServersApiClient
    {
        public ServersApiClient(
            IVersionedQueryApi queryApiClient,
            IVersionedCoD4xRconApi coD4xRconApiClient,
            IVersionedCod2RconApi cod2RconApiClient,
            IVersionedCod4RconApi cod4RconApiClient,
            IVersionedCod5RconApi cod5RconApiClient,
            IVersionedInsurgencyRconApi insurgencyRconApiClient,
            IVersionedRustRconApi rustRconApiClient,
            IVersionedL4d2RconApi l4d2RconApiClient,
            IVersionedMapsApi mapsApiClient,
            IVersionedApiHealthApi apiHealth,
            IVersionedApiInfoApi apiInfo,
            IVersionedConfigApi config,
            IVersionedFileBrowseApi fileBrowse,
            IVersionedFilesApi files)
        {
            Query = queryApiClient;
            CoD4xRcon = coD4xRconApiClient;
            Cod2Rcon = cod2RconApiClient;
            Cod4Rcon = cod4RconApiClient;
            Cod5Rcon = cod5RconApiClient;
            InsurgencyRcon = insurgencyRconApiClient;
            RustRcon = rustRconApiClient;
            L4d2Rcon = l4d2RconApiClient;
            Maps = mapsApiClient;
            ApiHealth = apiHealth;
            ApiInfo = apiInfo;
            Config = config;
            FileBrowse = fileBrowse;
            Files = files;
        }

        public IVersionedQueryApi Query { get; }
        public IVersionedCoD4xRconApi CoD4xRcon { get; }
        public IVersionedCod2RconApi Cod2Rcon { get; }
        public IVersionedCod4RconApi Cod4Rcon { get; }
        public IVersionedCod5RconApi Cod5Rcon { get; }
        public IVersionedInsurgencyRconApi InsurgencyRcon { get; }
        public IVersionedRustRconApi RustRcon { get; }
        public IVersionedL4d2RconApi L4d2Rcon { get; }
        public IVersionedMapsApi Maps { get; }
        public IVersionedApiHealthApi ApiHealth { get; }
        public IVersionedApiInfoApi ApiInfo { get; }
        public IVersionedConfigApi Config { get; }
        public IVersionedFileBrowseApi FileBrowse { get; }
        public IVersionedFilesApi Files { get; }
    }
}