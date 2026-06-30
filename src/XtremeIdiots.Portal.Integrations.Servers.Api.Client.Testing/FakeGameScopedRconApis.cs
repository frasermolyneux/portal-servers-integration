using System.Collections.Concurrent;
using System.Net;
using MX.Api.Abstractions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing;

public abstract class FakeGameScopedRconApiBase
{
    private readonly string _apiName;
    private readonly ConcurrentBag<(string Operation, Guid ServerId, object? Params)> _operationLog = [];

    protected FakeGameScopedRconApiBase(string apiName)
    {
        _apiName = apiName;
    }

    public IReadOnlyCollection<(string Operation, Guid ServerId, object? Params)> OperationLog => _operationLog.ToArray();

    public DefaultBehavior DefaultResponseBehavior { get; private set; } = DefaultBehavior.ReturnGenericSuccess;

    public void SetDefaultBehavior(DefaultBehavior behavior)
    {
        DefaultResponseBehavior = behavior;
    }

    public void Reset()
    {
        _operationLog.Clear();
        DefaultResponseBehavior = DefaultBehavior.ReturnGenericSuccess;
    }

    protected Task<ApiResult<RconCurrentMapDto>> GetCurrentMap(Guid gameServerId)
    {
        _operationLog.Add(($"{_apiName}.GetCurrentMap", gameServerId, null));
        return Task.FromResult(DefaultResponseBehavior switch
        {
            DefaultBehavior.ReturnGenericSuccess => new ApiResult<RconCurrentMapDto>(HttpStatusCode.OK, new ApiResponse<RconCurrentMapDto>(new RconCurrentMapDto("mp_default"))),
            DefaultBehavior.ReturnError => new ApiResult<RconCurrentMapDto>(HttpStatusCode.InternalServerError, new ApiResponse<RconCurrentMapDto>(new ApiError("FAILED", "Operation failed"))),
            _ => throw new InvalidOperationException($"Unknown default behavior: {DefaultResponseBehavior}")
        });
    }

    protected Task<ApiResult<RconStatusResponseDto>> Status(Guid gameServerId)
    {
        _operationLog.Add(($"{_apiName}.Status", gameServerId, null));
        return Task.FromResult(DefaultResponseBehavior switch
        {
            DefaultBehavior.ReturnGenericSuccess => new ApiResult<RconStatusResponseDto>(HttpStatusCode.OK, new ApiResponse<RconStatusResponseDto>(new RconStatusResponseDto())),
            DefaultBehavior.ReturnError => new ApiResult<RconStatusResponseDto>(HttpStatusCode.InternalServerError, new ApiResponse<RconStatusResponseDto>(new ApiError("FAILED", "Operation failed"))),
            _ => throw new InvalidOperationException($"Unknown default behavior: {DefaultResponseBehavior}")
        });
    }

    protected Task<ApiResult<RconMapCollectionDto>> GetMaps(Guid gameServerId)
    {
        _operationLog.Add(($"{_apiName}.GetMaps", gameServerId, null));
        return Task.FromResult(DefaultResponseBehavior switch
        {
            DefaultBehavior.ReturnGenericSuccess => new ApiResult<RconMapCollectionDto>(HttpStatusCode.OK, new ApiResponse<RconMapCollectionDto>(new RconMapCollectionDto([]))),
            DefaultBehavior.ReturnError => new ApiResult<RconMapCollectionDto>(HttpStatusCode.InternalServerError, new ApiResponse<RconMapCollectionDto>(new ApiError("FAILED", "Operation failed"))),
            _ => throw new InvalidOperationException($"Unknown default behavior: {DefaultResponseBehavior}")
        });
    }

    protected Task<ApiResult<string>> ServerInfo(Guid gameServerId)
    {
        _operationLog.Add(($"{_apiName}.ServerInfo", gameServerId, null));
        return Task.FromResult(StringResult("serverinfo"));
    }

    protected Task<ApiResult<string>> SystemInfo(Guid gameServerId)
    {
        _operationLog.Add(($"{_apiName}.SystemInfo", gameServerId, null));
        return Task.FromResult(StringResult("systeminfo"));
    }

    protected Task<ApiResult<string>> CmdList(Guid gameServerId)
    {
        _operationLog.Add(($"{_apiName}.CmdList", gameServerId, null));
        return Task.FromResult(StringResult("cmdlist"));
    }

    protected Task<ApiResult<string>> CvarList(Guid gameServerId)
    {
        _operationLog.Add(($"{_apiName}.CvarList", gameServerId, null));
        return Task.FromResult(StringResult("cvarlist"));
    }

    protected Task<ApiResult<string>> DvarList(Guid gameServerId)
    {
        _operationLog.Add(($"{_apiName}.DvarList", gameServerId, null));
        return Task.FromResult(StringResult("dvarlist"));
    }

    protected Task<ApiResult> Say(Guid gameServerId, SayRequest request)
    {
        _operationLog.Add(($"{_apiName}.Say", gameServerId, request));
        return Task.FromResult(DefaultResponseBehavior switch
        {
            DefaultBehavior.ReturnGenericSuccess => new ApiResult(HttpStatusCode.OK, new ApiResponse()),
            DefaultBehavior.ReturnError => new ApiResult(HttpStatusCode.InternalServerError, new ApiResponse(new ApiError("FAILED", "Operation failed"))),
            _ => throw new InvalidOperationException($"Unknown default behavior: {DefaultResponseBehavior}")
        });
    }

    protected Task<ApiResult<string>> Map(Guid gameServerId, ChangeMapRequest request)
    {
        _operationLog.Add(($"{_apiName}.Map", gameServerId, request));
        return Task.FromResult(StringResult("map ok"));
    }

    protected Task<ApiResult<string>> Kick(Guid gameServerId, ClientSlotRequest request)
    {
        _operationLog.Add(($"{_apiName}.Kick", gameServerId, request));
        return Task.FromResult(StringResult("kick ok"));
    }

    protected Task<ApiResult<string>> TempBan(Guid gameServerId, ClientSlotRequest request)
    {
        _operationLog.Add(($"{_apiName}.TempBan", gameServerId, request));
        return Task.FromResult(StringResult("tempban ok"));
    }

    protected Task<ApiResult<string>> Ban(Guid gameServerId, ClientSlotRequest request)
    {
        _operationLog.Add(($"{_apiName}.Ban", gameServerId, request));
        return Task.FromResult(StringResult("ban ok"));
    }

    protected Task<ApiResult<string>> Set(Guid gameServerId, SetDvarRequest request)
    {
        _operationLog.Add(($"{_apiName}.Set", gameServerId, request));
        return Task.FromResult(StringResult("set ok"));
    }

    protected Task<ApiResult<string>> Seta(Guid gameServerId, SetDvarRequest request)
    {
        _operationLog.Add(($"{_apiName}.Seta", gameServerId, request));
        return Task.FromResult(StringResult("seta ok"));
    }

    protected Task<ApiResult<string>> Restart(Guid gameServerId)
    {
        _operationLog.Add(($"{_apiName}.Restart", gameServerId, null));
        return Task.FromResult(StringResult("restart ok"));
    }

    protected Task<ApiResult<string>> RestartMap(Guid gameServerId)
    {
        _operationLog.Add(($"{_apiName}.RestartMap", gameServerId, null));
        return Task.FromResult(StringResult("restart map ok"));
    }

    protected Task<ApiResult<string>> FastRestartMap(Guid gameServerId)
    {
        _operationLog.Add(($"{_apiName}.FastRestartMap", gameServerId, null));
        return Task.FromResult(StringResult("fast restart map ok"));
    }

    protected Task<ApiResult<string>> NextMap(Guid gameServerId)
    {
        _operationLog.Add(($"{_apiName}.NextMap", gameServerId, null));
        return Task.FromResult(StringResult("next map ok"));
    }

    private ApiResult<string> StringResult(string value)
    {
        return DefaultResponseBehavior switch
        {
            DefaultBehavior.ReturnGenericSuccess => new ApiResult<string>(HttpStatusCode.OK, new ApiResponse<string>(value)),
            DefaultBehavior.ReturnError => new ApiResult<string>(HttpStatusCode.InternalServerError, new ApiResponse<string>(new ApiError("FAILED", "Operation failed"))),
            _ => throw new InvalidOperationException($"Unknown default behavior: {DefaultResponseBehavior}")
        };
    }
}

public sealed class FakeCod2RconApi : FakeGameScopedRconApiBase, ICod2RconApi
{
    public FakeCod2RconApi() : base("Cod2") { }
    public Task<ApiResult<RconCurrentMapDto>> GetCurrentMap(Guid gameServerId, CancellationToken cancellationToken = default) => base.GetCurrentMap(gameServerId);
    public Task<ApiResult<RconStatusResponseDto>> Status(Guid gameServerId, CancellationToken cancellationToken = default) => base.Status(gameServerId);
    public Task<ApiResult<RconMapCollectionDto>> GetMaps(Guid gameServerId, CancellationToken cancellationToken = default) => base.GetMaps(gameServerId);
    public Task<ApiResult<string>> ServerInfo(Guid gameServerId, CancellationToken cancellationToken = default) => base.ServerInfo(gameServerId);
    public Task<ApiResult<string>> SystemInfo(Guid gameServerId, CancellationToken cancellationToken = default) => base.SystemInfo(gameServerId);
    public Task<ApiResult<string>> CmdList(Guid gameServerId, CancellationToken cancellationToken = default) => base.CmdList(gameServerId);
    public Task<ApiResult<string>> CvarList(Guid gameServerId, CancellationToken cancellationToken = default) => base.CvarList(gameServerId);
    public Task<ApiResult<string>> DvarList(Guid gameServerId, CancellationToken cancellationToken = default) => base.DvarList(gameServerId);
    public Task<ApiResult> Say(Guid gameServerId, SayRequest request, CancellationToken cancellationToken = default) => base.Say(gameServerId, request);
    public Task<ApiResult<string>> Map(Guid gameServerId, ChangeMapRequest request, CancellationToken cancellationToken = default) => base.Map(gameServerId, request);
    public Task<ApiResult<string>> Kick(Guid gameServerId, ClientSlotRequest request, CancellationToken cancellationToken = default) => base.Kick(gameServerId, request);
    public Task<ApiResult<string>> TempBan(Guid gameServerId, ClientSlotRequest request, CancellationToken cancellationToken = default) => base.TempBan(gameServerId, request);
    public Task<ApiResult<string>> Ban(Guid gameServerId, ClientSlotRequest request, CancellationToken cancellationToken = default) => base.Ban(gameServerId, request);
    public Task<ApiResult<string>> Set(Guid gameServerId, SetDvarRequest request, CancellationToken cancellationToken = default) => base.Set(gameServerId, request);
    public Task<ApiResult<string>> Seta(Guid gameServerId, SetDvarRequest request, CancellationToken cancellationToken = default) => base.Seta(gameServerId, request);
    public Task<ApiResult<string>> Restart(Guid gameServerId, CancellationToken cancellationToken = default) => base.Restart(gameServerId);
    public Task<ApiResult<string>> RestartMap(Guid gameServerId, CancellationToken cancellationToken = default) => base.RestartMap(gameServerId);
    public Task<ApiResult<string>> FastRestartMap(Guid gameServerId, CancellationToken cancellationToken = default) => base.FastRestartMap(gameServerId);
    public Task<ApiResult<string>> NextMap(Guid gameServerId, CancellationToken cancellationToken = default) => base.NextMap(gameServerId);
}

public sealed class FakeCod4RconApi : FakeGameScopedRconApiBase, ICod4RconApi
{
    public FakeCod4RconApi() : base("Cod4") { }
    public Task<ApiResult<RconCurrentMapDto>> GetCurrentMap(Guid gameServerId, CancellationToken cancellationToken = default) => base.GetCurrentMap(gameServerId);
    public Task<ApiResult<RconStatusResponseDto>> Status(Guid gameServerId, CancellationToken cancellationToken = default) => base.Status(gameServerId);
    public Task<ApiResult<RconMapCollectionDto>> GetMaps(Guid gameServerId, CancellationToken cancellationToken = default) => base.GetMaps(gameServerId);
    public Task<ApiResult<string>> ServerInfo(Guid gameServerId, CancellationToken cancellationToken = default) => base.ServerInfo(gameServerId);
    public Task<ApiResult<string>> SystemInfo(Guid gameServerId, CancellationToken cancellationToken = default) => base.SystemInfo(gameServerId);
    public Task<ApiResult<string>> CmdList(Guid gameServerId, CancellationToken cancellationToken = default) => base.CmdList(gameServerId);
    public Task<ApiResult<string>> CvarList(Guid gameServerId, CancellationToken cancellationToken = default) => base.CvarList(gameServerId);
    public Task<ApiResult<string>> DvarList(Guid gameServerId, CancellationToken cancellationToken = default) => base.DvarList(gameServerId);
    public Task<ApiResult> Say(Guid gameServerId, SayRequest request, CancellationToken cancellationToken = default) => base.Say(gameServerId, request);
    public Task<ApiResult<string>> Map(Guid gameServerId, ChangeMapRequest request, CancellationToken cancellationToken = default) => base.Map(gameServerId, request);
    public Task<ApiResult<string>> Kick(Guid gameServerId, ClientSlotRequest request, CancellationToken cancellationToken = default) => base.Kick(gameServerId, request);
    public Task<ApiResult<string>> TempBan(Guid gameServerId, ClientSlotRequest request, CancellationToken cancellationToken = default) => base.TempBan(gameServerId, request);
    public Task<ApiResult<string>> Ban(Guid gameServerId, ClientSlotRequest request, CancellationToken cancellationToken = default) => base.Ban(gameServerId, request);
    public Task<ApiResult<string>> Set(Guid gameServerId, SetDvarRequest request, CancellationToken cancellationToken = default) => base.Set(gameServerId, request);
    public Task<ApiResult<string>> Seta(Guid gameServerId, SetDvarRequest request, CancellationToken cancellationToken = default) => base.Seta(gameServerId, request);
    public Task<ApiResult<string>> Restart(Guid gameServerId, CancellationToken cancellationToken = default) => base.Restart(gameServerId);
    public Task<ApiResult<string>> RestartMap(Guid gameServerId, CancellationToken cancellationToken = default) => base.RestartMap(gameServerId);
    public Task<ApiResult<string>> FastRestartMap(Guid gameServerId, CancellationToken cancellationToken = default) => base.FastRestartMap(gameServerId);
    public Task<ApiResult<string>> NextMap(Guid gameServerId, CancellationToken cancellationToken = default) => base.NextMap(gameServerId);
}

public sealed class FakeCod5RconApi : FakeGameScopedRconApiBase, ICod5RconApi
{
    public FakeCod5RconApi() : base("Cod5") { }
    public Task<ApiResult<RconCurrentMapDto>> GetCurrentMap(Guid gameServerId, CancellationToken cancellationToken = default) => base.GetCurrentMap(gameServerId);
    public Task<ApiResult<RconStatusResponseDto>> Status(Guid gameServerId, CancellationToken cancellationToken = default) => base.Status(gameServerId);
    public Task<ApiResult<RconMapCollectionDto>> GetMaps(Guid gameServerId, CancellationToken cancellationToken = default) => base.GetMaps(gameServerId);
    public Task<ApiResult<string>> ServerInfo(Guid gameServerId, CancellationToken cancellationToken = default) => base.ServerInfo(gameServerId);
    public Task<ApiResult<string>> SystemInfo(Guid gameServerId, CancellationToken cancellationToken = default) => base.SystemInfo(gameServerId);
    public Task<ApiResult<string>> CmdList(Guid gameServerId, CancellationToken cancellationToken = default) => base.CmdList(gameServerId);
    public Task<ApiResult<string>> CvarList(Guid gameServerId, CancellationToken cancellationToken = default) => base.CvarList(gameServerId);
    public Task<ApiResult<string>> DvarList(Guid gameServerId, CancellationToken cancellationToken = default) => base.DvarList(gameServerId);
    public Task<ApiResult> Say(Guid gameServerId, SayRequest request, CancellationToken cancellationToken = default) => base.Say(gameServerId, request);
    public Task<ApiResult<string>> Map(Guid gameServerId, ChangeMapRequest request, CancellationToken cancellationToken = default) => base.Map(gameServerId, request);
    public Task<ApiResult<string>> Kick(Guid gameServerId, ClientSlotRequest request, CancellationToken cancellationToken = default) => base.Kick(gameServerId, request);
    public Task<ApiResult<string>> TempBan(Guid gameServerId, ClientSlotRequest request, CancellationToken cancellationToken = default) => base.TempBan(gameServerId, request);
    public Task<ApiResult<string>> Ban(Guid gameServerId, ClientSlotRequest request, CancellationToken cancellationToken = default) => base.Ban(gameServerId, request);
    public Task<ApiResult<string>> Set(Guid gameServerId, SetDvarRequest request, CancellationToken cancellationToken = default) => base.Set(gameServerId, request);
    public Task<ApiResult<string>> Seta(Guid gameServerId, SetDvarRequest request, CancellationToken cancellationToken = default) => base.Seta(gameServerId, request);
    public Task<ApiResult<string>> Restart(Guid gameServerId, CancellationToken cancellationToken = default) => base.Restart(gameServerId);
    public Task<ApiResult<string>> RestartMap(Guid gameServerId, CancellationToken cancellationToken = default) => base.RestartMap(gameServerId);
    public Task<ApiResult<string>> FastRestartMap(Guid gameServerId, CancellationToken cancellationToken = default) => base.FastRestartMap(gameServerId);
    public Task<ApiResult<string>> NextMap(Guid gameServerId, CancellationToken cancellationToken = default) => base.NextMap(gameServerId);
}

public sealed class FakeInsurgencyRconApi : FakeGameScopedRconApiBase, IInsurgencyRconApi
{
    public FakeInsurgencyRconApi() : base("Insurgency") { }
    public Task<ApiResult<RconCurrentMapDto>> GetCurrentMap(Guid gameServerId, CancellationToken cancellationToken = default) => base.GetCurrentMap(gameServerId);
    public Task<ApiResult> Say(Guid gameServerId, SayRequest request, CancellationToken cancellationToken = default) => base.Say(gameServerId, request);
}

public sealed class FakeRustRconApi : FakeGameScopedRconApiBase, IRustRconApi
{
    public FakeRustRconApi() : base("Rust") { }
    public Task<ApiResult<RconCurrentMapDto>> GetCurrentMap(Guid gameServerId, CancellationToken cancellationToken = default) => base.GetCurrentMap(gameServerId);
    public Task<ApiResult> Say(Guid gameServerId, SayRequest request, CancellationToken cancellationToken = default) => base.Say(gameServerId, request);
}

public sealed class FakeL4d2RconApi : FakeGameScopedRconApiBase, IL4d2RconApi
{
    public FakeL4d2RconApi() : base("L4d2") { }
    public Task<ApiResult<RconCurrentMapDto>> GetCurrentMap(Guid gameServerId, CancellationToken cancellationToken = default) => base.GetCurrentMap(gameServerId);
    public Task<ApiResult> Say(Guid gameServerId, SayRequest request, CancellationToken cancellationToken = default) => base.Say(gameServerId, request);
}