using MX.Api.Abstractions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1
{
    public interface IQueryApi
    {
        Task<ApiResult<ServerQueryStatusResponseDto>> GetServerStatus(Guid gameServerId);
    }
}
