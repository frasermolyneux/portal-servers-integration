namespace XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1
{
    public interface IQueryClient
    {
        void Configure(string hostname, int queryPort);
        Task<IQueryResponse> GetServerStatus();
    }
}