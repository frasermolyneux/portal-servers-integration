using System.Text.Json;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;

internal static class FileTransportConfigResolver
{
    public static FileTransportCredentials? Parse(FileTransportType transportType, string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(configJson);
            var root = doc.RootElement;

            var hostname = root.TryGetProperty("hostname", out var hostProperty) ? hostProperty.GetString() : null;
            var username = root.TryGetProperty("username", out var usernameProperty) ? usernameProperty.GetString() : null;
            var password = root.TryGetProperty("password", out var passwordProperty) ? passwordProperty.GetString() : null;
            var hostKeyFingerprint = transportType == FileTransportType.Sftp && root.TryGetProperty("hostKeyFingerprint", out var hostKeyFingerprintProperty)
                ? hostKeyFingerprintProperty.GetString()
                : null;
            var mapsRootPath = root.TryGetProperty("mapsRootPath", out var mapsRootPathProperty) ? mapsRootPathProperty.GetString() : null;

            var defaultPort = transportType == FileTransportType.Sftp ? 22 : 21;
            var port = root.TryGetProperty("port", out var portProperty) && portProperty.TryGetInt32(out var parsedPort)
                ? parsedPort
                : defaultPort;

            if (string.IsNullOrWhiteSpace(hostname) || string.IsNullOrWhiteSpace(username))
                return null;

            return new FileTransportCredentials(hostname, port, username, password ?? string.Empty, hostKeyFingerprint, mapsRootPath);
        }
        catch
        {
            return null;
        }
    }
}
