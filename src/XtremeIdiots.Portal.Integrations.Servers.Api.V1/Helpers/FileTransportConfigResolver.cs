using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Settings.Contracts.V1.Contracts.FileTransport;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;

internal static class FileTransportConfigResolver
{
    public static FileTransportCredentials? Parse(FileTransportType transportType, string? configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
            return null;

        try
        {
            return transportType switch
            {
                FileTransportType.Sftp => ParseSftp(configJson),
                FileTransportType.Ftp => ParseFtp(configJson),
                _ => null,
            };
        }
        catch
        {
            return null;
        }
    }

    private static FileTransportCredentials? ParseFtp(string configJson)
    {
        var document = SettingsContractsJsonSerializer.Deserialize<FtpSettingsDocument>(configJson);
        var validationResult = new FtpSettingsValidator().Validate(document);
        if (document is null || validationResult.Errors.Count > 0)
            return null;

        if (string.IsNullOrWhiteSpace(document.Hostname) || string.IsNullOrWhiteSpace(document.Username))
            return null;

        return new FileTransportCredentials(
            document.Hostname,
            document.Port ?? 21,
            document.Username,
            document.Password ?? string.Empty,
            null,
            document.MapsRootPath);
    }

    private static FileTransportCredentials? ParseSftp(string configJson)
    {
        var document = SettingsContractsJsonSerializer.Deserialize<SftpSettingsDocument>(configJson);
        var validationResult = new SftpSettingsValidator().Validate(document);
        if (document is null || validationResult.Errors.Count > 0)
            return null;

        if (string.IsNullOrWhiteSpace(document.Hostname) || string.IsNullOrWhiteSpace(document.Username))
            return null;

        return new FileTransportCredentials(
            document.Hostname,
            document.Port ?? 22,
            document.Username,
            document.Password ?? string.Empty,
            document.HostKeyFingerprint,
            document.MapsRootPath);
    }
}
