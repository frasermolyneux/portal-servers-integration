using Asp.Versioning;
using FluentFTP;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MX.Api.Abstractions;
using MX.Api.Web.Extensions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Ftp;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Constants;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1;

[ApiController]
[Authorize(Roles = "ServiceAccount")]
[ApiVersion(ApiVersions.V1)]
[Route("v{version:apiVersion}")]
public class FileBrowseController(
    ILogger<FileBrowseController> logger,
    IGameServerFileTransportFactory fileTransportFactory,
    TelemetryClient telemetryClient,
    IMemoryCache memoryCache) : Controller, IFileBrowseApi
{

    [HttpGet]
    [Route("file-browse/{gameServerId}/browse")]
    public async Task<IActionResult> BrowseDirectory(Guid gameServerId, [FromQuery] string? path = null)
    {
        var response = await ((IFileBrowseApi)this).BrowseDirectory(gameServerId, path);

        return response.ToHttpResult();
    }

    async Task<ApiResult<FtpDirectoryListingDto>> IFileBrowseApi.BrowseDirectory(Guid gameServerId, string? path, CancellationToken cancellationToken)
    {
        var normalizedPath = NormalizePath(path);

        if (ContainsTraversalSegments(normalizedPath))
        {
            return new ApiResponse<FtpDirectoryListingDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "The path contains invalid traversal segments.")).ToBadRequestResult();
        }

        var sessionResult = await fileTransportFactory.CreateSession(gameServerId, cancellationToken).ConfigureAwait(false);
        if (sessionResult.IsNotFound)
        {
            return new ApiResponse<FtpDirectoryListingDto>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();
        }

        if (!sessionResult.IsSuccess || sessionResult.Result?.Data == null)
        {
            var error = sessionResult.Result?.Errors?.FirstOrDefault();
            if (string.Equals(error?.Code, ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, StringComparison.OrdinalIgnoreCase))
            {
                return new ApiResponse<FtpDirectoryListingDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, "Failed to connect to the game server file transport host to browse directory.")).ToApiResult();
            }

            return new ApiResponse<FtpDirectoryListingDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CREDENTIALS_MISSING, "The game server does not have file transport credentials configured.")).ToBadRequestResult();
        }

        await using var session = sessionResult.Result.Data;

        var cacheKey = $"{gameServerId}-file-browse-{session.Transport.TransportType}-{normalizedPath}";
        if (memoryCache.TryGetValue<FtpDirectoryListingDto>(cacheKey, out var cached) && cached != null)
        {
            return new ApiResponse<FtpDirectoryListingDto>(cached).ToApiResult();
        }

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("FileBrowse");
        operation.Telemetry.Type = session.Transport.TelemetryType;
        operation.Telemetry.Target = session.Transport.TelemetryTarget;

        try
        {
            var files = await session.GetListing(normalizedPath, cancellationToken).ConfigureAwait(false);

            var items = files
                .Select(f => new FtpItemDto(
                    f.Name,
                    f.FullPath,
                    f.IsDirectory ? FtpItemType.Directory : FtpItemType.File,
                    f.Size,
                    f.Modified))
                .OrderBy(f => f.Type == FtpItemType.Directory ? 0 : 1)
                .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var parentPath = GetParentPath(normalizedPath);
            var data = new FtpDirectoryListingDto(normalizedPath, parentPath, items);

            memoryCache.Set(cacheKey, data, TimeSpan.FromSeconds(30));

            return new ApiResponse<FtpDirectoryListingDto>(data).ToApiResult();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            operation.Telemetry.ResultCode = ex.Message;
            telemetryClient.TrackException(ex);

            logger.LogError(ex, "Failed to browse file transport directory for game server {GameServerId} at path {Path}", gameServerId, normalizedPath);
            return new ApiResponse<FtpDirectoryListingDto>(new ApiError(ErrorCodes.FILE_TRANSPORT_CONNECTION_FAILED, "Failed to connect to the game server file transport host to browse directory.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        // Normalize separators and ensure leading slash
        var normalized = path.Replace('\\', '/').Trim();
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        // Remove trailing slash unless root
        if (normalized.Length > 1 && normalized.EndsWith('/'))
        {
            normalized = normalized.TrimEnd('/');
        }

        return normalized;
    }

    private static bool ContainsTraversalSegments(string path)
    {
        // Reject path traversal
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(s => s == ".."))
        {
            return true;
        }

        // Reject Windows absolute paths (e.g. C:, D:) and UNC paths (e.g. //server)
        if (segments.Length > 0 && (segments[0].Contains(':') || path.StartsWith("//")))
        {
            return true;
        }

        return false;
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
}