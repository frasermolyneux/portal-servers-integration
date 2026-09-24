using System.Security.Cryptography;
using System.Text;

using FluentFTP;
using MX.Api.Abstractions;
using MX.Api.Web.Extensions;
using Renci.SshNet;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Constants;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Settings.Contracts.V1.Contracts.FileTransport;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;

internal sealed class GameServerFileTransportFactory(
    IFileTransportResolver fileTransportResolver,
    IConfiguration configuration,
    ILogger<GameServerFileTransportFactory> logger) : IGameServerFileTransportFactory
{
    public async Task<ApiResult<IGameServerFileTransportSession>> CreateSession(Guid gameServerId, CancellationToken cancellationToken = default)
    {
        var resolution = await fileTransportResolver.Resolve(gameServerId, cancellationToken).ConfigureAwait(false);
        if (!resolution.IsSuccess || resolution.Result?.Data == null)
        {
            return new ApiResult<IGameServerFileTransportSession>(
                resolution.StatusCode,
                new ApiResponse<IGameServerFileTransportSession>(resolution.Result?.Errors ?? []));
        }

        try
        {
            IGameServerFileTransportSession session = resolution.Result.Data.TransportType switch
            {
                FileTransportType.Sftp => await SftpGameServerFileTransportSession.CreateAsync(resolution.Result.Data, cancellationToken).ConfigureAwait(false),
                _ => await FtpGameServerFileTransportSession.CreateAsync(resolution.Result.Data, configuration, cancellationToken).ConfigureAwait(false),
            };

            return new ApiResponse<IGameServerFileTransportSession>(session).ToApiResult();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to establish {TransportType} session for server {GameServerId}", resolution.Result.Data.TransportType, gameServerId);
            return new ApiResponse<IGameServerFileTransportSession>(
                    new ApiError(ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, "Failed to connect to the game server file transport host."))
                .ToApiResult();
        }
    }

    internal static Renci.SshNet.ConnectionInfo CreateSftpConnectionInfo(FileTransportCredentials credentials)
    {
        AuthenticationMethod authenticationMethod = credentials.AuthenticationType switch
        {
            SftpAuthenticationType.Password => new PasswordAuthenticationMethod(credentials.Username, credentials.Password),
            SftpAuthenticationType.PrivateKey => CreatePrivateKeyAuthenticationMethod(credentials),
            _ => throw new InvalidOperationException($"Unsupported SFTP authentication type '{credentials.AuthenticationType}'."),
        };

        return new Renci.SshNet.ConnectionInfo(
            credentials.Hostname,
            credentials.Port,
            credentials.Username,
            authenticationMethod);
    }

    private static PrivateKeyAuthenticationMethod CreatePrivateKeyAuthenticationMethod(FileTransportCredentials credentials)
    {
        if (string.IsNullOrWhiteSpace(credentials.PrivateKey))
        {
            throw new InvalidOperationException("The sftp.privateKey setting is required for private-key authentication.");
        }

        var privateKeyBytes = Encoding.UTF8.GetBytes(credentials.PrivateKey);
        try
        {
            using var privateKeyStream = new MemoryStream(privateKeyBytes, writable: false);
            var privateKeyFile = string.IsNullOrEmpty(credentials.PrivateKeyPassphrase)
                ? new PrivateKeyFile(privateKeyStream)
                : new PrivateKeyFile(privateKeyStream, credentials.PrivateKeyPassphrase);

            try
            {
                return new PrivateKeyAuthenticationMethod(credentials.Username, privateKeyFile);
            }
            catch
            {
                privateKeyFile.Dispose();
                throw;
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKeyBytes);
        }
    }

    private sealed class FtpGameServerFileTransportSession : IGameServerFileTransportSession
    {
        private readonly AsyncFtpClient _client;

        private FtpGameServerFileTransportSession(ResolvedFileTransport transport, AsyncFtpClient client)
        {
            Transport = transport;
            _client = client;
        }

        public ResolvedFileTransport Transport { get; }

        public static async Task<FtpGameServerFileTransportSession> CreateAsync(ResolvedFileTransport transport, IConfiguration configuration, CancellationToken cancellationToken)
        {
            var client = new AsyncFtpClient(
                transport.Credentials.Hostname,
                transport.Credentials.Username,
                transport.Credentials.Password,
                transport.Credentials.Port);

            client.Config.ConnectTimeout = 10000;
            client.Config.ReadTimeout = 10000;
            client.Config.DataConnectionConnectTimeout = 10000;
            client.Config.DataConnectionReadTimeout = 10000;
            client.ValidateCertificate += (_, e) =>
            {
                if (e.Certificate.GetCertHashString().Equals(configuration["xtremeidiots_ftp_certificate_thumbprint"], StringComparison.OrdinalIgnoreCase))
                {
                    e.Accept = true;
                }
            };

            try
            {
                await client.AutoConnect(cancellationToken).ConfigureAwait(false);
                return new FtpGameServerFileTransportSession(transport, client);
            }
            catch
            {
                client.Dispose();
                throw;
            }
        }

        public async Task<IReadOnlyList<FileTransportEntry>> GetListing(string path, CancellationToken cancellationToken = default)
        {
            var items = await _client.GetListing(path, cancellationToken).ConfigureAwait(false);
            return items
                .Where(item => item.Name != "." && item.Name != "..")
                .Where(item => item.Type == FtpObjectType.File || item.Type == FtpObjectType.Directory)
                .Select(item => new FileTransportEntry(
                    item.Name,
                    item.FullName,
                    item.Type == FtpObjectType.Directory,
                    item.Type == FtpObjectType.File ? item.Size : null,
                    item.Modified != DateTime.MinValue ? item.Modified : null))
                .ToList();
        }

        public Task<bool> FileExists(string path, CancellationToken cancellationToken = default)
            => _client.FileExists(path, cancellationToken);

        public Task<byte[]> DownloadBytes(string path, CancellationToken cancellationToken = default)
            => _client.DownloadBytes(path, cancellationToken);

        public async Task UploadBytes(string path, byte[] content, CancellationToken cancellationToken = default)
        {
            await using var stream = new MemoryStream(content);
            await _client.UploadStream(stream, path, FtpRemoteExists.Overwrite, true, null, cancellationToken).ConfigureAwait(false);
        }

        public async Task UploadStream(string path, Stream content, CancellationToken cancellationToken = default)
        {
            if (content.CanSeek)
            {
                content.Position = 0;
            }

            await _client.UploadStream(content, path, FtpRemoteExists.Overwrite, true, null, cancellationToken).ConfigureAwait(false);
        }

        public Task DeleteFile(string path, CancellationToken cancellationToken = default)
            => _client.DeleteFile(path, cancellationToken);

        public Task<bool> DirectoryExists(string path, CancellationToken cancellationToken = default)
            => _client.DirectoryExists(path, cancellationToken);

        public Task CreateDirectory(string path, CancellationToken cancellationToken = default)
            => _client.CreateDirectory(path);

        public Task DeleteDirectory(string path, CancellationToken cancellationToken = default)
            => _client.DeleteDirectory(path);

        public ValueTask DisposeAsync() => _client.DisposeAsync();
    }

    private sealed class SftpGameServerFileTransportSession : IGameServerFileTransportSession
    {
        private readonly SftpClient _client;
        private readonly AuthenticationMethod _authenticationMethod;

        private SftpGameServerFileTransportSession(
            ResolvedFileTransport transport,
            SftpClient client,
            AuthenticationMethod authenticationMethod)
        {
            Transport = transport;
            _client = client;
            _authenticationMethod = authenticationMethod;
        }

        public ResolvedFileTransport Transport { get; }

        public static async Task<SftpGameServerFileTransportSession> CreateAsync(ResolvedFileTransport transport, CancellationToken cancellationToken)
        {
            var expectedFingerprint = NormalizeFingerprint(transport.Credentials.HostKeyFingerprint);
            if (string.IsNullOrWhiteSpace(expectedFingerprint))
            {
                throw new InvalidOperationException("The sftp.hostKeyFingerprint setting is required for SFTP connections.");
            }

            var connectionInfo = CreateSftpConnectionInfo(transport.Credentials);
            var authenticationMethod = connectionInfo.AuthenticationMethods.Single();
            SftpClient? client = null;

            try
            {
                client = new SftpClient(connectionInfo);
                client.HostKeyReceived += (_, args) =>
                {
                    var receivedFingerprint = NormalizeFingerprint(BitConverter.ToString(args.FingerPrint));
                    args.CanTrust = string.Equals(receivedFingerprint, expectedFingerprint, StringComparison.OrdinalIgnoreCase);
                };

                await Task.Run(client.Connect, cancellationToken).ConfigureAwait(false);

                return new SftpGameServerFileTransportSession(transport, client, authenticationMethod);
            }
            catch
            {
                client?.Dispose();
                authenticationMethod.Dispose();
                throw;
            }
        }

        private static string NormalizeFingerprint(string? fingerprint)
        {
            if (string.IsNullOrWhiteSpace(fingerprint))
            {
                return string.Empty;
            }

            return new string(fingerprint.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
        }

        public Task<IReadOnlyList<FileTransportEntry>> GetListing(string path, CancellationToken cancellationToken = default)
        {
            return Task.Run<IReadOnlyList<FileTransportEntry>>(() =>
            {
                var entries = _client.ListDirectory(path)
                    .Where(item => item.Name != "." && item.Name != "..")
                    .Select(item => new FileTransportEntry(
                        item.Name,
                        item.FullName,
                        item.IsDirectory,
                        item.IsDirectory ? null : item.Length,
                        item.LastWriteTimeUtc))
                    .ToList();

                return entries;
            }, cancellationToken);
        }

        public Task<bool> FileExists(string path, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                if (!_client.Exists(path))
                {
                    return false;
                }

                return !_client.GetAttributes(path).IsDirectory;
            }, cancellationToken);
        }

        public Task<byte[]> DownloadBytes(string path, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                using var stream = new MemoryStream();
                _client.DownloadFile(path, stream);
                return stream.ToArray();
            }, cancellationToken);
        }

        public Task UploadBytes(string path, byte[] content, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                using var stream = new MemoryStream(content);
                _client.UploadFile(stream, path, true);
            }, cancellationToken);
        }

        public Task UploadStream(string path, Stream content, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                if (content.CanSeek)
                {
                    content.Position = 0;
                }

                _client.UploadFile(content, path, true);
            }, cancellationToken);
        }

        public Task DeleteFile(string path, CancellationToken cancellationToken = default)
            => Task.Run(() => _client.DeleteFile(path), cancellationToken);

        public Task<bool> DirectoryExists(string path, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                if (!_client.Exists(path))
                {
                    return false;
                }

                return _client.GetAttributes(path).IsDirectory;
            }, cancellationToken);
        }

        public Task CreateDirectory(string path, CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                if (!_client.Exists(path))
                {
                    _client.CreateDirectory(path);
                }
            }, cancellationToken);
        }

        public Task DeleteDirectory(string path, CancellationToken cancellationToken = default)
            => Task.Run(() => DeleteDirectoryRecursive(path), cancellationToken);

        private void DeleteDirectoryRecursive(string path)
        {
            if (!_client.Exists(path))
            {
                return;
            }

            foreach (var item in _client.ListDirectory(path).Where(entry => entry.Name != "." && entry.Name != ".."))
            {
                if (item.IsDirectory)
                {
                    DeleteDirectoryRecursive(item.FullName);
                }
                else
                {
                    _client.DeleteFile(item.FullName);
                }
            }

            _client.DeleteDirectory(path);
        }

        public ValueTask DisposeAsync()
        {
            _client.Dispose();
            _authenticationMethod.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
