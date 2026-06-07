using System.Text.Json;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;

internal static class SettingsContractsJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static TDocument? Deserialize<TDocument>(string configJson)
    {
        return JsonSerializer.Deserialize<TDocument>(configJson, Options);
    }
}