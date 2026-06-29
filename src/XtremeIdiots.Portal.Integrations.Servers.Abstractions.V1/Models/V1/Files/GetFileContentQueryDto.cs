namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Files;

public record GetFileContentQueryDto
{
    public string? Path { get; init; }

    public FileContentMode Mode { get; init; } = FileContentMode.Text;

    public string Encoding { get; init; } = "utf-8";

    public long? RangeStart { get; init; }

    public long? RangeEnd { get; init; }
}
