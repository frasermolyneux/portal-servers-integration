using XtremeIdiots.Portal.Settings.Contracts.V1.Contracts.FileTransport;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;

internal static class FtpConfigResolver
{
    public record FtpCredentials(string Hostname, int Port, string Username, string Password);

    public static FtpCredentials? ParseFromConfig(string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson)) return null;

        try
        {
            var document = SettingsContractsJsonSerializer.Deserialize<FtpSettingsDocument>(configJson);
            var validationResult = new FtpSettingsValidator().Validate(document);
            if (document is null || validationResult.Errors.Count > 0)
                return null;

            if (string.IsNullOrWhiteSpace(document.Hostname) || string.IsNullOrWhiteSpace(document.Username))
                return null;

            return new FtpCredentials(document.Hostname, document.Port ?? 21, document.Username, document.Password ?? string.Empty);
        }
        catch
        {
            return null;
        }
    }
}
