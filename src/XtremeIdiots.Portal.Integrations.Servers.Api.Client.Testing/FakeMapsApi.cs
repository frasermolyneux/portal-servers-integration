using System.Collections.Concurrent;
using System.Net;
using MX.Api.Abstractions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Maps;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing;

/// <summary>
/// In-memory fake of <see cref="IMapsApi"/> for unit and integration testing.
/// Supports canned responses, error simulation, and call tracking.
/// </summary>
public class FakeMapsApi : IMapsApi
{
    private readonly ConcurrentDictionary<Guid, ApiResult<ServerMapsCollectionDto>> _loadedMapsResponses = new();
    private readonly ConcurrentBag<(string Operation, Guid ServerId, string? MapName)> _operationLog = [];

    public IReadOnlyCollection<(string Operation, Guid ServerId, string? MapName)> OperationLog => _operationLog.ToArray();

    public DefaultBehavior DefaultResponseBehavior { get; private set; } = DefaultBehavior.ReturnGenericSuccess;

    public FakeMapsApi AddLoadedMapsResponse(Guid gameServerId, ServerMapsCollectionDto dto)
    {
        _loadedMapsResponses[gameServerId] = new ApiResult<ServerMapsCollectionDto>(HttpStatusCode.OK, new ApiResponse<ServerMapsCollectionDto>(dto));
        return this;
    }

    public FakeMapsApi SetDefaultBehavior(DefaultBehavior behavior)
    {
        DefaultResponseBehavior = behavior;
        return this;
    }

    public void Reset()
    {
        _loadedMapsResponses.Clear();
        _operationLog.Clear();
        DefaultResponseBehavior = DefaultBehavior.ReturnGenericSuccess;
    }

    public Task<ApiResult<ServerMapsCollectionDto>> GetLoadedServerMapsFromHost(Guid gameServerId)
    {
        _operationLog.Add(("GetLoadedServerMapsFromHost", gameServerId, null));

        if (_loadedMapsResponses.TryGetValue(gameServerId, out var result))
            return Task.FromResult(result);

        return Task.FromResult(DefaultResponseBehavior switch
        {
            DefaultBehavior.ReturnGenericSuccess => new ApiResult<ServerMapsCollectionDto>(HttpStatusCode.OK, new ApiResponse<ServerMapsCollectionDto>(new ServerMapsCollectionDto([]))),
            DefaultBehavior.ReturnError => new ApiResult<ServerMapsCollectionDto>(HttpStatusCode.NotFound, new ApiResponse<ServerMapsCollectionDto>(new ApiError("NOT_FOUND", "Server not found"))),
            _ => throw new InvalidOperationException($"Unknown default behavior: {DefaultResponseBehavior}")
        });
    }

    public Task<ApiResult> PushServerMapToHost(Guid gameServerId, string mapName)
    {
        _operationLog.Add(("PushServerMapToHost", gameServerId, mapName));
        return Task.FromResult(DefaultResponseBehavior switch
        {
            DefaultBehavior.ReturnGenericSuccess => new ApiResult(HttpStatusCode.OK, new ApiResponse()),
            DefaultBehavior.ReturnError => new ApiResult(HttpStatusCode.InternalServerError, new ApiResponse(new ApiError("FAILED", "Operation failed"))),
            _ => throw new InvalidOperationException($"Unknown default behavior: {DefaultResponseBehavior}")
        });
    }

    public Task<ApiResult> DeleteServerMapFromHost(Guid gameServerId, string mapName)
    {
        _operationLog.Add(("DeleteServerMapFromHost", gameServerId, mapName));
        return Task.FromResult(DefaultResponseBehavior switch
        {
            DefaultBehavior.ReturnGenericSuccess => new ApiResult(HttpStatusCode.OK, new ApiResponse()),
            DefaultBehavior.ReturnError => new ApiResult(HttpStatusCode.InternalServerError, new ApiResponse(new ApiError("FAILED", "Operation failed"))),
            _ => throw new InvalidOperationException($"Unknown default behavior: {DefaultResponseBehavior}")
        });
    }
}
