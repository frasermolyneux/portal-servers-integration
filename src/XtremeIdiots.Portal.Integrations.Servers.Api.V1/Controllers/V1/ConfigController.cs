using System.Text.RegularExpressions;
using Asp.Versioning;
using FluentFTP;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MX.Api.Abstractions;
using MX.Api.Web.Extensions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Config;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Constants;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1;

[ApiController]
[Authorize(Roles = "ServiceAccount")]
[ApiVersion(ApiVersions.V1)]
[Route("v{version:apiVersion}")]
public class ConfigController(
    ILogger<ConfigController> logger,
    IRepositoryApiClient repositoryApiClient,
    TelemetryClient telemetryClient,
    IConfiguration configuration) : Controller, IConfigApi
{
        private static readonly Regex SafeConfigFileNameRegex = new(@"^[a-zA-Z0-9_\-]+\.cfg$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        private static readonly Regex SafeVariableNameRegex = new(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        private static bool IsAllowedFilePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;
            if (filePath.Contains("..") || filePath.Contains('/') || filePath.Contains('\\') || Path.IsPathRooted(filePath))
                return false;

            return SafeConfigFileNameRegex.IsMatch(filePath);
        }

        private static Regex ConfigVariableRegex(string variableName)
        {
            var escaped = Regex.Escape(variableName);
            return new Regex($@"^(\s*set\s+{escaped}\s+)""([^""]*)""", RegexOptions.IgnoreCase | RegexOptions.Multiline, TimeSpan.FromSeconds(1));
        }

        [HttpGet]
        [Route("config/{gameServerId}/file")]
        public async Task<IActionResult> GetConfigFile(Guid gameServerId, [FromQuery] string filePath)
        {
            var response = await ((IConfigApi)this).GetConfigFile(gameServerId, filePath);

            return response.ToHttpResult();
        }

        async Task<ApiResult<ConfigFileContentDto>> IConfigApi.GetConfigFile(Guid gameServerId, string filePath, CancellationToken cancellationToken)
        {
            if (!IsAllowedFilePath(filePath))
                return new ApiResponse<ConfigFileContentDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "File path must be a single .cfg filename (e.g. 'server.cfg'). Path separators and traversal are not allowed.")).ToBadRequestResult();

            var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

            if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
                return new ApiResponse<ConfigFileContentDto>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

            var ftpConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "ftp").ConfigureAwait(false);
            var ftpCreds = FtpConfigResolver.ParseFromConfig(ftpConfigResult?.Result?.Data?.Configuration);
            if (ftpCreds == null)
                return new ApiResponse<ConfigFileContentDto>(new ApiError(ErrorCodes.FTP_CREDENTIALS_MISSING, "The game server does not have FTP credentials configured.")).ToBadRequestResult();

            var operation = telemetryClient.StartOperation<DependencyTelemetry>("GetConfigFile");
            operation.Telemetry.Type = "FTP";
            operation.Telemetry.Target = $"{ftpCreds.Hostname}:{ftpCreds.Port}";

            try
            {
                await using var ftpClient = new AsyncFtpClient(ftpCreds.Hostname, ftpCreds.Username, ftpCreds.Password, ftpCreds.Port);
                ftpClient.ValidateCertificate += (control, e) =>
                {
                    if (e.Certificate.GetCertHashString().Equals(configuration["xtremeidiots_ftp_certificate_thumbprint"]))
                    {
                        e.Accept = true;
                    }
                };

                await ftpClient.AutoConnect();

                if (!await ftpClient.FileExists(filePath))
                    return new ApiResponse<ConfigFileContentDto>(new ApiError(ErrorCodes.CONFIG_FILE_NOT_FOUND, $"The config file '{filePath}' was not found on the server.")).ToNotFoundResult();

                var content = await ftpClient.DownloadBytes(filePath, cancellationToken);
                var contentString = System.Text.Encoding.UTF8.GetString(content);

                var data = new ConfigFileContentDto(filePath, contentString);
                return new ApiResponse<ConfigFileContentDto>(data).ToApiResult();
            }
            catch (Exception ex)
            {
                operation.Telemetry.Success = false;
                operation.Telemetry.ResultCode = ex.Message;
                telemetryClient.TrackException(ex);

                logger.LogError(ex, "Failed to retrieve config file from FTP host for game server {GameServerId}", gameServerId);
                return new ApiResponse<ConfigFileContentDto>(new ApiError(ErrorCodes.FTP_CONNECTION_FAILED, "Failed to connect to the game server's FTP host to retrieve config file.")).ToApiResult();
            }
            finally
            {
                telemetryClient.StopOperation(operation);
            }
        }

        [HttpPut]
        [Route("config/{gameServerId}/file/variable")]
        public async Task<IActionResult> UpdateConfigVariable(Guid gameServerId, [FromQuery] string filePath, [FromQuery] string variableName, [FromBody] UpdateConfigVariableRequest? request)
        {
            if (request == null)
            {
                return BadRequest(new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Request body cannot be null.")).ToApiResult());
            }

            var response = await ((IConfigApi)this).UpdateConfigVariable(gameServerId, filePath, variableName, request.Value);

            return response.ToHttpResult();
        }

        async Task<ApiResult> IConfigApi.UpdateConfigVariable(Guid gameServerId, string filePath, string variableName, string value, CancellationToken cancellationToken)
        {
            if (!IsAllowedFilePath(filePath))
                return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "File path must be a single .cfg filename (e.g. 'server.cfg'). Path separators and traversal are not allowed.")).ToBadRequestResult();

            if (string.IsNullOrWhiteSpace(variableName) || !SafeVariableNameRegex.IsMatch(variableName))
                return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Variable name must be a valid identifier (letters, digits, underscores).")).ToBadRequestResult();

            // Reject values containing characters that would corrupt the config file
            if (value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
                return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Value must not contain double quotes or newline characters.")).ToBadRequestResult();

            var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

            if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
                return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

            var ftpConfigResult = await repositoryApiClient.GameServerConfigurations.V1.GetConfiguration(gameServerId, "ftp").ConfigureAwait(false);
            var ftpCreds = FtpConfigResolver.ParseFromConfig(ftpConfigResult?.Result?.Data?.Configuration);
            if (ftpCreds == null)
                return new ApiResponse(new ApiError(ErrorCodes.FTP_CREDENTIALS_MISSING, "The game server does not have FTP credentials configured.")).ToBadRequestResult();

            var operation = telemetryClient.StartOperation<DependencyTelemetry>("UpdateConfigVariable");
            operation.Telemetry.Type = "FTP";
            operation.Telemetry.Target = $"{ftpCreds.Hostname}:{ftpCreds.Port}";

            try
            {
                await using var ftpClient = new AsyncFtpClient(ftpCreds.Hostname, ftpCreds.Username, ftpCreds.Password, ftpCreds.Port);
                ftpClient.ValidateCertificate += (control, e) =>
                {
                    if (e.Certificate.GetCertHashString().Equals(configuration["xtremeidiots_ftp_certificate_thumbprint"]))
                    {
                        e.Accept = true;
                    }
                };

                await ftpClient.AutoConnect();

                if (!await ftpClient.FileExists(filePath))
                    return new ApiResponse(new ApiError(ErrorCodes.CONFIG_FILE_NOT_FOUND, $"The config file '{filePath}' was not found on the server.")).ToNotFoundResult();

                var contentBytes = await ftpClient.DownloadBytes(filePath, cancellationToken);
                var content = System.Text.Encoding.UTF8.GetString(contentBytes);

                var regex = ConfigVariableRegex(variableName);
                var match = regex.Match(content);

                if (!match.Success)
                    return new ApiResponse(new ApiError(ErrorCodes.CONFIG_VARIABLE_NOT_FOUND, $"The variable '{variableName}' was not found in the config file '{filePath}'.")).ToBadRequestResult();

                // Replace only the first match, and escape $ in value to prevent regex replacement interpretation
                var escapedValue = value.Replace("$", "$$");
                var updatedContent = regex.Replace(content, $"${{1}}\"{escapedValue}\"", 1);
                var updatedBytes = System.Text.Encoding.UTF8.GetBytes(updatedContent);

                using var stream = new MemoryStream(updatedBytes);
                await ftpClient.UploadStream(stream, filePath, FtpRemoteExists.Overwrite, true, null, cancellationToken);

                return new ApiResponse().ToApiResult();
            }
            catch (Exception ex)
            {
                operation.Telemetry.Success = false;
                operation.Telemetry.ResultCode = ex.Message;
                telemetryClient.TrackException(ex);

                logger.LogError(ex, "Failed to update config variable on FTP host for game server {GameServerId}", gameServerId);
                return new ApiResponse(new ApiError(ErrorCodes.CONFIG_OPERATION_FAILED, "Failed to update config variable on the game server's FTP host.")).ToApiResult();
            }
            finally
            {
                telemetryClient.StopOperation(operation);
            }
        }
    }
