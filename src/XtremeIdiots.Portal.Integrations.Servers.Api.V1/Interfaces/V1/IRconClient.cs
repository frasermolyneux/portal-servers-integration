using XtremeIdiots.Portal.Integrations.Servers.Api.Models.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1
{
    public interface IRconClient
    {
        void Configure(GameType gameType, Guid gameServerId, string hostname, int queryPort, string rconPassword);
        List<IRconPlayer> GetPlayers();
        Task<string> GetCurrentMap();
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

        /// <summary>
        /// Kicks a player from the server by their name
        /// </summary>
        /// <param name="name">The player name to kick</param>
        /// <returns>Response message from the server</returns>
        Task<string> KickPlayerByName(string name);

        /// <summary>
        /// Kicks all players from the server
        /// </summary>
        /// <returns>Response message from the server</returns>
        Task<string> KickAllPlayers();

        /// <summary>
        /// Bans a player from the server by their name
        /// </summary>
        /// <param name="name">The player name to ban</param>
        /// <returns>Response message from the server</returns>
        Task<string> BanPlayerByName(string name);

        /// <summary>
        /// Temporarily bans a player from the server by their name
        /// </summary>
        /// <param name="name">The player name to temporarily ban</param>
        /// <returns>Response message from the server</returns>
        Task<string> TempBanPlayerByName(string name);

        /// <summary>
        /// Temporarily bans a player from the server by their client ID
        /// </summary>
        /// <param name="clientId">The client ID to temporarily ban</param>
        /// <returns>Response message from the server</returns>
        Task<string> TempBanPlayer(int clientId);

        /// <summary>
        /// Unbans a player from the server by their name
        /// </summary>
        /// <param name="name">The player name to unban</param>
        /// <returns>Response message from the server</returns>
        Task<string> UnbanPlayer(string name);

        /// <summary>
        /// Sends a message to a specific player
        /// </summary>
        /// <param name="clientId">The client ID to send message to</param>
        /// <param name="message">The message to send</param>
        /// <returns>Response message from the server</returns>
        Task<string> TellPlayer(int clientId, string message);

        /// <summary>
        /// Loads a specific map
        /// </summary>
        /// <param name="mapName">The name of the map to load</param>
        /// <returns>Response message from the server</returns>
        Task<string> ChangeMap(string mapName);

        /// <summary>
        /// Gets server information including settings and player list
        /// </summary>
        /// <returns>Server information</returns>
        Task<string> GetServerInfo();

        /// <summary>
        /// Gets system information
        /// </summary>
        /// <returns>System information</returns>
        Task<string> GetSystemInfo();

        /// <summary>
        /// Gets list of all available commands
        /// </summary>
        /// <returns>List of commands</returns>
        Task<string> GetCommandList();
    }
}