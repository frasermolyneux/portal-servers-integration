namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record CoD4xTargetReasonRequestDto
{
    public string? Target { get; init; }

    public string? Reason { get; init; }
}
