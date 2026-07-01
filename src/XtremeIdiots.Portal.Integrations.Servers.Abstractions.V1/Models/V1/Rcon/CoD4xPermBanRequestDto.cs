namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record CoD4xPermBanRequestDto
{
    public required string PlayerIdentifier { get; init; }
    public string? Reason { get; init; }
}
