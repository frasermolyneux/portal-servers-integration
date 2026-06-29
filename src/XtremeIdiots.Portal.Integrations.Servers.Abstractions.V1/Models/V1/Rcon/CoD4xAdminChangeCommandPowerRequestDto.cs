namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record CoD4xAdminChangeCommandPowerRequestDto
{
    public string? Command { get; init; }

    public int MinPower { get; init; }
}
