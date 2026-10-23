
using System.Net;
using Asp.Versioning;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MX.Api.Abstractions;
using MX.Api.Web.Extensions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Maps;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Constants;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Api.Client.V1;
using XtremeIdiots.Redirect.Api.Abstractions.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1;

[ApiController]
[Authorize(Roles = "ServiceAccount")]
[ApiVersion(ApiVersions.V1)]
[Route("v{version:apiVersion}")]
public class MapsController(
    ILogger<MapsController> logger,
    IRepositoryApiClient repositoryApiClient,
    IGameServerFileTransportFactory fileTransportFactory,
    TelemetryClient telemetryClient,
    IRedirectApiClient redirectApiClient,
    IRedirectDownloadClient redirectDownloadClient) : Controller, IMapsApi
{

    [HttpGet]
    [Route("maps/{gameServerId}/host/loaded")]
    public async Task<IActionResult> GetLoadedServerMapsFromHost(Guid gameServerId)
    {
        var response = await ((IMapsApi)this).GetLoadedServerMapsFromHost(gameServerId);

        return response.ToHttpResult();
    }

    async Task<ApiResult<ServerMapsCollectionDto>> IMapsApi.GetLoadedServerMapsFromHost(Guid gameServerId)
    {
        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse<ServerMapsCollectionDto>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var sessionResult = await fileTransportFactory.CreateSession(gameServerId).ConfigureAwait(false);
        if (!sessionResult.IsSuccess || sessionResult.Result?.Data == null)
        {
            var error = sessionResult.Result?.Errors?.FirstOrDefault();
            if (string.Equals(error?.Code, ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, StringComparison.OrdinalIgnoreCase))
            {
                return new ApiResponse<ServerMapsCollectionDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, "Failed to connect to the game server file transport host to retrieve maps.")).ToApiResult();
            }

            return new ApiResponse<ServerMapsCollectionDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CREDENTIALS_MISSING, "The game server does not have file transport credentials configured.")).ToBadRequestResult();
        }

        await using var session = sessionResult.Result.Data;
        if (!TryGetMapsDirectoryPath(session.Transport.Credentials.MapsRootPath, out var mapsDirectoryPath))
        {
            return new ApiResponse<ServerMapsCollectionDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "The configured maps root path is invalid.")).ToBadRequestResult();
        }

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("GetFileList");
        operation.Telemetry.Type = session.Transport.TelemetryType;
        operation.Telemetry.Target = session.Transport.TelemetryTarget;

        try
        {
            var files = await session.GetListing(mapsDirectoryPath).ConfigureAwait(false);
            var entries = files.Select(f => new ServerMapDto(f.Name, f.FullPath, f.Modified ?? DateTime.UnixEpoch)).ToList();

            var data = new ServerMapsCollectionDto(entries);

            return new ApiResponse<ServerMapsCollectionDto>(data).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to retrieve server maps from file transport host for game server {GameServerId}", gameServerId);
            return new ApiResponse<ServerMapsCollectionDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, "Failed to connect to the game server file transport host to retrieve maps.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    [HttpPost]
    [Route("maps/{gameServerId}/host/{mapName}")]
    public async Task<IActionResult> PushServerMapToHost(Guid gameServerId, string mapName)
    {
        var response = await ((IMapsApi)this).PushServerMapToHost(gameServerId, mapName);

        return response.ToHttpResult();
    }

    async Task<ApiResult> IMapsApi.PushServerMapToHost(Guid gameServerId, string mapName)
    {
        if (!IsValidMapName(mapName))
        {
            return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "The map name contains invalid path characters.")).ToBadRequestResult();
        }

        var cancellationToken = HttpContext?.RequestAborted ?? CancellationToken.None;
        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId, cancellationToken);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        // Built-in maps are already present on the server — no FTP upload needed
        if (BuiltInMaps.IsBuiltIn(gameServerApiResponse.Result.Data.GameType, mapName))
        {
            logger.LogInformation("Built-in map requested for {GameType}, skipping FTP push", gameServerApiResponse.Result.Data.GameType);
            return new ApiResponse().ToApiResult();
        }

        var sessionResult = await fileTransportFactory.CreateSession(gameServerId, cancellationToken).ConfigureAwait(false);
        if (!sessionResult.IsSuccess || sessionResult.Result?.Data == null)
        {
            var error = sessionResult.Result?.Errors?.FirstOrDefault();
            if (string.Equals(error?.Code, ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, StringComparison.OrdinalIgnoreCase))
            {
                return new ApiResponse(new ApiError(ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, "Failed to connect to the game server file transport host to push maps.")).ToApiResult();
            }

            return new ApiResponse(new ApiError(ErrorCodes.FILE_TRANSPORT_CREDENTIALS_MISSING, "The game server does not have file transport credentials configured.")).ToBadRequestResult();
        }

        await using var session = sessionResult.Result.Data;
        if (!TryGetMapsDirectoryPath(session.Transport.Credentials.MapsRootPath, out var mapsDirectoryPath))
        {
            return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "The configured maps root path is invalid.")).ToBadRequestResult();
        }

        try
        {
            var files = await GetRedirectFiles(gameServerApiResponse.Result.Data.GameType, mapName, cancellationToken);
            if (files.Count == 0)
            {
                return new ApiResponse(new ApiError(ErrorCodes.MAP_FILES_NOT_FOUND, $"The map '{mapName}' has no downloadable game files.")).ToBadRequestResult();
            }

            var mapDirectoryPath = JoinPath(mapsDirectoryPath, mapName);
            await EnsureDirectory(session, mapDirectoryPath, cancellationToken);
            foreach (var (file, relativePath) in files)
            {
                var destination = JoinPath(mapDirectoryPath, relativePath);
                var separator = relativePath.LastIndexOf('/');
                if (separator >= 0)
                {
                    var directory = mapDirectoryPath;
                    foreach (var segment in relativePath[..separator].Split('/'))
                    {
                        directory = JoinPath(directory, segment);
                        await EnsureDirectory(session, directory, cancellationToken);
                    }
                }

                using var download = await OpenRedirectFile(file, cancellationToken);
                await using var stream = await download.Content.ReadAsStreamAsync(cancellationToken);
                await session.UploadStream(destination, stream, cancellationToken).ConfigureAwait(false);
            }

            if (!await HasCompleteFiles(session, mapDirectoryPath, files, cancellationToken))
            {
                throw new IOException("The game server did not retain every transferred file at its expected size.");
            }

            return new ApiResponse().ToApiResult();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return new ApiResponse(new ApiError(ErrorCodes.MAP_NOT_FOUND, $"The map '{mapName}' is not available on the redirect.")).ToNotFoundResult();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            telemetryClient.TrackException(ex);
            logger.LogError(ex, "Failed to push map to game server {GameServerId}", gameServerId);
            return new ApiResult(HttpStatusCode.BadGateway, new ApiResponse(new ApiError(ErrorCodes.FILE_TRANSPORT_OPERATION_FAILED, "Failed to transfer the complete redirect map to the game server.")));
        }
    }

    [HttpDelete]
    [Route("maps/{gameServerId}/host/{mapName}")]
    public async Task<IActionResult> DeleteServerMapFromHost(Guid gameServerId, string mapName)
    {
        var response = await ((IMapsApi)this).DeleteServerMapFromHost(gameServerId, mapName);

        return response.ToHttpResult();
    }

    async Task<ApiResult> IMapsApi.DeleteServerMapFromHost(Guid gameServerId, string mapName)
    {
        if (!IsValidMapName(mapName))
        {
            return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "The map name contains invalid path characters.")).ToBadRequestResult();
        }

        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        // Built-in maps cannot be removed from the server — they are part of the game installation
        if (BuiltInMaps.IsBuiltIn(gameServerApiResponse.Result.Data.GameType, mapName))
        {
            logger.LogInformation("Built-in map requested for {GameType}, skipping FTP delete", gameServerApiResponse.Result.Data.GameType);
            return new ApiResponse().ToApiResult();
        }

        var sessionResult = await fileTransportFactory.CreateSession(gameServerId).ConfigureAwait(false);
        if (!sessionResult.IsSuccess || sessionResult.Result?.Data == null)
        {
            var error = sessionResult.Result?.Errors?.FirstOrDefault();
            if (string.Equals(error?.Code, ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, StringComparison.OrdinalIgnoreCase))
            {
                return new ApiResponse(new ApiError(ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, "Failed to connect to the game server file transport host to delete maps.")).ToApiResult();
            }

            return new ApiResponse(new ApiError(ErrorCodes.FILE_TRANSPORT_CREDENTIALS_MISSING, "The game server does not have file transport credentials configured.")).ToBadRequestResult();
        }

        await using var session = sessionResult.Result.Data;
        if (!TryGetMapsDirectoryPath(session.Transport.Credentials.MapsRootPath, out var mapsDirectoryPath))
        {
            return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "The configured maps root path is invalid.")).ToBadRequestResult();
        }

        try
        {
            var mapDirectoryPath = JoinPath(mapsDirectoryPath, mapName);

            if (await session.DirectoryExists(mapDirectoryPath).ConfigureAwait(false))
            {
                await session.DeleteDirectory(mapDirectoryPath).ConfigureAwait(false);
                return new ApiResponse().ToApiResult();
            }
            else
            {
                logger.LogInformation("Map directory does not exist on the server, skipping delete");
                return new ApiResponse().ToApiResult();
            }

        }
        catch (Exception ex)
        {
            telemetryClient.TrackException(ex);
            logger.LogError(ex, "Failed to delete map from game server {GameServerId}", gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.FILE_TRANSPORT_OPERATION_FAILED, "Failed to delete map directory from the game server file transport host.")).ToApiResult();
        }
    }

    [HttpPost]
    [Route("maps/{gameServerId}/host/verify")]
    public async Task<IActionResult> VerifyServerMaps(Guid gameServerId, [FromBody] MapVerificationRequestDto? request)
    {
        if (request == null)
        {
            return BadRequest(new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Request body cannot be null.")).ToApiResult());
        }

        var response = await ((IMapsApi)this).VerifyServerMaps(gameServerId, request.MapNames, HttpContext?.RequestAborted ?? CancellationToken.None);

        return response.ToHttpResult();
    }

    async Task<ApiResult<MapVerificationCollectionDto>> IMapsApi.VerifyServerMaps(Guid gameServerId, List<string> mapNames, CancellationToken cancellationToken)
    {
        if (mapNames == null || mapNames.Count == 0)
        {
            return new ApiResponse<MapVerificationCollectionDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "Map names list cannot be null or empty.")).ToBadRequestResult();
        }

        if (mapNames.Any(mapName => !IsValidMapName(mapName)))
        {
            return new ApiResponse<MapVerificationCollectionDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "One or more map names contain invalid path characters.")).ToBadRequestResult();
        }

        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId, cancellationToken);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse<MapVerificationCollectionDto>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        var sessionResult = await fileTransportFactory.CreateSession(gameServerId, cancellationToken).ConfigureAwait(false);
        if (!sessionResult.IsSuccess || sessionResult.Result?.Data == null)
        {
            var error = sessionResult.Result?.Errors?.FirstOrDefault();
            if (string.Equals(error?.Code, ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, StringComparison.OrdinalIgnoreCase))
            {
                return new ApiResponse<MapVerificationCollectionDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, "Failed to connect to the game server file transport host to verify maps.")).ToApiResult();
            }

            return new ApiResponse<MapVerificationCollectionDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CREDENTIALS_MISSING, "The game server does not have file transport credentials configured.")).ToBadRequestResult();
        }

        await using var session = sessionResult.Result.Data;
        if (!TryGetMapsDirectoryPath(session.Transport.Credentials.MapsRootPath, out var mapsDirectoryPath))
        {
            return new ApiResponse<MapVerificationCollectionDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "The configured maps root path is invalid.")).ToBadRequestResult();
        }

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("VerifyServerMaps");
        operation.Telemetry.Type = session.Transport.TelemetryType;
        operation.Telemetry.Target = session.Transport.TelemetryTarget;

        try
        {
            var results = new List<MapVerificationResultDto>();
            foreach (var mapName in mapNames)
            {
                if (BuiltInMaps.IsBuiltIn(gameServerApiResponse.Result.Data.GameType, mapName))
                {
                    results.Add(new MapVerificationResultDto(mapName, true));
                    continue;
                }

                try
                {
                    var files = await GetRedirectFiles(gameServerApiResponse.Result.Data.GameType, mapName, cancellationToken);
                    var complete = files.Count > 0 && await HasCompleteFiles(session, JoinPath(mapsDirectoryPath, mapName), files, cancellationToken);
                    results.Add(new MapVerificationResultDto(mapName, complete));
                }
                catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    results.Add(new MapVerificationResultDto(mapName, false));
                }
            }

            var data = new MapVerificationCollectionDto(results);
            return new ApiResponse<MapVerificationCollectionDto>(data).ToApiResult();
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to verify server maps from file transport host for game server {GameServerId}", gameServerId);
            return new ApiResult<MapVerificationCollectionDto>(HttpStatusCode.BadGateway, new ApiResponse<MapVerificationCollectionDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_OPERATION_FAILED, "Failed to verify complete redirect maps on the game server.")));
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    private async Task<IReadOnlyList<(ContentEntry File, string RelativePath)>> GetRedirectFiles(GameType gameType, string mapName, CancellationToken cancellationToken)
    {
        if (gameType is not (GameType.CallOfDuty4 or GameType.CallOfDuty4x or GameType.CallOfDuty5))
        {
            throw new NotSupportedException("Redirect map transfers are not supported for this game.");
        }
        var game = gameType == GameType.CallOfDuty5 ? "cod5" : "cod4";
        var result = await redirectApiClient.Manifest(game, mapName, cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!result.IsSuccess || result.Result?.Data is not { } manifest)
        {
            throw new HttpRequestException($"The redirect API did not return a complete map manifest (HTTP {(int)result.StatusCode}).", null,
                result.IsSuccess ? HttpStatusCode.BadGateway : result.StatusCode);
        }
        if (manifest.Game != game || manifest.Name != mapName || manifest.Path != $"redirect/{game}/usermaps/{mapName}" ||
            string.IsNullOrWhiteSpace(manifest.Version) || manifest.Files.Any(file => file.IsDirectory || file.Bytes < 0) ||
            manifest.Bytes != manifest.Files.Sum(file => file.Bytes))
        {
            throw new InvalidDataException("The redirect manifest does not match the requested map.");
        }

        var prefix = manifest.Path + "/";
        var files = new List<(ContentEntry File, string RelativePath)>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in manifest.Files)
        {
            if (!file.Path.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new InvalidDataException("The redirect manifest contains a file outside the requested map.");
            }
            var relative = file.Path[prefix.Length..];
            if (!relative.EndsWith(".ff", StringComparison.OrdinalIgnoreCase) && !relative.EndsWith(".iwd", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (relative.Split('/').Any(segment => string.IsNullOrWhiteSpace(segment) ||
                segment is "." or ".." || segment.EndsWith('.') || segment.EndsWith(' ') ||
                IsReservedDeviceName(segment) ||
                segment.Any(character => char.IsControl(character) || "\\:<>\"|?*".Contains(character))))
            {
                throw new InvalidDataException("The redirect manifest contains an unsafe game-server file path.");
            }
            if (file.Bytes < 0 || string.IsNullOrWhiteSpace(file.Version) || directories.Contains(relative) || !paths.Add(relative))
            {
                throw new InvalidDataException("The redirect manifest contains invalid or colliding game files.");
            }
            for (var separator = relative.IndexOf('/'); separator >= 0; separator = relative.IndexOf('/', separator + 1))
            {
                var directory = relative[..separator];
                if (paths.Contains(directory))
                {
                    throw new InvalidDataException("The redirect manifest contains a file that is also a game-file directory.");
                }
                directories.Add(directory);
            }
            files.Add((file, relative));
        }
        return files;
    }

    private async Task<HttpResponseMessage> OpenRedirectFile(ContentEntry file, CancellationToken cancellationToken)
    {
        try
        {
            return await redirectDownloadClient.OpenFile(file, cancellationToken: cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new IOException("A file from the validated redirect manifest is no longer available.", ex);
        }
    }

    private static async Task EnsureDirectory(IGameServerFileTransportSession session, string path, CancellationToken cancellationToken)
    {
        if (!await session.DirectoryExists(path, cancellationToken).ConfigureAwait(false))
        {
            await session.CreateDirectory(path, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<bool> HasCompleteFiles(IGameServerFileTransportSession session, string mapDirectoryPath,
        IReadOnlyList<(ContentEntry File, string RelativePath)> files, CancellationToken cancellationToken)
    {
        foreach (var group in files.GroupBy(file => file.RelativePath.Contains('/')
            ? file.RelativePath[..file.RelativePath.LastIndexOf('/')] : string.Empty))
        {
            var directory = group.Key.Length == 0 ? mapDirectoryPath : JoinPath(mapDirectoryPath, group.Key);
            if (!await session.DirectoryExists(directory, cancellationToken).ConfigureAwait(false))
            {
                return false;
            }
            var entries = await session.GetListing(directory, cancellationToken).ConfigureAwait(false);
            foreach (var (file, relativePath) in group)
            {
                var name = relativePath[(relativePath.LastIndexOf('/') + 1)..];
                if (!entries.Any(entry => !entry.IsDirectory && entry.Name == name && entry.Size == file.Bytes))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static bool TryGetMapsDirectoryPath(string? mapsRootPath, out string mapsDirectoryPath)
    {
        mapsDirectoryPath = string.Empty;
        if (!TryNormalizePath(mapsRootPath, out var normalizedRoot))
        {
            return false;
        }

        if (normalizedRoot == "/")
        {
            mapsDirectoryPath = "usermaps";
            return true;
        }

        if (normalizedRoot.EndsWith("/usermaps", StringComparison.OrdinalIgnoreCase))
        {
            mapsDirectoryPath = normalizedRoot;
            return true;
        }

        mapsDirectoryPath = JoinPath(normalizedRoot, "usermaps");
        return true;
    }

    private static bool TryNormalizePath(string? path, out string normalized)
    {
        normalized = "/";

        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        normalized = path.Replace('\\', '/').Trim();
        if (normalized.Length == 0)
        {
            normalized = "/";
            return true;
        }

        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        while (normalized.Length > 1 && normalized.EndsWith('/'))
        {
            normalized = normalized[..^1];
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment == ".."))
        {
            return false;
        }

        return true;
    }

    private static string JoinPath(string left, string right)
    {
        var leftNormalized = left.TrimEnd('/');
        var rightNormalized = right.Trim('/');
        return $"{leftNormalized}/{rightNormalized}";
    }

    private static bool IsReservedDeviceName(string segment)
    {
        var name = segment.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
        return name is "CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$" ||
            (name.Length == 4 && (name.StartsWith("COM", StringComparison.Ordinal) || name.StartsWith("LPT", StringComparison.Ordinal)) &&
                name[3] is (>= '1' and <= '9') or '\u00b9' or '\u00b2' or '\u00b3');
    }

    private static bool IsValidMapName(string mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            return false;
        }

        var normalized = mapName.Trim();
        if (normalized is "." or "..")
        {
            return false;
        }

        if (mapName.Any("\\/:<>\"|?*".Contains) || mapName.EndsWith('.') || mapName.EndsWith(' ') || IsReservedDeviceName(mapName))
        {
            return false;
        }

        if (mapName.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        return mapName.All(c => !char.IsControl(c));
    }
}
