using System.Collections.Concurrent;
using System.Net;
using MX.Api.Abstractions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing;

/// <summary>
/// In-memory fake of <see cref="IRconApi"/> for unit and integration testing.
/// Supports canned responses, error simulation, and call tracking.
/// </summary>
public class FakeRconApi : IRconApi
{
    private readonly ConcurrentDictionary<Guid, ApiResult<ServerRconStatusResponseDto>> _statusResponses = new();
    private readonly ConcurrentDictionary<Guid, ApiResult<RconMapCollectionDto>> _mapsResponses = new();
    private readonly ConcurrentDictionary<Guid, ApiResult<RconCurrentMapDto>> _currentMapResponses = new();
    private readonly ConcurrentBag<(string Operation, Guid ServerId, object? Params)> _operationLog = [];

    public IReadOnlyCollection<(string Operation, Guid ServerId, object? Params)> OperationLog => _operationLog.ToArray();

    public DefaultBehavior DefaultResponseBehavior { get; private set; } = DefaultBehavior.ReturnGenericSuccess;

    public FakeRconApi AddStatusResponse(Guid gameServerId, ServerRconStatusResponseDto dto)
    {
        _statusResponses[gameServerId] = new ApiResult<ServerRconStatusResponseDto>(HttpStatusCode.OK, new ApiResponse<ServerRconStatusResponseDto>(dto));
        return this;
    }

    public FakeRconApi AddMapsResponse(Guid gameServerId, RconMapCollectionDto dto)
    {
        _mapsResponses[gameServerId] = new ApiResult<RconMapCollectionDto>(HttpStatusCode.OK, new ApiResponse<RconMapCollectionDto>(dto));
        return this;
    }

    public FakeRconApi AddCurrentMapResponse(Guid gameServerId, RconCurrentMapDto dto)
    {
        _currentMapResponses[gameServerId] = new ApiResult<RconCurrentMapDto>(HttpStatusCode.OK, new ApiResponse<RconCurrentMapDto>(dto));
        return this;
    }

    public FakeRconApi SetDefaultBehavior(DefaultBehavior behavior)
    {
        DefaultResponseBehavior = behavior;
        return this;
    }

    public void Reset()
    {
        _statusResponses.Clear();
        _mapsResponses.Clear();
        _currentMapResponses.Clear();
        _operationLog.Clear();
        DefaultResponseBehavior = DefaultBehavior.ReturnGenericSuccess;
    }

    public Task<ApiResult<ServerRconStatusResponseDto>> GetServerStatus(Guid gameServerId)
    {
        _operationLog.Add(("GetServerStatus", gameServerId, null));

        if (_statusResponses.TryGetValue(gameServerId, out var result))
            return Task.FromResult(result);

        return Task.FromResult(DefaultResponseBehavior switch
        {
            DefaultBehavior.ReturnGenericSuccess => new ApiResult<ServerRconStatusResponseDto>(HttpStatusCode.OK, new ApiResponse<ServerRconStatusResponseDto>(ServersDtoFactory.CreateRconStatusResponse())),
            DefaultBehavior.ReturnError => new ApiResult<ServerRconStatusResponseDto>(HttpStatusCode.NotFound, new ApiResponse<ServerRconStatusResponseDto>(new ApiError("NOT_FOUND", "Server not found"))),
            _ => throw new InvalidOperationException($"Unknown default behavior: {DefaultResponseBehavior}")
        });
    }

    public Task<ApiResult<RconMapCollectionDto>> GetServerMaps(Guid gameServerId)
    {
        _operationLog.Add(("GetServerMaps", gameServerId, null));

        if (_mapsResponses.TryGetValue(gameServerId, out var result))
            return Task.FromResult(result);

        return Task.FromResult(DefaultResponseBehavior switch
        {
            DefaultBehavior.ReturnGenericSuccess => new ApiResult<RconMapCollectionDto>(HttpStatusCode.OK, new ApiResponse<RconMapCollectionDto>(new RconMapCollectionDto([]))),
            DefaultBehavior.ReturnError => new ApiResult<RconMapCollectionDto>(HttpStatusCode.NotFound, new ApiResponse<RconMapCollectionDto>(new ApiError("NOT_FOUND", "Server not found"))),
            _ => throw new InvalidOperationException($"Unknown default behavior: {DefaultResponseBehavior}")
        });
    }

    public Task<ApiResult<RconCurrentMapDto>> GetCurrentMap(Guid gameServerId)
    {
        _operationLog.Add(("GetCurrentMap", gameServerId, null));

        if (_currentMapResponses.TryGetValue(gameServerId, out var result))
            return Task.FromResult(result);

        return Task.FromResult(DefaultResponseBehavior switch
        {
            DefaultBehavior.ReturnGenericSuccess => new ApiResult<RconCurrentMapDto>(HttpStatusCode.OK, new ApiResponse<RconCurrentMapDto>(new RconCurrentMapDto("mp_default"))),
            DefaultBehavior.ReturnError => new ApiResult<RconCurrentMapDto>(HttpStatusCode.NotFound, new ApiResponse<RconCurrentMapDto>(new ApiError("NOT_FOUND", "Server not found"))),
            _ => throw new InvalidOperationException($"Unknown default behavior: {DefaultResponseBehavior}")
        });
    }

    private Task<ApiResult> LogAndReturnSuccess(string operation, Guid gameServerId, object? parameters = null)
    {
        _operationLog.Add((operation, gameServerId, parameters));
        return Task.FromResult(DefaultResponseBehavior switch
        {
            DefaultBehavior.ReturnGenericSuccess => new ApiResult(HttpStatusCode.OK, new ApiResponse()),
            DefaultBehavior.ReturnError => new ApiResult(HttpStatusCode.InternalServerError, new ApiResponse(new ApiError("FAILED", "Operation failed"))),
            _ => throw new InvalidOperationException($"Unknown default behavior: {DefaultResponseBehavior}")
        });
    }

    private Task<ApiResult<string>> LogAndReturnStringSuccess(string operation, Guid gameServerId, string defaultValue)
    {
        _operationLog.Add((operation, gameServerId, null));
        return Task.FromResult(DefaultResponseBehavior switch
        {
            DefaultBehavior.ReturnGenericSuccess => new ApiResult<string>(HttpStatusCode.OK, new ApiResponse<string>(defaultValue)),
            DefaultBehavior.ReturnError => new ApiResult<string>(HttpStatusCode.InternalServerError, new ApiResponse<string>(new ApiError("FAILED", "Operation failed"))),
            _ => throw new InvalidOperationException($"Unknown default behavior: {DefaultResponseBehavior}")
        });
    }

    public Task<ApiResult> KickPlayer(Guid gameServerId, int clientId) =>
        LogAndReturnSuccess("KickPlayer", gameServerId, new { clientId });

    public Task<ApiResult> BanPlayer(Guid gameServerId, int clientId) =>
        LogAndReturnSuccess("BanPlayer", gameServerId, new { clientId });

    public Task<ApiResult> Restart(Guid gameServerId) =>
        LogAndReturnSuccess("Restart", gameServerId);

    public Task<ApiResult> RestartMap(Guid gameServerId) =>
        LogAndReturnSuccess("RestartMap", gameServerId);

    public Task<ApiResult> FastRestartMap(Guid gameServerId) =>
        LogAndReturnSuccess("FastRestartMap", gameServerId);

    public Task<ApiResult> NextMap(Guid gameServerId) =>
        LogAndReturnSuccess("NextMap", gameServerId);

    public Task<ApiResult> Say(Guid gameServerId, string message) =>
        LogAndReturnSuccess("Say", gameServerId, new { message });

    public Task<ApiResult> TellPlayer(Guid gameServerId, int clientId, string message) =>
        LogAndReturnSuccess("TellPlayer", gameServerId, new { clientId, message });

    public Task<ApiResult> ChangeMap(Guid gameServerId, string mapName) =>
        LogAndReturnSuccess("ChangeMap", gameServerId, new { mapName });

    public Task<ApiResult> KickPlayerByName(Guid gameServerId, string name) =>
        LogAndReturnSuccess("KickPlayerByName", gameServerId, new { name });

    public Task<ApiResult> KickAllPlayers(Guid gameServerId) =>
        LogAndReturnSuccess("KickAllPlayers", gameServerId);

    public Task<ApiResult> BanPlayerByName(Guid gameServerId, string name) =>
        LogAndReturnSuccess("BanPlayerByName", gameServerId, new { name });

    public Task<ApiResult> TempBanPlayer(Guid gameServerId, int clientId) =>
        LogAndReturnSuccess("TempBanPlayer", gameServerId, new { clientId });

    public Task<ApiResult> TempBanPlayerByName(Guid gameServerId, string name) =>
        LogAndReturnSuccess("TempBanPlayerByName", gameServerId, new { name });

    public Task<ApiResult> UnbanPlayer(Guid gameServerId, string name) =>
        LogAndReturnSuccess("UnbanPlayer", gameServerId, new { name });

    public Task<ApiResult<string>> GetServerInfo(Guid gameServerId) =>
        LogAndReturnStringSuccess("GetServerInfo", gameServerId, "sv_hostname: Test Server");

    public Task<ApiResult<string>> GetSystemInfo(Guid gameServerId) =>
        LogAndReturnStringSuccess("GetSystemInfo", gameServerId, "System: Test");

    public Task<ApiResult<string>> GetCommandList(Guid gameServerId) =>
        LogAndReturnStringSuccess("GetCommandList", gameServerId, "kick\nban\nstatus");

    public Task<ApiResult> KickPlayerWithVerification(Guid gameServerId, int clientId, string? expectedPlayerName) =>
        LogAndReturnSuccess("KickPlayerWithVerification", gameServerId, new { clientId, expectedPlayerName });

    public Task<ApiResult> BanPlayerWithVerification(Guid gameServerId, int clientId, string? expectedPlayerName) =>
        LogAndReturnSuccess("BanPlayerWithVerification", gameServerId, new { clientId, expectedPlayerName });

    public Task<ApiResult> TempBanPlayerWithVerification(Guid gameServerId, int clientId, string? expectedPlayerName) =>
        LogAndReturnSuccess("TempBanPlayerWithVerification", gameServerId, new { clientId, expectedPlayerName });

    public Task<ApiResult> TellPlayerWithVerification(Guid gameServerId, int clientId, string message, string? expectedPlayerName) =>
        LogAndReturnSuccess("TellPlayerWithVerification", gameServerId, new { clientId, message, expectedPlayerName });
}
