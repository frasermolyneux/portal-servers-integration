using MxIO.ApiClient.Abstractions;

using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1
{
    public interface IQueryApi
    {
        Task<ApiResponseDto<ServerQueryStatusResponseDto>> GetServerStatus(Guid gameServerId);
    }
}
