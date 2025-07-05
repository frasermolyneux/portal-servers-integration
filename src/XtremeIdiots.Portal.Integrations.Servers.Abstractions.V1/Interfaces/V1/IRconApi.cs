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
    }
}
