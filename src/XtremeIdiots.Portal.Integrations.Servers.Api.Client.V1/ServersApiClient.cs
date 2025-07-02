namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1
{
    public class ServersApiClient : IServersApiClient
    {
        public ServersApiClient(
            IVersionedQueryApi queryApiClient,
            IVersionedRconApi rconApiClient,
            IVersionedMapsApi mapsApiClient,
            IVersionedRootApi rootApiClient)
        {
            Query = queryApiClient;
            Rcon = rconApiClient;
            Maps = mapsApiClient;
            Root = rootApiClient;
        }

        public IVersionedQueryApi Query { get; }
        public IVersionedRconApi Rcon { get; }
        public IVersionedMapsApi Maps { get; }
        public IVersionedRootApi Root { get; }
    }
}