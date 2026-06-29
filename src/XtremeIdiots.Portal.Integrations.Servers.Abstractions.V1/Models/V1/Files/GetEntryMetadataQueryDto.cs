namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Files;

public record GetEntryMetadataQueryDto
{
    public string? Path { get; init; }
}
