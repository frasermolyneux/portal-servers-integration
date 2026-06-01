using System.Collections.Concurrent;
using System.Net;
using MX.Api.Abstractions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Ftp;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing;

/// <summary>
/// In-memory fake of <see cref="IFileBrowseApi"/> for unit and integration testing.
/// Supports canned responses, error simulation, and call tracking.
/// </summary>
public class FakeFileBrowseApi : IFileBrowseApi
{
    private readonly ConcurrentDictionary<string, ApiResult<FtpDirectoryListingDto>> _browseResponses = new();
    private readonly ConcurrentBag<(string Operation, Guid ServerId, string? Path)> _operationLog = [];

    public IReadOnlyCollection<(string Operation, Guid ServerId, string? Path)> OperationLog => _operationLog.ToArray();

    public DefaultBehavior DefaultResponseBehavior { get; private set; } = DefaultBehavior.ReturnGenericSuccess;

    public FakeFileBrowseApi AddBrowseResponse(Guid gameServerId, string path, FtpDirectoryListingDto dto)
    {
        _browseResponses[$"{gameServerId}:{path}"] = new ApiResult<FtpDirectoryListingDto>(HttpStatusCode.OK, new ApiResponse<FtpDirectoryListingDto>(dto));
        return this;
    }

    public FakeFileBrowseApi SetDefaultBehavior(DefaultBehavior behavior)
    {
        DefaultResponseBehavior = behavior;
        return this;
    }

    public void Reset()
    {
        _browseResponses.Clear();
        _operationLog.Clear();
        DefaultResponseBehavior = DefaultBehavior.ReturnGenericSuccess;
    }

    public Task<ApiResult<FtpDirectoryListingDto>> BrowseDirectory(Guid gameServerId, string? path = null, CancellationToken cancellationToken = default)
    {
        var normalizedPath = path ?? "/";
        _operationLog.Add(("BrowseDirectory", gameServerId, normalizedPath));

        var key = $"{gameServerId}:{normalizedPath}";
        if (_browseResponses.TryGetValue(key, out var result))
            return Task.FromResult(result);

        return Task.FromResult(DefaultResponseBehavior switch
        {
            DefaultBehavior.ReturnGenericSuccess => new ApiResult<FtpDirectoryListingDto>(HttpStatusCode.OK, new ApiResponse<FtpDirectoryListingDto>(new FtpDirectoryListingDto(normalizedPath, null, []))),
            DefaultBehavior.ReturnError => new ApiResult<FtpDirectoryListingDto>(HttpStatusCode.NotFound, new ApiResponse<FtpDirectoryListingDto>(new ApiError("NOT_FOUND", "Server not found"))),
            _ => throw new InvalidOperationException($"Unknown default behavior: {DefaultResponseBehavior}")
        });
    }
}
