using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1
{
    public interface IQueryClientFactory
    {
        IQueryClient CreateInstance(GameType gameType, string hostname, int queryPort);
    }
}