
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
    IHttpClientFactory httpClientFactory) : Controller, IMapsApi
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

        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        // Built-in maps are already present on the server — no FTP upload needed
        if (BuiltInMaps.IsBuiltIn(gameServerApiResponse.Result.Data.GameType, mapName))
        {
            logger.LogInformation("Map {MapName} is a built-in map for {GameType}, skipping FTP push", mapName, gameServerApiResponse.Result.Data.GameType);
            return new ApiResponse().ToApiResult();
        }

        var mapApiResponse = await repositoryApiClient.Maps.V1.GetMap(gameServerApiResponse.Result.Data.GameType, mapName);

        if (mapApiResponse.IsNotFound || mapApiResponse.Result?.Data == null)
        {
            return new ApiResponse(new ApiError(ErrorCodes.MAP_NOT_FOUND, $"The map '{mapName}' does not exist in the repository.")).ToNotFoundResult();
        }

        if (mapApiResponse.Result.Data.MapFiles.Count == 0)
        {
            return new ApiResponse(new ApiError(ErrorCodes.MAP_FILES_NOT_FOUND, $"The map '{mapName}' does not have any files associated with it.")).ToBadRequestResult();
        }

        var sessionResult = await fileTransportFactory.CreateSession(gameServerId).ConfigureAwait(false);
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
            var mapDirectoryPath = JoinPath(mapsDirectoryPath, mapName);

            if (await session.DirectoryExists(mapDirectoryPath).ConfigureAwait(false))
            {
                logger.LogInformation("Directory {MapDirectoryPath} already exists on the server, skipping sync", mapDirectoryPath);
                return new ApiResponse().ToApiResult();
            }
            else
            {
                await session.CreateDirectory(mapDirectoryPath).ConfigureAwait(false);

                var httpClient = httpClientFactory.CreateClient();
                foreach (var file in mapApiResponse.Result.Data.MapFiles)
                {
                    await using var mapStream = await httpClient.GetStreamAsync(file.Url).ConfigureAwait(false);
                    await session.UploadStream($"{mapDirectoryPath}/{file.FileName}", mapStream).ConfigureAwait(false);
                }

                return new ApiResponse().ToApiResult();
            }

        }
        catch (Exception ex)
        {
            telemetryClient.TrackException(ex);
            logger.LogError(ex, "Failed to push map {MapName} to game server {GameServerId}", mapName, gameServerId);
            return new ApiResponse(new ApiError(ErrorCodes.FILE_TRANSPORT_OPERATION_FAILED, "Failed to push map files to the game server file transport host.")).ToApiResult();
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
            logger.LogInformation("Map {MapName} is a built-in map for {GameType}, skipping FTP delete", mapName, gameServerApiResponse.Result.Data.GameType);
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
                logger.LogInformation("Directory {MapDirectoryPath} does not exist on the server, skipping delete", mapDirectoryPath);
                return new ApiResponse().ToApiResult();
            }

        }
        catch (Exception ex)
        {
            telemetryClient.TrackException(ex);
            logger.LogError(ex, "Failed to delete map {MapName} from game server {GameServerId}", mapName, gameServerId);
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

        var response = await ((IMapsApi)this).VerifyServerMaps(gameServerId, request.MapNames);

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

        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

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
            var files = await session.GetListing(mapsDirectoryPath, cancellationToken).ConfigureAwait(false);
            var existingDirectories = new HashSet<string>(files.Where(f => f.IsDirectory).Select(f => f.Name), StringComparer.OrdinalIgnoreCase);

            var results = mapNames.Select(mapName => new MapVerificationResultDto(mapName, existingDirectories.Contains(mapName))).ToList();

            var data = new MapVerificationCollectionDto(results);
            return new ApiResponse<MapVerificationCollectionDto>(data).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to verify server maps from file transport host for game server {GameServerId}", gameServerId);
            return new ApiResponse<MapVerificationCollectionDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, "Failed to connect to the game server file transport host to verify maps.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
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

        if (mapName.Contains('/') || mapName.Contains('\\'))
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
