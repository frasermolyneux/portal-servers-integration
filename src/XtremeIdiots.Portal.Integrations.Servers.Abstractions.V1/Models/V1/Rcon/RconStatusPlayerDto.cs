namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record RconStatusPlayerDto
{
    public int Num { get; init; }

    public string Guid { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string IpAddress { get; init; } = string.Empty;

    public int Rate { get; init; }

    public int Ping { get; init; }
}
