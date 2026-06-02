namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record ResolvePlayerSuggestionDto
{
    public required string Name { get; init; }
    public int Slot { get; init; }
    public string? Guid { get; init; }
    public double Score { get; init; }
}
