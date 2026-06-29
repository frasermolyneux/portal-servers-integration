namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Files;

public record DeleteDirectoryQueryDto
{
    public string? Path { get; init; }

    public bool Recursive { get; init; }
}
