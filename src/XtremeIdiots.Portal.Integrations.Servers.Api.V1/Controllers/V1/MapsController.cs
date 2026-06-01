
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
                return new ApiResponse<ServerMapsCollectionDto>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

            var sessionResult = await fileTransportFactory.CreateSession(gameServerId).ConfigureAwait(false);
            if (!sessionResult.IsSuccess || sessionResult.Result?.Data == null)
            {
                var error = sessionResult.Result?.Errors?.FirstOrDefault();
                if (string.Equals(error?.Code, ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, StringComparison.OrdinalIgnoreCase))
                    return new ApiResponse<ServerMapsCollectionDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, "Failed to connect to the game server file transport host to retrieve maps.")).ToApiResult();

                return new ApiResponse<ServerMapsCollectionDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CREDENTIALS_MISSING, "The game server does not have file transport credentials configured.")).ToBadRequestResult();
            }

            await using var session = sessionResult.Result.Data;

            var operation = telemetryClient.StartOperation<DependencyTelemetry>("GetFileList");
            operation.Telemetry.Type = session.Transport.TelemetryType;
            operation.Telemetry.Target = session.Transport.TelemetryTarget;

            try
            {
                var files = await session.GetListing("usermaps").ConfigureAwait(false);
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
            var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

            if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
                return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

            // Built-in maps are already present on the server — no FTP upload needed
            if (BuiltInMaps.IsBuiltIn(gameServerApiResponse.Result.Data.GameType, mapName))
            {
                logger.LogInformation("Map {MapName} is a built-in map for {GameType}, skipping FTP push", mapName, gameServerApiResponse.Result.Data.GameType);
                return new ApiResponse().ToApiResult();
            }

            var mapApiResponse = await repositoryApiClient.Maps.V1.GetMap(gameServerApiResponse.Result.Data.GameType, mapName);

            if (mapApiResponse.IsNotFound || mapApiResponse.Result?.Data == null)
                return new ApiResponse(new ApiError(ErrorCodes.MAP_NOT_FOUND, $"The map '{mapName}' does not exist in the repository.")).ToNotFoundResult();

            if (mapApiResponse.Result.Data.MapFiles.Count == 0)
                return new ApiResponse(new ApiError(ErrorCodes.MAP_FILES_NOT_FOUND, $"The map '{mapName}' does not have any files associated with it.")).ToBadRequestResult();

            var sessionResult = await fileTransportFactory.CreateSession(gameServerId).ConfigureAwait(false);
            if (!sessionResult.IsSuccess || sessionResult.Result?.Data == null)
            {
                var error = sessionResult.Result?.Errors?.FirstOrDefault();
                if (string.Equals(error?.Code, ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, StringComparison.OrdinalIgnoreCase))
                    return new ApiResponse(new ApiError(ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, "Failed to connect to the game server file transport host to push maps.")).ToApiResult();

                return new ApiResponse(new ApiError(ErrorCodes.FILE_TRANSPORT_CREDENTIALS_MISSING, "The game server does not have file transport credentials configured.")).ToBadRequestResult();
            }

            await using var session = sessionResult.Result.Data;

            try
            {
                var mapDirectoryPath = $"usermaps/{mapName}";

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
            var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

            if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
                return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

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
                    return new ApiResponse(new ApiError(ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, "Failed to connect to the game server file transport host to delete maps.")).ToApiResult();

                return new ApiResponse(new ApiError(ErrorCodes.FILE_TRANSPORT_CREDENTIALS_MISSING, "The game server does not have file transport credentials configured.")).ToBadRequestResult();
            }

            await using var session = sessionResult.Result.Data;

            try
            {
                var mapDirectoryPath = $"usermaps/{mapName}";

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
                return new ApiResponse<MapVerificationCollectionDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "Map names list cannot be null or empty.")).ToBadRequestResult();

            var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

            if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
                return new ApiResponse<MapVerificationCollectionDto>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

            var sessionResult = await fileTransportFactory.CreateSession(gameServerId, cancellationToken).ConfigureAwait(false);
            if (!sessionResult.IsSuccess || sessionResult.Result?.Data == null)
            {
                var error = sessionResult.Result?.Errors?.FirstOrDefault();
                if (string.Equals(error?.Code, ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, StringComparison.OrdinalIgnoreCase))
                    return new ApiResponse<MapVerificationCollectionDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, "Failed to connect to the game server file transport host to verify maps.")).ToApiResult();

                return new ApiResponse<MapVerificationCollectionDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CREDENTIALS_MISSING, "The game server does not have file transport credentials configured.")).ToBadRequestResult();
            }

            await using var session = sessionResult.Result.Data;

            var operation = telemetryClient.StartOperation<DependencyTelemetry>("VerifyServerMaps");
            operation.Telemetry.Type = session.Transport.TelemetryType;
            operation.Telemetry.Target = session.Transport.TelemetryTarget;

            try
            {
                var files = await session.GetListing("usermaps", cancellationToken).ConfigureAwait(false);
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
    }
