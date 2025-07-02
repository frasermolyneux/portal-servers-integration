using XtremeIdiots.Portal.RepositoryApi.Abstractions.Constants;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1
{
    public interface IRconClientFactory
    {
        IRconClient CreateInstance(GameType gameType, Guid gameServerId, string hostname, int queryPort, string rconPassword);
    }
}