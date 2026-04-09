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
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1;

[ApiController]
[Authorize(Roles = "ServiceAccount")]
[ApiVersion(ApiVersions.V1)]
[Route("v{version:apiVersion}")]
public class FtpBrowseController(
    ILogger<FtpBrowseController> logger,
    IRepositoryApiClient repositoryApiClient,
    TelemetryClient telemetryClient,
    IConfiguration configuration,
    IMemoryCache memoryCache) : Controller, IFtpBrowseApi
{

    [HttpGet]
    [Route("ftp/{gameServerId}/browse")]
    public async Task<IActionResult> BrowseDirectory(Guid gameServerId, [FromQuery] string? path = null)
    {
        var response = await ((IFtpBrowseApi)this).BrowseDirectory(gameServerId, path);

        return response.ToHttpResult();
    }

    async Task<ApiResult<FtpDirectoryListingDto>> IFtpBrowseApi.BrowseDirectory(Guid gameServerId, string? path, CancellationToken cancellationToken)
    {
        var normalizedPath = NormalizePath(path);

        if (ContainsTraversalSegments(normalizedPath))
            return new ApiResponse<FtpDirectoryListingDto>(new ApiError(ErrorCodes.INVALID_REQUEST, "The path contains invalid traversal segments.")).ToBadRequestResult();

        var gameServerApiResponse = await repositoryApiClient.GameServers.V1.GetGameServer(gameServerId);

        if (gameServerApiResponse.IsNotFound || gameServerApiResponse.Result?.Data == null)
            return new ApiResponse<FtpDirectoryListingDto>(new ApiError(ErrorCodes.GAME_SERVER_NOT_FOUND, $"The game server with ID '{gameServerId}' does not exist.")).ToNotFoundResult();

        var serverData = gameServerApiResponse.Result.Data;

        if (string.IsNullOrWhiteSpace(serverData.FtpHostname) || string.IsNullOrWhiteSpace(serverData.FtpUsername))
            return new ApiResponse<FtpDirectoryListingDto>(new ApiError(ErrorCodes.FTP_CREDENTIALS_MISSING, "The game server does not have FTP credentials configured.")).ToBadRequestResult();

        var cacheKey = $"{gameServerId}-ftp-browse-{normalizedPath}";
        if (memoryCache.TryGetValue<FtpDirectoryListingDto>(cacheKey, out var cached) && cached != null)
            return new ApiResponse<FtpDirectoryListingDto>(cached).ToApiResult();

        var operation = telemetryClient.StartOperation<DependencyTelemetry>("FtpBrowse");
        operation.Telemetry.Type = "FTP";
        operation.Telemetry.Target = $"{serverData.FtpHostname}:{serverData.FtpPort}";

        try
        {
            await using var ftpClient = new AsyncFtpClient(serverData.FtpHostname, serverData.FtpUsername, serverData.FtpPassword, serverData.FtpPort ?? 21);
            ftpClient.ValidateCertificate += (control, e) =>
            {
                if (e.Certificate.GetCertHashString().Equals(configuration["xtremeidiots_ftp_certificate_thumbprint"]))
                { // Account for self-signed FTP certificate for self-hosted servers
                    e.Accept = true;
                }
            };

            await ftpClient.AutoConnect();

            var files = await ftpClient.GetListing(normalizedPath);

            var items = files
                .Where(f => f.Name != "." && f.Name != "..")
                .Where(f => f.Type == FtpObjectType.File || f.Type == FtpObjectType.Directory)
                .Select(f => new FtpItemDto(
                    f.Name,
                    f.FullName,
                    f.Type == FtpObjectType.Directory ? FtpItemType.Directory : FtpItemType.File,
                    f.Type == FtpObjectType.File ? f.Size : null,
                    f.Modified != DateTime.MinValue ? f.Modified : null))
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

            logger.LogError(ex, "Failed to browse FTP directory for game server {GameServerId} at path {Path}", gameServerId, normalizedPath);
            return new ApiResponse<FtpDirectoryListingDto>(new ApiError(ErrorCodes.FTP_CONNECTION_FAILED, "Failed to connect to the game server's FTP host to browse directory.")).ToApiResult();
        }
        finally
        {
            telemetryClient.StopOperation(operation);
        }
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "/";

        // Normalize separators and ensure leading slash
        var normalized = path.Replace('\\', '/').Trim();
        if (!normalized.StartsWith('/'))
            normalized = "/" + normalized;

        // Remove trailing slash unless root
        if (normalized.Length > 1 && normalized.EndsWith('/'))
            normalized = normalized.TrimEnd('/');

        return normalized;
    }

    private static bool ContainsTraversalSegments(string path)
    {
        // Reject path traversal
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(s => s == ".."))
            return true;

        // Reject Windows absolute paths (e.g. C:, D:) and UNC paths (e.g. //server)
        if (segments.Length > 0 && (segments[0].Contains(':') || path.StartsWith("//")))
            return true;

        return false;
    }

    private static string? GetParentPath(string path)
    {
        if (path == "/")
            return null;

        var lastSlash = path.LastIndexOf('/');
        if (lastSlash <= 0)
            return "/";

        return path[..lastSlash];
    }
}
