namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record CoD4xAdminAddAdminRequestDto
{
    public string? User { get; init; }

    public int Power { get; init; }
}
