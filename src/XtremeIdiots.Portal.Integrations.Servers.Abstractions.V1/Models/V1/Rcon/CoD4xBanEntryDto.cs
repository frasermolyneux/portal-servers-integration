namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record CoD4xBanEntryDto
{
    public int Index { get; set; }
    public string PlayerIdentifier { get; set; } = string.Empty;
    public string Nick { get; set; } = string.Empty;
    public string AdminSteamId { get; set; } = string.Empty;
    public string Expire { get; set; } = string.Empty;
    public bool IsPermanent { get; set; }
    public string Reason { get; set; } = string.Empty;
}
