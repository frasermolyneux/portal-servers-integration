namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Files;

public record PutFileContentRequestDto
{
    public string? Path { get; init; }

    public FileContentMode Mode { get; init; } = FileContentMode.Text;

    public string? TextContent { get; init; }

    public string? Base64Content { get; init; }

    public string Encoding { get; init; } = "utf-8";

    public bool Overwrite { get; init; } = true;

    public bool CreateParentDirectories { get; init; }
}
