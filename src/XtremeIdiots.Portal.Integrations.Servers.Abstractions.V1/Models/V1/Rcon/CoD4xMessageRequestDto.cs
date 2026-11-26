namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record CoD4xMessageRequestDto
{
    public string? Message { get; init; }

    public IReadOnlyCollection<string>? Messages { get; init; }

    public string? FirstFragmentHeader { get; init; }
}
