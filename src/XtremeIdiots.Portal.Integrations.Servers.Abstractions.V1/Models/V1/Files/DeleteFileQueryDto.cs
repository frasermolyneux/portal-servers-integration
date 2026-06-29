namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Files;

public record DeleteFileQueryDto
{
    public string? Path { get; init; }
}
