namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;

internal static class FtpConfigResolver
{
    public record FtpCredentials(string Hostname, int Port, string Username, string Password);

    public static FtpCredentials? ParseFromConfig(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson)) return null;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(configJson);
            var root = doc.RootElement;

            var hostname = root.TryGetProperty("hostname", out var h) ? h.GetString() : null;
            var username = root.TryGetProperty("username", out var u) ? u.GetString() : null;
            var password = root.TryGetProperty("password", out var p) ? p.GetString() : null;
            var port = root.TryGetProperty("port", out var pt) && pt.TryGetInt32(out var portVal) ? portVal : 21;

            if (string.IsNullOrWhiteSpace(hostname) || string.IsNullOrWhiteSpace(username))
                return null;

            return new FtpCredentials(hostname, port, username, password ?? "");
        }
        catch
        {
            return null;
        }
    }
}
