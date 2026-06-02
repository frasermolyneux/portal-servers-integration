namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record ResolvePlayerResponseDto
{
    public ResolvePlayerStatus Status { get; init; }
    public ResolvePlayerSuggestionDto? ResolvedPlayer { get; init; }
    public IList<ResolvePlayerSuggestionDto> Suggestions { get; init; } = [];
}
