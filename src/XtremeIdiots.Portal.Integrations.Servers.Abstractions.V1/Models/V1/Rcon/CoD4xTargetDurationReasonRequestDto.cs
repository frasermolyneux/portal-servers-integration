namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record CoD4xTargetDurationReasonRequestDto
{
    public string? Target { get; init; }

    public int DurationMinutes { get; init; }

    public string? Reason { get; init; }
}
