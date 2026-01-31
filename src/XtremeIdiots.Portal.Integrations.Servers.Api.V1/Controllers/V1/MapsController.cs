
using System.Net;
using Asp.Versioning;
using FluentFTP;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MX.Api.Abstractions;
using MX.Api.Web.Extensions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Maps;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Constants;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1;

[ApiController]
[Authorize(Roles = "ServiceAccount")]
[ApiVersion(ApiVersions.V1)]
[Route("api/v{version:apiVersion}")]
public class MapsController(
    ILogger<MapsController> logger,
    IRepositoryApiClient repositoryApiClient,
    TelemetryClient telemetryClient,
    IConfiguration configuration) : Controller, IMapsApi
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

            var operation = telemetryClient.StartOperation<DependencyTelemetry>("GetFileList");
            operation.Telemetry.Type = $"FTP";
            operation.Telemetry.Target = $"{gameServerApiResponse.Result.Data.FtpHostname}:{gameServerApiResponse.Result.Data.FtpPort}";

            AsyncFtpClient? ftpClient = null;

            try
            {
                ftpClient = new AsyncFtpClient(gameServerApiResponse.Result.Data.FtpHostname, gameServerApiResponse.Result.Data.FtpUsername, gameServerApiResponse.Result.Data.FtpPassword, gameServerApiResponse.Result.Data.FtpPort ?? 21);
                ftpClient.ValidateCertificate += (control, e) =>
                {
                    if (e.Certificate.GetCertHashString().Equals(configuration["xtremeidiots_ftp_certificate_thumbprint"]))
                    { // Account for self-signed FTP certificate for self-hosted servers
                        e.Accept = true;
                    }
                };

                await ftpClient.AutoConnect();
                await ftpClient.SetWorkingDirectory("usermaps");

                var files = await ftpClient.GetListing();
                var entries = files.Select(f => new ServerMapDto(f.Name, f.FullName, f.Modified)).ToList();

                var data = new ServerMapsCollectionDto(entries);

                return new ApiResponse<ServerMapsCollectionDto>(data).ToApiResult();
            }
            catch (Exception ex)
            {
                operation.Telemetry.Success = false;
                operation.Telemetry.ResultCode = ex.Message;
                telemetryClient.TrackException(ex);

                logger.LogError(ex, "Failed to retrieve server maps from FTP host for game server {GameServerId}", gameServerId);
                return new ApiResponse<ServerMapsCollectionDto>(new ApiError(ErrorCodes.FTP_CONNECTION_FAILED, "Failed to connect to the game server's FTP host to retrieve maps.")).ToApiResult();
            }
            finally
            {
                telemetryClient.StopOperation(operation);
                ftpClient?.Dispose();
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

            var mapApiResponse = await repositoryApiClient.Maps.V1.GetMap(gameServerApiResponse.Result.Data.GameType, mapName);

            if (mapApiResponse.IsNotFound || mapApiResponse.Result?.Data == null)
                return new ApiResponse(new ApiError(ErrorCodes.MAP_NOT_FOUND, $"The map '{mapName}' does not exist in the repository.")).ToNotFoundResult();

            if (mapApiResponse.Result.Data.MapFiles.Count == 0)
                return new ApiResponse(new ApiError(ErrorCodes.MAP_FILES_NOT_FOUND, $"The map '{mapName}' does not have any files associated with it.")).ToBadRequestResult();

            AsyncFtpClient? ftpClient = null;

            try
            {
                ftpClient = new AsyncFtpClient(gameServerApiResponse.Result.Data.FtpHostname, gameServerApiResponse.Result.Data.FtpUsername, gameServerApiResponse.Result.Data.FtpPassword, gameServerApiResponse.Result.Data.FtpPort ?? 21);
                ftpClient.ValidateCertificate += (control, e) =>
                {
                    if (e.Certificate.GetCertHashString().Equals(configuration["xtremeidiots_ftp_certificate_thumbprint"]))
                    { // Account for self-signed FTP certificate for self-hosted servers
                        e.Accept = true;
                    }
                };

                await ftpClient.AutoConnect();

                var mapDirectoryPath = $"usermaps/{mapName}";

                if (await ftpClient.DirectoryExists(mapDirectoryPath))
                {
                    logger.LogInformation("Directory {MapDirectoryPath} already exists on the server, skipping sync", mapDirectoryPath);
                    return new ApiResponse().ToApiResult();
                }
                else
                {
                    await ftpClient.CreateDirectory(mapDirectoryPath);

                    foreach (var file in mapApiResponse.Result.Data.MapFiles)
                    {
                        using (var httpClient = new HttpClient())
                        {
                            var filePath = Path.Join(Path.GetTempPath(), file.FileName);
                            using (var stream = System.IO.File.Create(filePath))
                                await (await httpClient.GetStreamAsync(file.Url)).CopyToAsync(stream);

                            await ftpClient.UploadFile(filePath, $"{mapDirectoryPath}/{file.FileName}");
                        }
                    }

                    return new ApiResponse().ToApiResult();
                }

            }
            catch (Exception ex)
            {
                telemetryClient.TrackException(ex);
                logger.LogError(ex, "Failed to push map {MapName} to game server {GameServerId}", mapName, gameServerId);
                return new ApiResponse(new ApiError(ErrorCodes.FTP_OPERATION_FAILED, "Failed to push map files to the game server's FTP host.")).ToApiResult();
            }
            finally
            {
                ftpClient?.Dispose();
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

            AsyncFtpClient? ftpClient = null;

            try
            {
                ftpClient = new AsyncFtpClient(gameServerApiResponse.Result.Data.FtpHostname, gameServerApiResponse.Result.Data.FtpUsername, gameServerApiResponse.Result.Data.FtpPassword, gameServerApiResponse.Result.Data.FtpPort ?? 21);
                ftpClient.ValidateCertificate += (control, e) =>
                {
                    if (e.Certificate.GetCertHashString().Equals(configuration["xtremeidiots_ftp_certificate_thumbprint"]))
                    { // Account for self-signed FTP certificate for self-hosted servers
                        e.Accept = true;
                    }
                };

                await ftpClient.AutoConnect();

                var mapDirectoryPath = $"usermaps/{mapName}";

                if (await ftpClient.DirectoryExists(mapDirectoryPath))
                {
                    await ftpClient.DeleteDirectory(mapDirectoryPath);
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
                return new ApiResponse(new ApiError(ErrorCodes.FTP_OPERATION_FAILED, "Failed to delete map directory from the game server's FTP host.")).ToApiResult();
            }
            finally
            {
                ftpClient?.Dispose();
            }
        }
    }
