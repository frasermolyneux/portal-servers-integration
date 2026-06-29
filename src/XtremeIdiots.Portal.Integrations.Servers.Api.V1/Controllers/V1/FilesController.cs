using System.Net;
using System.Text;
using System.Globalization;
using Asp.Versioning;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MX.Api.Abstractions;
using MX.Api.Web.Extensions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Files;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Constants;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1;

[ApiController]
[Authorize(Roles = "ServiceAccount")]
[ApiVersion(ApiVersions.V1)]
[Route("v{version:apiVersion}")]
public class FilesController(
    ILogger<FilesController> logger,
    IGameServerFileTransportFactory fileTransportFactory,
    TelemetryClient telemetryClient) : Controller, IFilesApi
{
    [HttpGet]
    [Route("files/{gameServerId}/entries")]
    public async Task<IActionResult> ListEntries(Guid gameServerId, [FromQuery] ListEntriesQueryDto query, CancellationToken cancellationToken = default)
    {
        var response = await ((IFilesApi)this).ListEntries(gameServerId, query, cancellationToken);
        return response.ToHttpResult();
    }

    [HttpGet]
    [Route("files/{gameServerId}/content")]
    public async Task<IActionResult> GetContent(Guid gameServerId, [FromQuery] GetFileContentQueryDto query, CancellationToken cancellationToken = default)
    {
        var response = await ((IFilesApi)this).GetContent(gameServerId, query, cancellationToken);
        return response.ToHttpResult();
    }

    [HttpGet]
    [Route("files/{gameServerId}/metadata")]
    public async Task<IActionResult> GetMetadata(Guid gameServerId, [FromQuery] GetEntryMetadataQueryDto query, CancellationToken cancellationToken = default)
    {
        var response = await ((IFilesApi)this).GetMetadata(gameServerId, query, cancellationToken);
        return response.ToHttpResult();
    }

    [HttpPut]
    [Route("files/{gameServerId}/content")]
    public async Task<IActionResult> PutContent(Guid gameServerId, [FromBody] PutFileContentRequestDto request, CancellationToken cancellationToken = default)
    {
        var response = await ((IFilesApi)this).PutContent(gameServerId, request, cancellationToken);
        return response.ToHttpResult();
    }

    [HttpDelete]
    [Route("files/{gameServerId}/content")]
    public async Task<IActionResult> DeleteContent(Guid gameServerId, [FromQuery] DeleteFileQueryDto query, CancellationToken cancellationToken = default)
    {
        var response = await ((IFilesApi)this).DeleteContent(gameServerId, query, cancellationToken);
        return response.ToHttpResult();
    }

    [HttpPost]
    [Route("files/{gameServerId}/directories")]
    public async Task<IActionResult> CreateDirectory(Guid gameServerId, [FromBody] CreateDirectoryRequestDto request, CancellationToken cancellationToken = default)
    {
        var response = await ((IFilesApi)this).CreateDirectory(gameServerId, request, cancellationToken);
        return response.ToHttpResult();
    }

    [HttpDelete]
    [Route("files/{gameServerId}/directories")]
    public async Task<IActionResult> DeleteDirectory(Guid gameServerId, [FromQuery] DeleteDirectoryQueryDto query, CancellationToken cancellationToken = default)
    {
        var response = await ((IFilesApi)this).DeleteDirectory(gameServerId, query, cancellationToken);
        return response.ToHttpResult();
    }

    [HttpPatch]
    [Route("files/{gameServerId}/entries")]
    public async Task<IActionResult> PatchEntry(Guid gameServerId, [FromBody] PatchFileEntryRequestDto request, CancellationToken cancellationToken = default)
    {
        var response = await ((IFilesApi)this).PatchEntry(gameServerId, request, cancellationToken);
        return response.ToHttpResult();
    }

    async Task<ApiResult<FileEntriesCollectionDto>> IFilesApi.ListEntries(Guid gameServerId, ListEntriesQueryDto query, CancellationToken cancellationToken)
    {
        var normalizedPath = NormalizePath(query.Path);
        if (!IsPathSafe(normalizedPath))
        {
            return new ApiResponse<FileEntriesCollectionDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "The path contains invalid traversal segments.")).ToBadRequestResult();
        }

        if (query.PageSize is <= 0)
        {
            return new ApiResponse<FileEntriesCollectionDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "PageSize must be greater than zero when specified.")).ToBadRequestResult();
        }

        var offset = 0;
        if (!string.IsNullOrWhiteSpace(query.ContinuationToken)
            && (!int.TryParse(query.ContinuationToken, NumberStyles.None, CultureInfo.InvariantCulture, out offset) || offset < 0))
        {
            return new ApiResponse<FileEntriesCollectionDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "ContinuationToken must be a non-negative integer.")).ToBadRequestResult();
        }

        var sessionResult = await fileTransportFactory.CreateSession(gameServerId, cancellationToken).ConfigureAwait(false);
        if (sessionResult.IsNotFound)
        {
            return new ApiResponse<FileEntriesCollectionDto>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        if (!sessionResult.IsSuccess || sessionResult.Result?.Data == null)
        {
            var error = sessionResult.Result?.Errors?.FirstOrDefault();
            if (string.Equals(error?.Code, ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, StringComparison.OrdinalIgnoreCase))
            {
                return new ApiResponse<FileEntriesCollectionDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, "Failed to connect to the game server file transport host to list entries.")).ToApiResult();
            }

            return new ApiResponse<FileEntriesCollectionDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CREDENTIALS_MISSING, "The game server does not have file transport credentials configured.")).ToBadRequestResult();
        }

        await using var session = sessionResult.Result.Data;
        using var operation = telemetryClient.StartOperation<DependencyTelemetry>("FilesListEntries");
        operation.Telemetry.Type = session.Transport.TelemetryType;
        operation.Telemetry.Target = session.Transport.TelemetryTarget;

        try
        {
            var entries = query.Recursive
                ? await GetRecursiveListing(session, normalizedPath, cancellationToken).ConfigureAwait(false)
                : await session.GetListing(normalizedPath, cancellationToken).ConfigureAwait(false);

            var filteredEntries = entries
                .Where(entry => query.IncludeHidden || !entry.Name.StartsWith('.'))
                .OrderBy(entry => entry.IsDirectory ? 0 : 1)
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => NormalizePath(entry.FullPath), StringComparer.OrdinalIgnoreCase)
                .Select(entry => new FileEntryDto(
                    entry.Name,
                    entry.FullPath,
                    entry.IsDirectory ? FileEntryType.Directory : FileEntryType.File,
                    entry.Size,
                    entry.Modified))
                .ToList();

            var pageSize = query.PageSize ?? filteredEntries.Count;
            var pagedEntries = filteredEntries.Skip(offset).Take(pageSize).ToList();
            var nextOffset = offset + pagedEntries.Count;
            var continuationToken = query.PageSize.HasValue && nextOffset < filteredEntries.Count
                ? nextOffset.ToString(CultureInfo.InvariantCulture)
                : null;

            var dto = new FileEntriesCollectionDto(normalizedPath, GetParentPath(normalizedPath), pagedEntries, continuationToken);
            return new ApiResponse<FileEntriesCollectionDto>(dto).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);
            logger.LogError(ex, "Failed to list files for game server {GameServerId} at path {Path}", gameServerId, normalizedPath);
            return new ApiResponse<FileEntriesCollectionDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_OPERATION_FAILED, "Failed to list entries from the game server file transport host.")).ToApiResult();
        }
    }

    async Task<ApiResult<FileContentDto>> IFilesApi.GetContent(Guid gameServerId, GetFileContentQueryDto query, CancellationToken cancellationToken)
    {
        var normalizedPath = NormalizePath(query.Path);
        if (!IsPathSafe(normalizedPath))
        {
            return new ApiResponse<FileContentDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "The path contains invalid traversal segments.")).ToBadRequestResult();
        }

        var sessionResult = await fileTransportFactory.CreateSession(gameServerId, cancellationToken).ConfigureAwait(false);
        if (sessionResult.IsNotFound)
        {
            return new ApiResponse<FileContentDto>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        if (!sessionResult.IsSuccess || sessionResult.Result?.Data == null)
        {
            var error = sessionResult.Result?.Errors?.FirstOrDefault();
            if (string.Equals(error?.Code, ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, StringComparison.OrdinalIgnoreCase))
            {
                return new ApiResponse<FileContentDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, "Failed to connect to the game server file transport host to read file content.")).ToApiResult();
            }

            return new ApiResponse<FileContentDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CREDENTIALS_MISSING, "The game server does not have file transport credentials configured.")).ToBadRequestResult();
        }

        await using var session = sessionResult.Result.Data;
        using var operation = telemetryClient.StartOperation<DependencyTelemetry>("FilesGetContent");
        operation.Telemetry.Type = session.Transport.TelemetryType;
        operation.Telemetry.Target = session.Transport.TelemetryTarget;

        try
        {
            if (await session.DirectoryExists(normalizedPath, cancellationToken).ConfigureAwait(false))
            {
                return new ApiResult<FileContentDto>(
                    HttpStatusCode.Conflict,
                    new ApiResponse<FileContentDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_OPERATION_FAILED, "The path points to a directory, not a file.")));
            }

            if (!await session.FileExists(normalizedPath, cancellationToken).ConfigureAwait(false))
            {
                return new ApiResponse<FileContentDto>(new ApiError(ErrorCodes.CONFIG_FILE_NOT_FOUND, $"The file '{normalizedPath}' was not found on the server.")).ToNotFoundResult();
            }

            var bytes = await session.DownloadBytes(normalizedPath, cancellationToken).ConfigureAwait(false);
            if (query.RangeStart.HasValue || query.RangeEnd.HasValue)
            {
                var rangeStart = query.RangeStart ?? 0;
                var rangeEnd = query.RangeEnd ?? (bytes.LongLength - 1);

                if (rangeStart < 0 || rangeEnd < 0 || rangeStart > rangeEnd || rangeStart >= bytes.LongLength)
                {
                    return new ApiResult<FileContentDto>(
                        HttpStatusCode.RequestedRangeNotSatisfiable,
                        new ApiResponse<FileContentDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "The requested byte range is invalid for this file.")));
                }

                if (rangeEnd >= bytes.LongLength)
                {
                    rangeEnd = bytes.LongLength - 1;
                }

                var rangeLength = checked((int)(rangeEnd - rangeStart + 1));
                bytes = bytes.AsSpan((int)rangeStart, rangeLength).ToArray();
            }

            if (query.Mode == FileContentMode.Binary)
            {
                var binaryDto = new FileContentDto(normalizedPath, FileContentMode.Binary, "base64", bytes.LongLength, null, Convert.ToBase64String(bytes));
                return new ApiResponse<FileContentDto>(binaryDto).ToApiResult();
            }

            Encoding textEncoding;
            try
            {
                textEncoding = Encoding.GetEncoding(query.Encoding);
            }
            catch (Exception)
            {
                return new ApiResponse<FileContentDto>(new ApiError(ErrorCodes.INVALID_REQUEST, $"The encoding '{query.Encoding}' is invalid or unsupported.")).ToBadRequestResult();
            }

            var content = textEncoding.GetString(bytes);
            var textDto = new FileContentDto(normalizedPath, FileContentMode.Text, query.Encoding, bytes.LongLength, content, null);
            return new ApiResponse<FileContentDto>(textDto).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);
            logger.LogError(ex, "Failed to read file content for game server {GameServerId} at path {Path}", gameServerId, normalizedPath);
            return new ApiResponse<FileContentDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_OPERATION_FAILED, "Failed to read file content from the game server file transport host.")).ToApiResult();
        }
    }

    async Task<ApiResult<FileEntryMetadataDto>> IFilesApi.GetMetadata(Guid gameServerId, GetEntryMetadataQueryDto query, CancellationToken cancellationToken)
    {
        var normalizedPath = NormalizePath(query.Path);
        if (!IsPathSafe(normalizedPath))
        {
            return new ApiResponse<FileEntryMetadataDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "The path contains invalid traversal segments.")).ToBadRequestResult();
        }

        var sessionResult = await fileTransportFactory.CreateSession(gameServerId, cancellationToken).ConfigureAwait(false);
        if (sessionResult.IsNotFound)
        {
            return new ApiResponse<FileEntryMetadataDto>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        if (!sessionResult.IsSuccess || sessionResult.Result?.Data == null)
        {
            var error = sessionResult.Result?.Errors?.FirstOrDefault();
            if (string.Equals(error?.Code, ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, StringComparison.OrdinalIgnoreCase))
            {
                return new ApiResponse<FileEntryMetadataDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, "Failed to connect to the game server file transport host to read metadata.")).ToApiResult();
            }

            return new ApiResponse<FileEntryMetadataDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CREDENTIALS_MISSING, "The game server does not have file transport credentials configured.")).ToBadRequestResult();
        }

        await using var session = sessionResult.Result.Data;
        using var operation = telemetryClient.StartOperation<DependencyTelemetry>("FilesGetMetadata");
        operation.Telemetry.Type = session.Transport.TelemetryType;
        operation.Telemetry.Target = session.Transport.TelemetryTarget;

        try
        {
            var parentPath = GetParentPath(normalizedPath) ?? "/";
            var entries = await session.GetListing(parentPath, cancellationToken).ConfigureAwait(false);
            var match = entries.FirstOrDefault(entry => string.Equals(NormalizePath(entry.FullPath), normalizedPath, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                return new ApiResponse<FileEntryMetadataDto>(new ApiError(ErrorCodes.CONFIG_FILE_NOT_FOUND, $"The path '{normalizedPath}' was not found on the server.")).ToNotFoundResult();
            }

            var dto = new FileEntryMetadataDto(
                match.Name,
                match.FullPath,
                match.IsDirectory ? FileEntryType.Directory : FileEntryType.File,
                match.Size,
                match.Modified);

            return new ApiResponse<FileEntryMetadataDto>(dto).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);
            logger.LogError(ex, "Failed to read file metadata for game server {GameServerId} at path {Path}", gameServerId, normalizedPath);
            return new ApiResponse<FileEntryMetadataDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_OPERATION_FAILED, "Failed to read metadata from the game server file transport host.")).ToApiResult();
        }
    }

    async Task<ApiResult<FileMutationResultDto>> IFilesApi.PutContent(Guid gameServerId, PutFileContentRequestDto request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "Request body cannot be null.")).ToBadRequestResult();
        }

        if (string.IsNullOrWhiteSpace(request.Path))
        {
            return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "Path is required.")).ToBadRequestResult();
        }

        var normalizedPath = NormalizePath(request.Path);
        if (!IsPathSafe(normalizedPath))
        {
            return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "The path contains invalid traversal segments.")).ToBadRequestResult();
        }

        if (normalizedPath == "/")
        {
            return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "Path cannot be the root directory for file content operations.")).ToBadRequestResult();
        }

        var sessionResult = await fileTransportFactory.CreateSession(gameServerId, cancellationToken).ConfigureAwait(false);
        if (sessionResult.IsNotFound)
        {
            return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        if (!sessionResult.IsSuccess || sessionResult.Result?.Data == null)
        {
            var error = sessionResult.Result?.Errors?.FirstOrDefault();
            if (string.Equals(error?.Code, ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, StringComparison.OrdinalIgnoreCase))
            {
                return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, "Failed to connect to the game server file transport host to write file content.")).ToApiResult();
            }

            return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CREDENTIALS_MISSING, "The game server does not have file transport credentials configured.")).ToBadRequestResult();
        }

        await using var session = sessionResult.Result.Data;
        using var operation = telemetryClient.StartOperation<DependencyTelemetry>("FilesPutContent");
        operation.Telemetry.Type = session.Transport.TelemetryType;
        operation.Telemetry.Target = session.Transport.TelemetryTarget;

        try
        {
            if (await session.DirectoryExists(normalizedPath, cancellationToken).ConfigureAwait(false))
            {
                return new ApiResult<FileMutationResultDto>(
                    HttpStatusCode.Conflict,
                    new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_OPERATION_FAILED, "The path points to a directory, not a file.")));
            }

            var parentPath = GetParentPath(normalizedPath) ?? "/";
            if (request.CreateParentDirectories)
            {
                await EnsureDirectoryExists(session, parentPath, cancellationToken).ConfigureAwait(false);
            }
            else if (parentPath != "/" && !await session.DirectoryExists(parentPath, cancellationToken).ConfigureAwait(false))
            {
                return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.CONFIG_FILE_NOT_FOUND, $"The parent directory '{parentPath}' was not found on the server.")).ToNotFoundResult();
            }

            var existed = await session.FileExists(normalizedPath, cancellationToken).ConfigureAwait(false);
            if (existed && !request.Overwrite)
            {
                return new ApiResult<FileMutationResultDto>(
                    HttpStatusCode.Conflict,
                    new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_OPERATION_FAILED, "The file already exists and overwrite is disabled.")));
            }

            byte[] payload;
            if (request.Mode == FileContentMode.Binary)
            {
                if (string.IsNullOrWhiteSpace(request.Base64Content))
                {
                    return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "Base64Content is required when mode is binary.")).ToBadRequestResult();
                }

                try
                {
                    payload = Convert.FromBase64String(request.Base64Content);
                }
                catch (FormatException)
                {
                    return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "Base64Content is not valid base64.")).ToBadRequestResult();
                }
            }
            else
            {
                var textContent = request.TextContent ?? string.Empty;
                Encoding textEncoding;
                try
                {
                    textEncoding = Encoding.GetEncoding(request.Encoding);
                }
                catch (Exception)
                {
                    return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.INVALID_REQUEST, $"The encoding '{request.Encoding}' is invalid or unsupported.")).ToBadRequestResult();
                }

                payload = textEncoding.GetBytes(textContent);
            }

            await session.UploadBytes(normalizedPath, payload, cancellationToken).ConfigureAwait(false);

            var outcome = existed ? FileMutationOutcome.Replaced : FileMutationOutcome.Created;
            var dto = new FileMutationResultDto(FileMutationOperation.Put, outcome, normalizedPath, bytesWritten: payload.LongLength);

            return new ApiResult<FileMutationResultDto>(
                existed ? HttpStatusCode.OK : HttpStatusCode.Created,
                new ApiResponse<FileMutationResultDto>(dto));
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);
            logger.LogError(ex, "Failed to put file content for game server {GameServerId} at path {Path}", gameServerId, normalizedPath);
            return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_OPERATION_FAILED, "Failed to write file content to the game server file transport host.")).ToApiResult();
        }
    }

    async Task<ApiResult<FileMutationResultDto>> IFilesApi.DeleteContent(Guid gameServerId, DeleteFileQueryDto query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.Path))
        {
            return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "Path is required.")).ToBadRequestResult();
        }

        var normalizedPath = NormalizePath(query.Path);
        if (!IsPathSafe(normalizedPath))
        {
            return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "The path contains invalid traversal segments.")).ToBadRequestResult();
        }

        var sessionResult = await fileTransportFactory.CreateSession(gameServerId, cancellationToken).ConfigureAwait(false);
        if (sessionResult.IsNotFound)
        {
            return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        if (!sessionResult.IsSuccess || sessionResult.Result?.Data == null)
        {
            var error = sessionResult.Result?.Errors?.FirstOrDefault();
            if (string.Equals(error?.Code, ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, StringComparison.OrdinalIgnoreCase))
            {
                return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, "Failed to connect to the game server file transport host to delete file content.")).ToApiResult();
            }

            return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CREDENTIALS_MISSING, "The game server does not have file transport credentials configured.")).ToBadRequestResult();
        }

        await using var session = sessionResult.Result.Data;
        using var operation = telemetryClient.StartOperation<DependencyTelemetry>("FilesDeleteContent");
        operation.Telemetry.Type = session.Transport.TelemetryType;
        operation.Telemetry.Target = session.Transport.TelemetryTarget;

        try
        {
            if (await session.DirectoryExists(normalizedPath, cancellationToken).ConfigureAwait(false))
            {
                return new ApiResult<FileMutationResultDto>(
                    HttpStatusCode.Conflict,
                    new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_OPERATION_FAILED, "The path points to a directory, not a file.")));
            }

            if (!await session.FileExists(normalizedPath, cancellationToken).ConfigureAwait(false))
            {
                return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.CONFIG_FILE_NOT_FOUND, $"The file '{normalizedPath}' was not found on the server.")).ToNotFoundResult();
            }

            await session.DeleteFile(normalizedPath, cancellationToken).ConfigureAwait(false);

            var dto = new FileMutationResultDto(FileMutationOperation.DeleteFile, FileMutationOutcome.Deleted, normalizedPath);
            return new ApiResponse<FileMutationResultDto>(dto).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);
            logger.LogError(ex, "Failed to delete file content for game server {GameServerId} at path {Path}", gameServerId, normalizedPath);
            return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_OPERATION_FAILED, "Failed to delete file content from the game server file transport host.")).ToApiResult();
        }
    }

    async Task<ApiResult<FileMutationResultDto>> IFilesApi.CreateDirectory(Guid gameServerId, CreateDirectoryRequestDto request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "Request body cannot be null.")).ToBadRequestResult();
        }

        if (string.IsNullOrWhiteSpace(request.Path))
        {
            return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "Path is required.")).ToBadRequestResult();
        }

        var normalizedPath = NormalizePath(request.Path);
        if (!IsPathSafe(normalizedPath))
        {
            return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "The path contains invalid traversal segments.")).ToBadRequestResult();
        }

        if (normalizedPath == "/")
        {
            if (request.IfNotExists)
            {
                var rootDto = new FileMutationResultDto(FileMutationOperation.CreateDirectory, FileMutationOutcome.AlreadyExists, normalizedPath);
                return new ApiResponse<FileMutationResultDto>(rootDto).ToApiResult();
            }

            return new ApiResult<FileMutationResultDto>(
                HttpStatusCode.Conflict,
                new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_OPERATION_FAILED, "The directory already exists.")));
        }

        var sessionResult = await fileTransportFactory.CreateSession(gameServerId, cancellationToken).ConfigureAwait(false);
        if (sessionResult.IsNotFound)
        {
            return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        if (!sessionResult.IsSuccess || sessionResult.Result?.Data == null)
        {
            var error = sessionResult.Result?.Errors?.FirstOrDefault();
            if (string.Equals(error?.Code, ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, StringComparison.OrdinalIgnoreCase))
            {
                return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, "Failed to connect to the game server file transport host to create directory.")).ToApiResult();
            }

            return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CREDENTIALS_MISSING, "The game server does not have file transport credentials configured.")).ToBadRequestResult();
        }

        await using var session = sessionResult.Result.Data;
        using var operation = telemetryClient.StartOperation<DependencyTelemetry>("FilesCreateDirectory");
        operation.Telemetry.Type = session.Transport.TelemetryType;
        operation.Telemetry.Target = session.Transport.TelemetryTarget;

        try
        {
            var parentPath = GetParentPath(normalizedPath) ?? "/";
            if (request.CreateParents)
            {
                await EnsureDirectoryExists(session, parentPath, cancellationToken).ConfigureAwait(false);
            }
            else if (parentPath != "/" && !await session.DirectoryExists(parentPath, cancellationToken).ConfigureAwait(false))
            {
                return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.CONFIG_FILE_NOT_FOUND, $"The parent directory '{parentPath}' was not found on the server.")).ToNotFoundResult();
            }

            var exists = await session.DirectoryExists(normalizedPath, cancellationToken).ConfigureAwait(false);
            if (exists && !request.IfNotExists)
            {
                return new ApiResult<FileMutationResultDto>(
                    HttpStatusCode.Conflict,
                    new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_OPERATION_FAILED, "The directory already exists.")));
            }

            if (exists)
            {
                var existingDto = new FileMutationResultDto(FileMutationOperation.CreateDirectory, FileMutationOutcome.AlreadyExists, normalizedPath);
                return new ApiResponse<FileMutationResultDto>(existingDto).ToApiResult();
            }

            await session.CreateDirectory(normalizedPath, cancellationToken).ConfigureAwait(false);
            var createdDto = new FileMutationResultDto(FileMutationOperation.CreateDirectory, FileMutationOutcome.Created, normalizedPath);

            return new ApiResult<FileMutationResultDto>(HttpStatusCode.Created, new ApiResponse<FileMutationResultDto>(createdDto));
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);
            logger.LogError(ex, "Failed to create directory for game server {GameServerId} at path {Path}", gameServerId, normalizedPath);
            return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_OPERATION_FAILED, "Failed to create directory on the game server file transport host.")).ToApiResult();
        }
    }

    async Task<ApiResult<FileMutationResultDto>> IFilesApi.DeleteDirectory(Guid gameServerId, DeleteDirectoryQueryDto query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.Path))
        {
            return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "Path is required.")).ToBadRequestResult();
        }

        var normalizedPath = NormalizePath(query.Path);
        if (!IsPathSafe(normalizedPath))
        {
            return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "The path contains invalid traversal segments.")).ToBadRequestResult();
        }

        if (normalizedPath == "/")
        {
            return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "Deleting the root directory is not allowed.")).ToBadRequestResult();
        }

        if (!query.Recursive)
        {
            return new ApiResult<FileMutationResultDto>(
                HttpStatusCode.Conflict,
                new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "Recursive must be true to delete directories.")));
        }

        var sessionResult = await fileTransportFactory.CreateSession(gameServerId, cancellationToken).ConfigureAwait(false);
        if (sessionResult.IsNotFound)
        {
            return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        if (!sessionResult.IsSuccess || sessionResult.Result?.Data == null)
        {
            var error = sessionResult.Result?.Errors?.FirstOrDefault();
            if (string.Equals(error?.Code, ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, StringComparison.OrdinalIgnoreCase))
            {
                return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, "Failed to connect to the game server file transport host to delete directory.")).ToApiResult();
            }

            return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CREDENTIALS_MISSING, "The game server does not have file transport credentials configured.")).ToBadRequestResult();
        }

        await using var session = sessionResult.Result.Data;
        using var operation = telemetryClient.StartOperation<DependencyTelemetry>("FilesDeleteDirectory");
        operation.Telemetry.Type = session.Transport.TelemetryType;
        operation.Telemetry.Target = session.Transport.TelemetryTarget;

        try
        {
            if (!await session.DirectoryExists(normalizedPath, cancellationToken).ConfigureAwait(false))
            {
                return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.CONFIG_FILE_NOT_FOUND, $"The directory '{normalizedPath}' was not found on the server.")).ToNotFoundResult();
            }

            await DeleteDirectoryRecursive(session, normalizedPath, cancellationToken).ConfigureAwait(false);

            var dto = new FileMutationResultDto(FileMutationOperation.DeleteDirectory, FileMutationOutcome.Deleted, normalizedPath);
            return new ApiResponse<FileMutationResultDto>(dto).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);
            logger.LogError(ex, "Failed to delete directory for game server {GameServerId} at path {Path}", gameServerId, normalizedPath);
            return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_OPERATION_FAILED, "Failed to delete directory from the game server file transport host.")).ToApiResult();
        }
    }

    Task<ApiResult<FileMutationResultDto>> IFilesApi.PatchEntry(Guid gameServerId, PatchFileEntryRequestDto request, CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return Task.FromResult(
                new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "Request body cannot be null.")).ToBadRequestResult());
        }

        if (string.IsNullOrWhiteSpace(request.SourcePath) || string.IsNullOrWhiteSpace(request.DestinationPath))
        {
            return Task.FromResult(
                new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "SourcePath and DestinationPath are required.")).ToBadRequestResult());
        }

        if (!Enum.IsDefined(request.Operation))
        {
            return Task.FromResult(
                new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.INVALID_REQUEST, $"The patch operation '{request.Operation}' is unsupported.")).ToBadRequestResult());
        }

        var sourcePath = NormalizePath(request.SourcePath);
        var destinationPath = NormalizePath(request.DestinationPath);

        if (!IsPathSafe(sourcePath) || !IsPathSafe(destinationPath))
        {
            return Task.FromResult(
                new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "The path contains invalid traversal segments.")).ToBadRequestResult());
        }

        if (sourcePath == "/" || destinationPath == "/")
        {
            return Task.FromResult(
                new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "Root directory operations are not supported for patch entry."))
                    .ToBadRequestResult());
        }

        if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
        {
            var noOpDto = new FileMutationResultDto(
                MapPatchOperation(request.Operation),
                FileMutationOutcome.NoOp,
                destinationPath,
                sourcePath,
                destinationPath);

            return Task.FromResult(new ApiResponse<FileMutationResultDto>(noOpDto).ToApiResult());
        }

        return PatchEntryCore(gameServerId, request, sourcePath, destinationPath, cancellationToken);
    }

    private async Task<ApiResult<FileMutationResultDto>> PatchEntryCore(
        Guid gameServerId,
        PatchFileEntryRequestDto request,
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        var sessionResult = await fileTransportFactory.CreateSession(gameServerId, cancellationToken).ConfigureAwait(false);
        if (sessionResult.IsNotFound)
        {
            return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        if (!sessionResult.IsSuccess || sessionResult.Result?.Data == null)
        {
            var error = sessionResult.Result?.Errors?.FirstOrDefault();
            if (string.Equals(error?.Code, ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, StringComparison.OrdinalIgnoreCase))
            {
                return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, "Failed to connect to the game server file transport host to patch entry.")).ToApiResult();
            }

            return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CREDENTIALS_MISSING, "The game server does not have file transport credentials configured.")).ToBadRequestResult();
        }

        await using var session = sessionResult.Result.Data;
        using var operation = telemetryClient.StartOperation<DependencyTelemetry>("FilesPatchEntry");
        operation.Telemetry.Type = session.Transport.TelemetryType;
        operation.Telemetry.Target = session.Transport.TelemetryTarget;

        try
        {
            var sourceIsDirectory = await session.DirectoryExists(sourcePath, cancellationToken).ConfigureAwait(false);
            var sourceIsFile = !sourceIsDirectory && await session.FileExists(sourcePath, cancellationToken).ConfigureAwait(false);
            if (!sourceIsDirectory && !sourceIsFile)
            {
                return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.CONFIG_FILE_NOT_FOUND, $"The path '{sourcePath}' was not found on the server.")).ToNotFoundResult();
            }

            if (sourceIsDirectory && destinationPath.StartsWith(sourcePath + "/", StringComparison.OrdinalIgnoreCase))
            {
                return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "DestinationPath cannot be inside SourcePath for directory patch operations.")).ToBadRequestResult();
            }

            if (sourceIsDirectory && !request.Recursive)
            {
                return new ApiResult<FileMutationResultDto>(
                    HttpStatusCode.Conflict,
                    new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "Recursive must be true when patching directory entries.")));
            }

            var destinationIsDirectory = await session.DirectoryExists(destinationPath, cancellationToken).ConfigureAwait(false);
            var destinationIsFile = !destinationIsDirectory && await session.FileExists(destinationPath, cancellationToken).ConfigureAwait(false);

            if (sourceIsFile && destinationIsDirectory)
            {
                return new ApiResult<FileMutationResultDto>(
                    HttpStatusCode.Conflict,
                    new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_OPERATION_FAILED, "DestinationPath points to a directory, not a file.")));
            }

            if (sourceIsDirectory && destinationIsFile)
            {
                return new ApiResult<FileMutationResultDto>(
                    HttpStatusCode.Conflict,
                    new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_OPERATION_FAILED, "DestinationPath points to a file, not a directory.")));
            }

            if ((destinationIsDirectory || destinationIsFile) && !request.Overwrite)
            {
                return new ApiResult<FileMutationResultDto>(
                    HttpStatusCode.Conflict,
                    new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_OPERATION_FAILED, "The destination entry already exists and overwrite is disabled.")));
            }

            var destinationParent = GetParentPath(destinationPath) ?? "/";
            if (request.CreateDestinationDirectories)
            {
                await EnsureDirectoryExists(session, destinationParent, cancellationToken).ConfigureAwait(false);
            }
            else if (destinationParent != "/" && !await session.DirectoryExists(destinationParent, cancellationToken).ConfigureAwait(false))
            {
                return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.CONFIG_FILE_NOT_FOUND, $"The destination parent directory '{destinationParent}' was not found on the server.")).ToNotFoundResult();
            }

            long? bytesWritten = null;
            if (sourceIsDirectory)
            {
                await CopyDirectoryRecursive(session, sourcePath, destinationPath, request.Overwrite, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var bytes = await session.DownloadBytes(sourcePath, cancellationToken).ConfigureAwait(false);
                bytesWritten = bytes.LongLength;
                await session.UploadBytes(destinationPath, bytes, cancellationToken).ConfigureAwait(false);
            }

            if (request.Operation != FileEntryPatchOperation.Copy)
            {
                if (sourceIsDirectory)
                {
                    await DeleteDirectoryRecursive(session, sourcePath, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await session.DeleteFile(sourcePath, cancellationToken).ConfigureAwait(false);
                }
            }

            var responseDto = new FileMutationResultDto(
                MapPatchOperation(request.Operation),
                MapPatchOutcome(request.Operation),
                destinationPath,
                sourcePath,
                destinationPath,
                bytesWritten);

            return new ApiResponse<FileMutationResultDto>(responseDto).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);
            logger.LogError(ex, "Failed to patch file entry for game server {GameServerId} from {SourcePath} to {DestinationPath}", gameServerId, sourcePath, destinationPath);
            return new ApiResponse<FileMutationResultDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_OPERATION_FAILED, "Failed to patch entry on the game server file transport host.")).ToApiResult();
        }
    }

    private static FileMutationOperation MapPatchOperation(FileEntryPatchOperation operation)
    {
        return operation switch
        {
            FileEntryPatchOperation.Rename => FileMutationOperation.Rename,
            FileEntryPatchOperation.Move => FileMutationOperation.Move,
            FileEntryPatchOperation.Copy => FileMutationOperation.Copy,
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unsupported patch operation."),
        };
    }

    private static FileMutationOutcome MapPatchOutcome(FileEntryPatchOperation operation)
    {
        return operation switch
        {
            FileEntryPatchOperation.Rename => FileMutationOutcome.Renamed,
            FileEntryPatchOperation.Move => FileMutationOutcome.Moved,
            FileEntryPatchOperation.Copy => FileMutationOutcome.Copied,
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unsupported patch operation."),
        };
    }

    private static async Task<IReadOnlyList<FileTransportEntry>> GetRecursiveListing(IGameServerFileTransportSession session, string rootPath, CancellationToken cancellationToken)
    {
        var results = new List<FileTransportEntry>();
        var queue = new Queue<string>();
        queue.Enqueue(rootPath);

        while (queue.Count > 0)
        {
            var currentPath = queue.Dequeue();
            var entries = await session.GetListing(currentPath, cancellationToken).ConfigureAwait(false);
            foreach (var entry in entries)
            {
                results.Add(entry);
                if (entry.IsDirectory)
                {
                    queue.Enqueue(NormalizePath(entry.FullPath));
                }
            }
        }

        return results;
    }

    private static async Task EnsureDirectoryExists(IGameServerFileTransportSession session, string path, CancellationToken cancellationToken)
    {
        var normalizedPath = NormalizePath(path);
        if (normalizedPath == "/")
        {
            return;
        }

        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var currentPath = string.Empty;
        foreach (var segment in segments)
        {
            currentPath = string.Concat(currentPath, "/", segment);
            if (!await session.DirectoryExists(currentPath, cancellationToken).ConfigureAwait(false))
            {
                await session.CreateDirectory(currentPath, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task DeleteDirectoryRecursive(IGameServerFileTransportSession session, string path, CancellationToken cancellationToken)
    {
        var entries = await session.GetListing(path, cancellationToken).ConfigureAwait(false);
        foreach (var entry in entries)
        {
            var entryPath = NormalizePath(entry.FullPath);
            if (entry.IsDirectory)
            {
                await DeleteDirectoryRecursive(session, entryPath, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await session.DeleteFile(entryPath, cancellationToken).ConfigureAwait(false);
            }
        }

        await session.DeleteDirectory(path, cancellationToken).ConfigureAwait(false);
    }

    private static async Task CopyDirectoryRecursive(IGameServerFileTransportSession session, string sourcePath, string destinationPath, bool overwrite, CancellationToken cancellationToken)
    {
        if (!await session.DirectoryExists(destinationPath, cancellationToken).ConfigureAwait(false))
        {
            await session.CreateDirectory(destinationPath, cancellationToken).ConfigureAwait(false);
        }

        var entries = await session.GetListing(sourcePath, cancellationToken).ConfigureAwait(false);
        foreach (var entry in entries)
        {
            var sourceChildPath = NormalizePath(entry.FullPath);
            var destinationChildPath = CombinePath(destinationPath, entry.Name);

            if (entry.IsDirectory)
            {
                await CopyDirectoryRecursive(session, sourceChildPath, destinationChildPath, overwrite, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                if (!overwrite && await session.FileExists(destinationChildPath, cancellationToken).ConfigureAwait(false))
                {
                    throw new InvalidOperationException($"The destination file '{destinationChildPath}' already exists and overwrite is disabled.");
                }

                var bytes = await session.DownloadBytes(sourceChildPath, cancellationToken).ConfigureAwait(false);
                await session.UploadBytes(destinationChildPath, bytes, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static string CombinePath(string basePath, string name)
    {
        var normalizedBase = NormalizePath(basePath);
        var trimmedName = name.Trim('/');
        return normalizedBase == "/" ? string.Concat("/", trimmedName) : string.Concat(normalizedBase, "/", trimmedName);
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        var normalized = path.Replace('\\', '/').Trim();
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        if (normalized.Length > 1 && normalized.EndsWith('/'))
        {
            normalized = normalized.TrimEnd('/');
        }

        return normalized;
    }

    private static string? GetParentPath(string path)
    {
        if (path == "/")
        {
            return null;
        }

        var lastSlash = path.LastIndexOf('/');
        if (lastSlash <= 0)
        {
            return "/";
        }

        return path[..lastSlash];
    }

    private static bool IsPathSafe(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(segment => segment == ".."))
        {
            return false;
        }

        if (segments.Length > 0 && (segments[0].Contains(':') || path.StartsWith("//", StringComparison.Ordinal)))
        {
            return false;
        }

        return true;
    }
}
