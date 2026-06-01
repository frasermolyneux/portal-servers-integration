using MX.Api.Abstractions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Maps;

namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;

public interface IMapsApi
{
    Task<ApiResult<ServerMapsCollectionDto>> GetLoadedServerMapsFromHost(Guid gameServerId);
    Task<ApiResult> PushServerMapToHost(Guid gameServerId, string mapName);
    Task<ApiResult> DeleteServerMapFromHost(Guid gameServerId, string mapName);

    /// <summary>
    /// Verifies whether the specified maps exist on the game server file transport host.
    /// </summary>
    /// <param name="gameServerId">The ID of the game server</param>
    /// <param name="mapNames">The list of map names to verify</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<ApiResult<MapVerificationCollectionDto>> VerifyServerMaps(Guid gameServerId, List<string> mapNames, CancellationToken cancellationToken = default);
}
