using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;

public sealed record FileTransportCredentials(string Hostname, int Port, string Username, string Password, string? HostKeyFingerprint = null, string? MapsRootPath = null);

public sealed record ResolvedFileTransport(
    FileTransportType TransportType,
    string ConfigurationNamespace,
    FileTransportCredentials Credentials)
{
    public string TelemetryType => TransportType.ToString().ToUpperInvariant();
    public string TelemetryTarget => $"{Credentials.Hostname}:{Credentials.Port}";
}

public sealed record FileTransportEntry(
    string Name,
    string FullPath,
    bool IsDirectory,
    long? Size,
    DateTime? Modified);

public interface IGameServerFileTransportSession : IAsyncDisposable
{
    ResolvedFileTransport Transport { get; }

    Task<IReadOnlyList<FileTransportEntry>> GetListing(string path, CancellationToken cancellationToken = default);
    Task<bool> FileExists(string path, CancellationToken cancellationToken = default);
    Task<byte[]> DownloadBytes(string path, CancellationToken cancellationToken = default);
    Task UploadBytes(string path, byte[] content, CancellationToken cancellationToken = default);
    Task UploadStream(string path, Stream content, CancellationToken cancellationToken = default);
    Task<bool> DirectoryExists(string path, CancellationToken cancellationToken = default);
    Task CreateDirectory(string path, CancellationToken cancellationToken = default);
    Task DeleteDirectory(string path, CancellationToken cancellationToken = default);
}

internal interface IFileTransportResolver
{
    Task<MX.Api.Abstractions.ApiResult<ResolvedFileTransport>> Resolve(Guid gameServerId, CancellationToken cancellationToken = default);
}

public interface IGameServerFileTransportFactory
{
    Task<MX.Api.Abstractions.ApiResult<IGameServerFileTransportSession>> CreateSession(Guid gameServerId, CancellationToken cancellationToken = default);
}
