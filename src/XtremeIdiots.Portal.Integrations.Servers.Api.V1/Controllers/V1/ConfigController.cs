using System.Text.RegularExpressions;
using Asp.Versioning;
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

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1;

[ApiController]
[Authorize(Roles = "ServiceAccount")]
[ApiVersion(ApiVersions.V1)]
[Route("v{version:apiVersion}")]
public class ConfigController(
    ILogger<ConfigController> logger,
    IGameServerFileTransportFactory fileTransportFactory,
    TelemetryClient telemetryClient) : Controller, IConfigApi
{
        private static readonly Regex SafeConfigFilePathRegex = new(@"^/?([a-zA-Z0-9_\-]+/)*[a-zA-Z0-9_\-]+\.cfg$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        private static readonly Regex SafeVariableNameRegex = new(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        private const string ManagedCommentPrefix = "// [Portal] ";

        private static bool IsAllowedFilePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;
            if (filePath.Contains("..") || filePath.Contains('\\'))
                return false;

            return SafeConfigFilePathRegex.IsMatch(filePath);
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
                return new ApiResponse<ConfigFileContentDto>(new ApiError(ErrorCodes.INVALID_REQUEST, $"File path '{filePath}' is not valid. Must be a .cfg path (e.g. 'server.cfg' or '/mods/mymod/configs/mapcontrol.cfg'). Backslashes and path traversal (..) are not allowed.")).ToBadRequestResult();

            var sessionResult = await fileTransportFactory.CreateSession(gameServerId, cancellationToken).ConfigureAwait(false);
            if (sessionResult.IsNotFound)
                return new ApiResponse<ConfigFileContentDto>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

            if (!sessionResult.IsSuccess || sessionResult.Result?.Data == null)
            {
                var error = sessionResult.Result?.Errors?.FirstOrDefault();
                if (string.Equals(error?.Code, ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, StringComparison.OrdinalIgnoreCase))
                    return new ApiResponse<ConfigFileContentDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, "Failed to connect to the game server file transport host to retrieve config file.")).ToApiResult();

                return new ApiResponse<ConfigFileContentDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CREDENTIALS_MISSING, "The game server does not have file transport credentials configured.")).ToBadRequestResult();
            }

            await using var session = sessionResult.Result.Data;

            var operation = telemetryClient.StartOperation<DependencyTelemetry>("GetConfigFile");
            operation.Telemetry.Type = session.Transport.TelemetryType;
            operation.Telemetry.Target = session.Transport.TelemetryTarget;

            try
            {
                if (!await session.FileExists(filePath, cancellationToken).ConfigureAwait(false))
                    return new ApiResponse<ConfigFileContentDto>(new ApiError(ErrorCodes.CONFIG_FILE_NOT_FOUND, $"The config file '{filePath}' was not found on the server.")).ToNotFoundResult();

                var content = await session.DownloadBytes(filePath, cancellationToken).ConfigureAwait(false);
                var contentString = System.Text.Encoding.UTF8.GetString(content);

                var data = new ConfigFileContentDto(filePath, contentString);
                return new ApiResponse<ConfigFileContentDto>(data).ToApiResult();
            }
            catch (Exception ex)
            {
                operation.Telemetry.Success = false;
                operation.Telemetry.ResultCode = ex.Message;
                telemetryClient.TrackException(ex);

                logger.LogError(ex, "Failed to retrieve config file from file transport host for game server {GameServerId}", gameServerId);
                return new ApiResponse<ConfigFileContentDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, "Failed to connect to the game server file transport host to retrieve config file.")).ToApiResult();
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

            var response = await ((IConfigApi)this).UpdateConfigVariable(gameServerId, filePath, variableName, request.Value, request.CommentLines);

            return response.ToHttpResult();
        }

        async Task<ApiResult> IConfigApi.UpdateConfigVariable(Guid gameServerId, string filePath, string variableName, string value, string[]? commentLines, CancellationToken cancellationToken)
        {
            if (!IsAllowedFilePath(filePath))
                return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, $"File path '{filePath}' is not valid. Must be a .cfg path (e.g. 'server.cfg' or '/mods/mymod/configs/mapcontrol.cfg'). Backslashes and path traversal (..) are not allowed.")).ToBadRequestResult();

            if (string.IsNullOrWhiteSpace(variableName) || !SafeVariableNameRegex.IsMatch(variableName))
                return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Variable name must be a valid identifier (letters, digits, underscores).")).ToBadRequestResult();

            // Reject values containing characters that would corrupt the config file
            if (value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
                return new ApiResponse(new ApiError(ErrorCodes.INVALID_REQUEST, "Value must not contain double quotes or newline characters.")).ToBadRequestResult();

            var sessionResult = await fileTransportFactory.CreateSession(gameServerId, cancellationToken).ConfigureAwait(false);
            if (sessionResult.IsNotFound)
                return new ApiResponse(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

            if (!sessionResult.IsSuccess || sessionResult.Result?.Data == null)
            {
                var error = sessionResult.Result?.Errors?.FirstOrDefault();
                if (string.Equals(error?.Code, ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, StringComparison.OrdinalIgnoreCase))
                    return new ApiResponse(new ApiError(ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, "Failed to connect to the game server file transport host to update config variable.")).ToApiResult();

                return new ApiResponse(new ApiError(ErrorCodes.FILE_TRANSPORT_CREDENTIALS_MISSING, "The game server does not have file transport credentials configured.")).ToBadRequestResult();
            }

            await using var session = sessionResult.Result.Data;

            var operation = telemetryClient.StartOperation<DependencyTelemetry>("UpdateConfigVariable");
            operation.Telemetry.Type = session.Transport.TelemetryType;
            operation.Telemetry.Target = session.Transport.TelemetryTarget;

            try
            {
                if (!await session.FileExists(filePath, cancellationToken).ConfigureAwait(false))
                    return new ApiResponse(new ApiError(ErrorCodes.CONFIG_FILE_NOT_FOUND, $"The config file '{filePath}' was not found on the server.")).ToNotFoundResult();

                var contentBytes = await session.DownloadBytes(filePath, cancellationToken).ConfigureAwait(false);
                var content = System.Text.Encoding.UTF8.GetString(contentBytes);

                var regex = ConfigVariableRegex(variableName);
                var match = regex.Match(content);

                if (!match.Success)
                    return new ApiResponse(new ApiError(ErrorCodes.CONFIG_VARIABLE_NOT_FOUND, $"The variable '{variableName}' was not found in the config file '{filePath}'.")).ToBadRequestResult();

                // Detect the file's newline style
                var newline = content.Contains("\r\n") ? "\r\n" : "\n";

                // Replace only the first match, and escape $ in value to prevent regex replacement interpretation
                var escapedValue = value.Replace("$", "$$");
                var updatedContent = regex.Replace(content, $"${{1}}\"{escapedValue}\"", 1);

                // Handle managed comment block above the variable
                if (commentLines is not null)
                {
                    updatedContent = UpsertManagedCommentBlock(updatedContent, variableName, commentLines, newline);
                }
                var updatedBytes = System.Text.Encoding.UTF8.GetBytes(updatedContent);
                await session.UploadBytes(filePath, updatedBytes, cancellationToken).ConfigureAwait(false);

                return new ApiResponse().ToApiResult();
            }
            catch (Exception ex)
            {
                operation.Telemetry.Success = false;
                operation.Telemetry.ResultCode = ex.Message;
                telemetryClient.TrackException(ex);

                logger.LogError(ex, "Failed to update config variable on file transport host for game server {GameServerId}", gameServerId);
                return new ApiResponse(new ApiError(ErrorCodes.CONFIG_OPERATION_FAILED, "Failed to update config variable on the game server file transport host.")).ToApiResult();
            }
            finally
            {
                telemetryClient.StopOperation(operation);
            }
        }

        /// <summary>
        /// Inserts or replaces a managed comment block (lines prefixed with "// [Portal] ") above the
        /// first occurrence of the given config variable. Pass an empty array to remove any existing block.
        /// </summary>
        internal static string UpsertManagedCommentBlock(string content, string variableName, string[] commentLines, string newline)
        {
            var lines = content.Split(newline);
            var varRegex = ConfigVariableRegex(variableName);

            // Find the first line that matches the variable
            var varLineIndex = -1;
            for (var i = 0; i < lines.Length; i++)
            {
                if (varRegex.IsMatch(lines[i]))
                {
                    varLineIndex = i;
                    break;
                }
            }

            if (varLineIndex < 0)
                return content; // Variable not found, return unchanged

            // Collect indices of managed comment lines above the variable (may have gaps)
            var managedLineIndices = new HashSet<int>();
            for (var i = varLineIndex - 1; i >= 0; i--)
            {
                var trimmed = lines[i].TrimStart();
                if (trimmed.StartsWith(ManagedCommentPrefix.TrimStart()))
                {
                    managedLineIndices.Add(i);
                }
                else if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("//"))
                {
                    // Skip blank lines and regular comments to find non-contiguous managed blocks
                    continue;
                }
                else
                {
                    // Stop at the first non-comment, non-blank line
                    break;
                }
            }

            // Build the new set of lines
            var result = new List<string>();

            // Lines before the variable, excluding old managed comment lines
            for (var i = 0; i < varLineIndex; i++)
            {
                if (!managedLineIndices.Contains(i))
                    result.Add(lines[i]);
            }

            // Insert new managed comment lines (if any)
            foreach (var line in commentLines)
            {
                if (!string.IsNullOrEmpty(line))
                    result.Add($"{ManagedCommentPrefix}{line}");
            }

            // The variable line and everything after it
            for (var i = varLineIndex; i < lines.Length; i++)
                result.Add(lines[i]);

            return string.Join(newline, result);
        }
    }
