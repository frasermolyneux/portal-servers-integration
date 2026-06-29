namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record CoD4xTargetMessageRequestDto
{
    public string? Target { get; init; }

    public string? Message { get; init; }
}
