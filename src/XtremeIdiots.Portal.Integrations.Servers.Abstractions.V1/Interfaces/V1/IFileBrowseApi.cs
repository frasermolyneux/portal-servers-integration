using MX.Api.Abstractions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Ftp;

namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;

public interface IFileBrowseApi
{
    Task<ApiResult<FtpDirectoryListingDto>> BrowseDirectory(Guid gameServerId, string? path = null, CancellationToken cancellationToken = default);
}
