namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1
{
    public interface IServersApiClient
    {
        public IVersionedQueryApi Query { get; }
        public IVersionedRconApi Rcon { get; }
        public IVersionedCoD4xRconApi CoD4xRcon => NotSupportedVersionedCoD4xRconApi.Instance;
        public IVersionedMapsApi Maps { get; }
        public IVersionedApiHealthApi ApiHealth { get; }
        public IVersionedApiInfoApi ApiInfo { get; }
        public IVersionedConfigApi Config { get; }
        public IVersionedFileBrowseApi FileBrowse { get; }
    }
}