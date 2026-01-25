using MX.Api.Abstractions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1
{
    public interface IRconApi
    {
        Task<ApiResult<ServerRconStatusResponseDto>> GetServerStatus(Guid gameServerId);
        Task<ApiResult<RconMapCollectionDto>> GetServerMaps(Guid gameServerId);

        /// <summary>
        /// Kicks a player from the server by their client ID
        /// </summary>
        /// <param name="gameServerId">The ID of the game server</param>
        /// <param name="clientId">The client ID to kick</param>
        Task<ApiResult> KickPlayer(Guid gameServerId, int clientId);

        /// <summary>
        /// Bans a player from the server by their client ID
        /// </summary>
        /// <param name="gameServerId">The ID of the game server</param>
        /// <param name="clientId">The client ID to ban</param>
        Task<ApiResult> BanPlayer(Guid gameServerId, int clientId);

        /// <summary>
        /// Restarts the game server
        /// </summary>
        /// <param name="gameServerId">The ID of the game server</param>
        Task<ApiResult> Restart(Guid gameServerId);

        /// <summary>
        /// Restarts the current map
        /// </summary>
        /// <param name="gameServerId">The ID of the game server</param>
        Task<ApiResult> RestartMap(Guid gameServerId);

        /// <summary>
        /// Fast restarts the current map
        /// </summary>
        /// <param name="gameServerId">The ID of the game server</param>
        Task<ApiResult> FastRestartMap(Guid gameServerId);

        /// <summary>
        /// Rotates to the next map in the rotation
        /// </summary>
        /// <param name="gameServerId">The ID of the game server</param>
        Task<ApiResult> NextMap(Guid gameServerId);

        /// <summary>
        /// Broadcasts a message to all players on the server
        /// </summary>
        /// <param name="gameServerId">The ID of the game server</param>
        /// <param name="message">The message to broadcast</param>
        Task<ApiResult> Say(Guid gameServerId, string message);

        /// <summary>
        /// Sends a message to a specific player
        /// </summary>
        /// <param name="gameServerId">The ID of the game server</param>
        /// <param name="clientId">The client ID to send message to</param>
        /// <param name="message">The message to send</param>
        Task<ApiResult> TellPlayer(Guid gameServerId, int clientId, string message);

        /// <summary>
        /// Changes the current map
        /// </summary>
        /// <param name="gameServerId">The ID of the game server</param>
        /// <param name="mapName">The name of the map to load</param>
        Task<ApiResult> ChangeMap(Guid gameServerId, string mapName);

        /// <summary>
        /// Kicks a player from the server by their name
        /// </summary>
        /// <param name="gameServerId">The ID of the game server</param>
        /// <param name="name">The player name to kick</param>
        Task<ApiResult> KickPlayerByName(Guid gameServerId, string name);

        /// <summary>
        /// Kicks all players from the server
        /// </summary>
        /// <param name="gameServerId">The ID of the game server</param>
        Task<ApiResult> KickAllPlayers(Guid gameServerId);

        /// <summary>
        /// Bans a player from the server by their name
        /// </summary>
        /// <param name="gameServerId">The ID of the game server</param>
        /// <param name="name">The player name to ban</param>
        Task<ApiResult> BanPlayerByName(Guid gameServerId, string name);

        /// <summary>
        /// Temporarily bans a player from the server by their client ID
        /// </summary>
        /// <param name="gameServerId">The ID of the game server</param>
        /// <param name="clientId">The client ID to temporarily ban</param>
        Task<ApiResult> TempBanPlayer(Guid gameServerId, int clientId);

        /// <summary>
        /// Temporarily bans a player from the server by their name
        /// </summary>
        /// <param name="gameServerId">The ID of the game server</param>
        /// <param name="name">The player name to temporarily ban</param>
        Task<ApiResult> TempBanPlayerByName(Guid gameServerId, string name);

        /// <summary>
        /// Unbans a player from the server by their name
        /// </summary>
        /// <param name="gameServerId">The ID of the game server</param>
        /// <param name="name">The player name to unban</param>
        Task<ApiResult> UnbanPlayer(Guid gameServerId, string name);

        /// <summary>
        /// Gets server information including settings and player list
        /// </summary>
        /// <param name="gameServerId">The ID of the game server</param>
        Task<ApiResult<string>> GetServerInfo(Guid gameServerId);

        /// <summary>
        /// Gets system information
        /// </summary>
        /// <param name="gameServerId">The ID of the game server</param>
        Task<ApiResult<string>> GetSystemInfo(Guid gameServerId);

        /// <summary>
        /// Gets list of all available commands
        /// </summary>
        /// <param name="gameServerId">The ID of the game server</param>
        Task<ApiResult<string>> GetCommandList(Guid gameServerId);
    }
}
