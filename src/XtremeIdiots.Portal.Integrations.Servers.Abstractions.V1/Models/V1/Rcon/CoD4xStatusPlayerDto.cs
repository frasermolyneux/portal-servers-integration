namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record CoD4xStatusPlayerDto
{
    public int Num { get; set; }
    public int Score { get; set; }
    public string PingRaw { get; set; } = string.Empty;
    public int? Ping { get; set; }
    public string PlayerIdentifier { get; set; } = string.Empty;
    public string SteamId { get; set; } = string.Empty;
    public string RawName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int LastMessageSeconds { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public int Rate { get; set; }
}
