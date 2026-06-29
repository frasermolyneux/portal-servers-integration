using MX.Api.Abstractions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;

public interface IRustRconApi
{
    Task<ApiResult<RconCurrentMapDto>> GetCurrentMap(Guid gameServerId, CancellationToken cancellationToken = default);
    Task<ApiResult> Say(Guid gameServerId, SayRequest request, CancellationToken cancellationToken = default);
}
