using XtremeIdiots.Portal.RepositoryApi.Abstractions.Constants;
using XtremeIdiots.Portal.ServersWebApi.Models;

namespace XtremeIdiots.Portal.ServersWebApi.Interfaces
{
    public interface IRconClient
    {
        void Configure(GameType gameType, Guid gameServerId, string hostname, int queryPort, string rconPassword);
        List<IRconPlayer> GetPlayers();
        Task Say(string message);
        Task<List<Quake3QueryMap>> GetMaps();
        Task<string> Restart();
        Task<string> RestartMap();
        Task<string> FastRestartMap();
        Task<string> NextMap();
    }
}