namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record ResolvePlayerRequestDto
{
    public string? PlayerQuery { get; init; }
    public int? MaxSuggestions { get; init; }
}
