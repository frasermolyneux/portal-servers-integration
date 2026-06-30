using MX.Api.Abstractions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;

public interface ICod2RconApi
{
    Task<ApiResult<RconCurrentMapDto>> GetCurrentMap(Guid gameServerId, CancellationToken cancellationToken = default);
    Task<ApiResult<RconStatusResponseDto>> Status(Guid gameServerId, CancellationToken cancellationToken = default);
    Task<ApiResult<RconMapCollectionDto>> GetMaps(Guid gameServerId, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> ServerInfo(Guid gameServerId, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> SystemInfo(Guid gameServerId, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> CmdList(Guid gameServerId, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> CvarList(Guid gameServerId, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> DvarList(Guid gameServerId, CancellationToken cancellationToken = default);
    Task<ApiResult> Say(Guid gameServerId, SayRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> Tell(Guid gameServerId, CoD4xTargetMessageRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> Map(Guid gameServerId, ChangeMapRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> Kick(Guid gameServerId, ClientSlotRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> TempBan(Guid gameServerId, ClientSlotRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> Ban(Guid gameServerId, ClientSlotRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> Set(Guid gameServerId, SetDvarRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> Seta(Guid gameServerId, SetDvarRequest request, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> Restart(Guid gameServerId, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> RestartMap(Guid gameServerId, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> FastRestartMap(Guid gameServerId, CancellationToken cancellationToken = default);
    Task<ApiResult<string>> NextMap(Guid gameServerId, CancellationToken cancellationToken = default);
}