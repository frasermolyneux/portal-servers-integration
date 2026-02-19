using System.Collections.Concurrent;
using System.Net;
using MX.Api.Abstractions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing;

/// <summary>
/// In-memory fake of <see cref="IQueryApi"/> for unit and integration testing.
/// Supports canned responses, error simulation, and call tracking.
/// </summary>
public class FakeQueryApi : IQueryApi
{
    private readonly ConcurrentDictionary<Guid, ApiResult<ServerQueryStatusResponseDto>> _responses = new();
    private readonly ConcurrentDictionary<Guid, ApiResult<ServerQueryStatusResponseDto>> _errorResponses = new();
    private readonly ConcurrentBag<Guid> _queriedServerIds = [];

    public IReadOnlyCollection<Guid> QueriedServerIds => _queriedServerIds.ToArray();

    public DefaultBehavior DefaultResponseBehavior { get; private set; } = DefaultBehavior.ReturnGenericSuccess;

    public FakeQueryApi AddResponse(Guid gameServerId, ServerQueryStatusResponseDto dto)
    {
        _responses[gameServerId] = new ApiResult<ServerQueryStatusResponseDto>(HttpStatusCode.OK, new ApiResponse<ServerQueryStatusResponseDto>(dto));
        return this;
    }

    public FakeQueryApi AddErrorResponse(Guid gameServerId, HttpStatusCode statusCode, string errorCode, string message)
    {
        _errorResponses[gameServerId] = new ApiResult<ServerQueryStatusResponseDto>(statusCode, new ApiResponse<ServerQueryStatusResponseDto>(new ApiError(errorCode, message)));
        return this;
    }

    public FakeQueryApi SetDefaultBehavior(DefaultBehavior behavior)
    {
        DefaultResponseBehavior = behavior;
        return this;
    }

    public void Reset()
    {
        _responses.Clear();
        _errorResponses.Clear();
        _queriedServerIds.Clear();
        DefaultResponseBehavior = DefaultBehavior.ReturnGenericSuccess;
    }

    public Task<ApiResult<ServerQueryStatusResponseDto>> GetServerStatus(Guid gameServerId)
    {
        _queriedServerIds.Add(gameServerId);

        if (_errorResponses.TryGetValue(gameServerId, out var errorResult))
            return Task.FromResult(errorResult);

        if (_responses.TryGetValue(gameServerId, out var result))
            return Task.FromResult(result);

        return Task.FromResult(DefaultResponseBehavior switch
        {
            DefaultBehavior.ReturnGenericSuccess => new ApiResult<ServerQueryStatusResponseDto>(HttpStatusCode.OK, new ApiResponse<ServerQueryStatusResponseDto>(ServersDtoFactory.CreateQueryStatusResponse())),
            DefaultBehavior.ReturnError => new ApiResult<ServerQueryStatusResponseDto>(HttpStatusCode.NotFound, new ApiResponse<ServerQueryStatusResponseDto>(new ApiError("NOT_FOUND", "Server not found"))),
            _ => throw new InvalidOperationException($"Unknown default behavior: {DefaultResponseBehavior}")
        });
    }
}
