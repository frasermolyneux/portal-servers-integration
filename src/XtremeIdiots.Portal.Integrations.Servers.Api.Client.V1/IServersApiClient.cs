namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1
{
    public interface IServersApiClient
    {
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