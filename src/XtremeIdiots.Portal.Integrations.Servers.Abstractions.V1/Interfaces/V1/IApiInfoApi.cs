using MX.Api.Abstractions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models;

namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;

public interface IApiInfoApi
{
    Task<ApiResult<ApiInfoDto>> GetApiInfo(CancellationToken cancellationToken = default);
}
