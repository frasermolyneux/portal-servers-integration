
using System.Net;

using FluentFTP;

using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using MxIO.ApiClient.Abstractions;
using MxIO.ApiClient.WebExtensions;

using XtremeIdiots.Portal.RepositoryApiClient.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Maps;
using Asp.Versioning;
using XtremeIdiots.Portal.RepositoryApi.Abstractions.Constants;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1
{
    [ApiController]
    [Authorize(Roles = "ServiceAccount")]
    [ApiVersion(ApiVersions.V1)]
    [Route("api/v{version:apiVersion}")]
    public class MapsController : Controller, IMapsApi
    {
        private readonly ILogger<MapsController> logger;
        private readonly IRepositoryApiClient repositoryApiClient;
        private readonly TelemetryClient telemetryClient;
        private readonly IConfiguration configuration;

        public MapsController(
            ILogger<MapsController> logger,
            IRepositoryApiClient repositoryApiClient,
            TelemetryClient telemetryClient,
            IConfiguration configuration)
        {
            this.logger = logger;
            this.repositoryApiClient = repositoryApiClient;
            this.telemetryClient = telemetryClient;
            this.configuration = configuration;
        }

        [HttpGet]
        [Route("maps/{gameServerId}/host/loaded")]
        public async Task<IActionResult> GetLoadedServerMapsFromHost(Guid gameServerId)
        {
            var response = await ((IMapsApi)this).GetLoadedServerMapsFromHost(gameServerId);

            return response.ToHttpResult();
        }

        async Task<ApiResponseDto<ServerMapsCollectionDto>> IMapsApi.GetLoadedServerMapsFromHost(Guid gameServerId)
        {
            var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

            if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result == null)
                return new ApiResponseDto<ServerMapsCollectionDto>(HttpStatusCode.NotFound);

            var operation = telemetryClient.StartOperation<DependencyTelemetry>("GetFileList");
            operation.Telemetry.Type = $"FTP";
            operation.Telemetry.Target = $"{gameServerApiResponse.Result.FtpHostname}:{gameServerApiResponse.Result.FtpPort}";

            AsyncFtpClient? ftpClient = null;

            try
            {
                ftpClient = new AsyncFtpClient(gameServerApiResponse.Result.FtpHostname, gameServerApiResponse.Result.FtpUsername, gameServerApiResponse.Result.FtpPassword, gameServerApiResponse.Result.FtpPort.Value);
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

                var result = new ServerMapsCollectionDto
                {
                    TotalRecords = files.Count(),
                    FilteredRecords = files.Count(),
                    Entries = files.Select(f => new ServerMapDto(f.Name, f.FullName, f.Modified)).ToList()
                };

                return new ApiResponseDto<ServerMapsCollectionDto>(HttpStatusCode.OK, result);
            }
            catch (Exception ex)
            {
                operation.Telemetry.Success = false;
                operation.Telemetry.ResultCode = ex.Message;
                telemetryClient.TrackException(ex);

                throw;
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

        async Task<ApiResponseDto> IMapsApi.PushServerMapToHost(Guid gameServerId, string mapName)
        {
            var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

            if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result == null)
                return new ApiResponseDto<ServerMapsCollectionDto>(HttpStatusCode.NotFound);

            var mapApiResponse = await repositoryApiClient.Maps.V1.GetMap(gameServerApiResponse.Result.GameType, mapName);

            if (mapApiResponse.IsNotFound || mapApiResponse.Result == null)
                return new ApiResponseDto<ServerMapsCollectionDto>(HttpStatusCode.NotFound, null, new List<string> { "Map could not be found in the database" });

            if (!mapApiResponse.Result.MapFiles.Any())
                return new ApiResponseDto<ServerMapsCollectionDto>(HttpStatusCode.NotFound, null, new List<string> { "There are no map files to be pushed to the server" });

            AsyncFtpClient? ftpClient = null;

            try
            {
                ftpClient = new AsyncFtpClient(gameServerApiResponse.Result.FtpHostname, gameServerApiResponse.Result.FtpUsername, gameServerApiResponse.Result.FtpPassword, gameServerApiResponse.Result.FtpPort.Value);
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
                    logger.LogInformation($"Directory {mapDirectoryPath} already exists on the server, skipping sync");
                    return new ApiResponseDto(HttpStatusCode.OK);
                }
                else
                {
                    await ftpClient.CreateDirectory(mapDirectoryPath);

                    foreach (var file in mapApiResponse.Result.MapFiles)
                    {
                        using (var httpClient = new HttpClient())
                        {
                            var filePath = Path.Join(Path.GetTempPath(), file.FileName);
                            using (var stream = System.IO.File.Create(filePath))
                                await (await httpClient.GetStreamAsync(file.Url)).CopyToAsync(stream);

                            await ftpClient.UploadFile(filePath, $"{mapDirectoryPath}/{file.FileName}");
                        }
                    }

                    return new ApiResponseDto(HttpStatusCode.OK);
                }

            }
            catch (Exception ex)
            {
                telemetryClient.TrackException(ex);
                throw;
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

        async Task<ApiResponseDto> IMapsApi.DeleteServerMapFromHost(Guid gameServerId, string mapName)
        {
            var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

            if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result == null)
                return new ApiResponseDto<ServerMapsCollectionDto>(HttpStatusCode.NotFound);

            AsyncFtpClient? ftpClient = null;

            try
            {
                ftpClient = new AsyncFtpClient(gameServerApiResponse.Result.FtpHostname, gameServerApiResponse.Result.FtpUsername, gameServerApiResponse.Result.FtpPassword, gameServerApiResponse.Result.FtpPort.Value);
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
                    return new ApiResponseDto(HttpStatusCode.OK);
                }
                else
                {
                    logger.LogInformation($"Directory {mapDirectoryPath} does not exist on the server, skipping delete");
                    return new ApiResponseDto(HttpStatusCode.OK);
                }

            }
            catch (Exception ex)
            {
                telemetryClient.TrackException(ex);
                throw;
            }
            finally
            {
                ftpClient?.Dispose();
            }



        }
    }
}
