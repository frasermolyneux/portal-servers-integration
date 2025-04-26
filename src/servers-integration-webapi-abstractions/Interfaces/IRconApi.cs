using MxIO.ApiClient.Abstractions;

using XtremeIdiots.Portal.ServersApi.Abstractions.Models;
using XtremeIdiots.Portal.ServersApi.Abstractions.Models.Rcon;

namespace XtremeIdiots.Portal.ServersApi.Abstractions.Interfaces
{
    public interface IRconApi
    {
        Task<ApiResponseDto<ServerRconStatusResponseDto>> GetServerStatus(Guid gameServerId);
        Task<ApiResponseDto<RconMapCollectionDto>> GetServerMaps(Guid gameServerId);

        /// <summary>
        /// Kicks a player from the server by their client ID
        /// </summary>
        /// <param name="gameServerId">The ID of the game server</param>
        /// <param name="clientId">The client ID to kick</param>
        Task<ApiResponseDto> KickPlayer(Guid gameServerId, int clientId);

        /// <summary>
        /// Bans a player from the server by their client ID
        /// </summary>
        /// <param name="gameServerId">The ID of the game server</param>
        /// <param name="clientId">The client ID to ban</param>
        Task<ApiResponseDto> BanPlayer(Guid gameServerId, int clientId);
    }
}
