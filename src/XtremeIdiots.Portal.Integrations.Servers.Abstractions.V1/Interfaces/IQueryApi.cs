using MxIO.ApiClient.Abstractions;

using XtremeIdiots.Portal.ServersApi.Abstractions.Models;

namespace XtremeIdiots.Portal.ServersApi.Abstractions.Interfaces
{
    public interface IQueryApi
    {
        Task<ApiResponseDto<ServerQueryStatusResponseDto>> GetServerStatus(Guid gameServerId);
    }
}
