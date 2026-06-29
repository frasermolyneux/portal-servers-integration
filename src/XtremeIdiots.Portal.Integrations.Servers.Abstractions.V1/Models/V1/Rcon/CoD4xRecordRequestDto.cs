namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record CoD4xRecordRequestDto
{
    public string? Target { get; init; }

    public string? DemoName { get; init; }
}
