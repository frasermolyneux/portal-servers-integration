using MxIO.ApiClient.Abstractions;

using XtremeIdiots.Portal.ServersApi.Abstractions.Models;

namespace XtremeIdiots.Portal.ServersApi.Abstractions.Interfaces
{
    public interface IRconApi
    {
        Task<ApiResponseDto<ServerRconStatusResponseDto>> GetServerStatus(Guid gameServerId);
    }
}
