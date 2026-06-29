namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1
{
    public interface IServersApiClient
    {
        public IVersionedQueryApi Query { get; }
        public IVersionedRconApi Rcon { get; }
        public IVersionedCoD4xRconApi CoD4xRcon => NotSupportedVersionedCoD4xRconApi.Instance;
        public IVersionedCod2RconApi Cod2Rcon => NotSupportedVersionedCod2RconApi.Instance;
        public IVersionedCod4RconApi Cod4Rcon => NotSupportedVersionedCod4RconApi.Instance;
        public IVersionedCod5RconApi Cod5Rcon => NotSupportedVersionedCod5RconApi.Instance;
        public IVersionedInsurgencyRconApi InsurgencyRcon => NotSupportedVersionedInsurgencyRconApi.Instance;
        public IVersionedRustRconApi RustRcon => NotSupportedVersionedRustRconApi.Instance;
        public IVersionedL4d2RconApi L4d2Rcon => NotSupportedVersionedL4d2RconApi.Instance;
        public IVersionedMapsApi Maps { get; }
        public IVersionedApiHealthApi ApiHealth { get; }
        public IVersionedApiInfoApi ApiInfo { get; }
        public IVersionedConfigApi Config { get; }
        public IVersionedFileBrowseApi FileBrowse { get; }
        public IVersionedFilesApi Files => NotSupportedVersionedFilesApi.Instance;
    }
}