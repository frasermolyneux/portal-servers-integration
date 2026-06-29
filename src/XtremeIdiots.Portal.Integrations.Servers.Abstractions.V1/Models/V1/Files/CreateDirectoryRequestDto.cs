namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Files;

public record CreateDirectoryRequestDto
{
    public string? Path { get; init; }

    public bool CreateParents { get; init; }

    public bool IfNotExists { get; init; } = true;
}
