namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record CoD4xUnbanRequestDto
{
    public required string PlayerIdentifier { get; init; }
}
