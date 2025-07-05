using MX.Api.Abstractions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Maps;

namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1
{
    public interface IMapsApi
    {
        Task<ApiResult<ServerMapsCollectionDto>> GetLoadedServerMapsFromHost(Guid gameServerId);
        Task<ApiResult> PushServerMapToHost(Guid gameServerId, string mapName);
        Task<ApiResult> DeleteServerMapFromHost(Guid gameServerId, string mapName);
    }
}
