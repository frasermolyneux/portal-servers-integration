namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;

internal static class RconConfigResolver
{
    public static string? ParsePasswordFromConfig(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson)) return null;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(configJson);
            var root = doc.RootElement;

            return root.TryGetProperty("password", out var p) ? p.GetString() : null;
        }
        catch
        {
            return null;
        }
    }
}
