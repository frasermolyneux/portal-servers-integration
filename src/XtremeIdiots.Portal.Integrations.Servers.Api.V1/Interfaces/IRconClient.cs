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

        /// <summary>
        /// Kicks a player from the server by their client ID
        /// </summary>
        /// <param name="clientId">The client ID to kick</param>
        /// <returns>Response message from the server</returns>
        Task<string> KickPlayer(int clientId);

        /// <summary>
        /// Bans a player from the server by their client ID
        /// </summary>
        /// <param name="clientId">The client ID to ban</param>
        /// <returns>Response message from the server</returns>
        Task<string> BanPlayer(int clientId);
    }
}