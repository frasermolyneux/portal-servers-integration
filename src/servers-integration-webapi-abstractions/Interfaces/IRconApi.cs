using MxIO.ApiClient.Abstractions;

using XtremeIdiots.Portal.ServersApi.Abstractions.Models;
using XtremeIdiots.Portal.ServersApi.Abstractions.Models.Rcon;

namespace XtremeIdiots.Portal.ServersApi.Abstractions.Interfaces
{
    public interface IRconApi
    {
        Task<ApiResponseDto<ServerRconStatusResponseDto>> GetServerStatus(Guid gameServerId);
        Task<ApiResponseDto<RconMapCollectionDto>> GetServerMaps(Guid gameServerId);
    }
}
