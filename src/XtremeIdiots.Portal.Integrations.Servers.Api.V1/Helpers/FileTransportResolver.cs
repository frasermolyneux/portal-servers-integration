using MX.Api.Abstractions;
using MX.Api.Web.Extensions;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Constants;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.GameServers;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;

internal sealed class FileTransportResolver(
    IRepositoryApiClient repositoryApiClient) : IFileTransportResolver
{
    private const string FtpNamespace = "ftp";
    private const string SftpNamespace = "sftp";

    public async Task<ApiResult<ResolvedFileTransport>> Resolve(Guid gameServerId, CancellationToken cancellationToken = default)
    {
        var gameServerApiResponse = await repositoryApiClient.GameServers.V1
            .GetGameServer(gameServerId, cancellationToken)
            .ConfigureAwait(false);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse<ResolvedFileTransport>(
                    new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist."))
                .ToNotFoundResult();
        }

        var gameServer = gameServerApiResponse.Result.Data;
        var selectedTransport = SelectTransport(gameServer);
        if (selectedTransport == FileTransportType.Unknown)
        {
            return new ApiResponse<ResolvedFileTransport>(
                    new ApiError(ErrorCodes.FILE_TRANSPORT_CREDENTIALS_MISSING, "The game server does not have file transport credentials configured."))
                .ToBadRequestResult();
        }

        var configNamespace = selectedTransport == FileTransportType.Sftp ? SftpNamespace : FtpNamespace;
        var configResult = await repositoryApiClient.GameServerConfigurations.V1
            .GetConfiguration(gameServerId, configNamespace, cancellationToken)
            .ConfigureAwait(false);

        var credentials = FileTransportConfigResolver.Parse(selectedTransport, configResult?.Result?.Data?.Configuration);
        if (credentials == null)
        {
            return new ApiResponse<ResolvedFileTransport>(
                    new ApiError(ErrorCodes.FILE_TRANSPORT_CREDENTIALS_MISSING, "The game server does not have file transport credentials configured."))
                .ToBadRequestResult();
        }

        var resolved = new ResolvedFileTransport(selectedTransport, configNamespace, credentials);
        return new ApiResponse<ResolvedFileTransport>(resolved).ToApiResult();
    }

    private static FileTransportType SelectTransport(GameServerDto gameServer)
    {
        // Authoritative transport metadata has precedence when explicitly set.
        if (gameServer.FileTransportEnabled)
        {
            if (gameServer.FileTransportType == FileTransportType.Sftp)
                return FileTransportType.Sftp;

            return FileTransportType.Ftp;
        }

        // Compatibility fallback for legacy payloads that only used FTP flags.
        if (gameServer.FtpEnabled)
            return FileTransportType.Ftp;

        return FileTransportType.Unknown;
    }
}
