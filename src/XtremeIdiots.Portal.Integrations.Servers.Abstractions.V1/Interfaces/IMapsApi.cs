using MxIO.ApiClient.Abstractions;

using XtremeIdiots.Portal.ServersApi.Abstractions.Models.Maps;

namespace XtremeIdiots.Portal.ServersApi.Abstractions.Interfaces
{
    public interface IMapsApi
    {
        Task<ApiResponseDto<ServerMapsCollectionDto>> GetLoadedServerMapsFromHost(Guid gameServerId);
        Task<ApiResponseDto> PushServerMapToHost(Guid gameServerId, string mapName);
        Task<ApiResponseDto> DeleteServerMapFromHost(Guid gameServerId, string mapName);
    }
}
