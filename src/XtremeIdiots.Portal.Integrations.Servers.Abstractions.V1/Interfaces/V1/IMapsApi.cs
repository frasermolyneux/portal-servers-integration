using MxIO.ApiClient.Abstractions;

using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Maps;

namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1
{
    public interface IMapsApi
    {
        Task<ApiResponseDto<ServerMapsCollectionDto>> GetLoadedServerMapsFromHost(Guid gameServerId);
        Task<ApiResponseDto> PushServerMapToHost(Guid gameServerId, string mapName);
        Task<ApiResponseDto> DeleteServerMapFromHost(Guid gameServerId, string mapName);
    }
}
