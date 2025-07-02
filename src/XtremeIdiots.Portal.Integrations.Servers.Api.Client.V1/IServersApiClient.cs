namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1
{
    public interface IServersApiClient
    {
        public IVersionedQueryApi Query { get; }
        public IVersionedRconApi Rcon { get; }
        public IVersionedMapsApi Maps { get; }
    }
}