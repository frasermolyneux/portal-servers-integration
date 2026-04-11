using System.Collections.Concurrent;
using System.Net;
using MX.Api.Abstractions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Config;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing;

/// <summary>
/// In-memory fake of <see cref="IConfigApi"/> for unit and integration testing.
/// Supports canned responses, error simulation, and call tracking.
/// </summary>
public class FakeConfigApi : IConfigApi
{
    private readonly ConcurrentDictionary<(Guid ServerId, string FilePath), ApiResult<ConfigFileContentDto>> _configFileResponses = new();
    private readonly ConcurrentBag<(string Operation, Guid ServerId, object? Params)> _operationLog = [];

    public IReadOnlyCollection<(string Operation, Guid ServerId, object? Params)> OperationLog => _operationLog.ToArray();

    public DefaultBehavior DefaultResponseBehavior { get; private set; } = DefaultBehavior.ReturnGenericSuccess;

    public FakeConfigApi AddConfigFileResponse(Guid gameServerId, string filePath, ConfigFileContentDto dto)
    {
        _configFileResponses[(gameServerId, filePath)] = new ApiResult<ConfigFileContentDto>(HttpStatusCode.OK, new ApiResponse<ConfigFileContentDto>(dto));
        return this;
    }

    public FakeConfigApi SetDefaultBehavior(DefaultBehavior behavior)
    {
        DefaultResponseBehavior = behavior;
        return this;
    }

    public void Reset()
    {
        _configFileResponses.Clear();
        _operationLog.Clear();
        DefaultResponseBehavior = DefaultBehavior.ReturnGenericSuccess;
    }

    public Task<ApiResult<ConfigFileContentDto>> GetConfigFile(Guid gameServerId, string filePath, CancellationToken cancellationToken = default)
    {
        _operationLog.Add(("GetConfigFile", gameServerId, new { filePath }));

        if (_configFileResponses.TryGetValue((gameServerId, filePath), out var result))
            return Task.FromResult(result);

        return Task.FromResult(DefaultResponseBehavior switch
        {
            DefaultBehavior.ReturnGenericSuccess => new ApiResult<ConfigFileContentDto>(HttpStatusCode.OK, new ApiResponse<ConfigFileContentDto>(new ConfigFileContentDto(filePath, ""))),
            DefaultBehavior.ReturnError => new ApiResult<ConfigFileContentDto>(HttpStatusCode.NotFound, new ApiResponse<ConfigFileContentDto>(new ApiError("NOT_FOUND", "Config file not found"))),
            _ => throw new InvalidOperationException($"Unknown default behavior: {DefaultResponseBehavior}")
        });
    }

    public Task<ApiResult> UpdateConfigVariable(Guid gameServerId, string filePath, string variableName, string value, string[]? commentLines = null, CancellationToken cancellationToken = default)
    {
        _operationLog.Add(("UpdateConfigVariable", gameServerId, new { filePath, variableName, value, commentLines }));
        return Task.FromResult(DefaultResponseBehavior switch
        {
            DefaultBehavior.ReturnGenericSuccess => new ApiResult(HttpStatusCode.OK, new ApiResponse()),
            DefaultBehavior.ReturnError => new ApiResult(HttpStatusCode.InternalServerError, new ApiResponse(new ApiError("FAILED", "Operation failed"))),
            _ => throw new InvalidOperationException($"Unknown default behavior: {DefaultResponseBehavior}")
        });
    }
}
