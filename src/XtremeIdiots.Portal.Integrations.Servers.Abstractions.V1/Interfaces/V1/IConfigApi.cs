using MX.Api.Abstractions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Config;

namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;

public interface IConfigApi
{
    Task<ApiResult<ConfigFileContentDto>> GetConfigFile(Guid gameServerId, string filePath, CancellationToken cancellationToken = default);
    Task<ApiResult> UpdateConfigVariable(Guid gameServerId, string filePath, string variableName, string value, string[]? commentLines = null, CancellationToken cancellationToken = default);
}
