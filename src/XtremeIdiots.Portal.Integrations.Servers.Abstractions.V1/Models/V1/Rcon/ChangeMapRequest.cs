namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record ChangeMapRequest
{
    public required string MapName { get; init; }
}
