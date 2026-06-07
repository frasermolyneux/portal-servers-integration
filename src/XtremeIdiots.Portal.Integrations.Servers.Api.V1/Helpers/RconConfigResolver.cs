using XtremeIdiots.Portal.Settings.Contracts.V1.Contracts.Rcon;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;

internal static class RconConfigResolver
{
    public static string? ParsePasswordFromConfig(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson)) return null;

        try
        {
            var document = SettingsContractsJsonSerializer.Deserialize<RconSettingsDocument>(configJson);
            var validationResult = new RconSettingsValidator().Validate(document);
            if (document is null || validationResult.Errors.Count > 0)
                return null;

            return document.Password;
        }
        catch
        {
            return null;
        }
    }
}
