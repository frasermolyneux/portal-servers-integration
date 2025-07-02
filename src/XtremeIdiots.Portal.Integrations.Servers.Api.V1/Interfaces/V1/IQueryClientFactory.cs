using XtremeIdiots.Portal.RepositoryApi.Abstractions.Constants;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1
{
    public interface IQueryClientFactory
    {
        IQueryClient CreateInstance(GameType gameType, string hostname, int queryPort);
    }
}