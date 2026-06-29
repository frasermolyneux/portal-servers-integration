namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Files;

public record FileContentDto
{
    public FileContentDto(string fullPath, FileContentMode mode, string encoding, long contentLengthBytes, string? textContent, string? base64Content)
    {
        FullPath = fullPath;
        Mode = mode;
        Encoding = encoding;
        ContentLengthBytes = contentLengthBytes;
        TextContent = textContent;
        Base64Content = base64Content;
    }

    public string FullPath { get; init; }

    public FileContentMode Mode { get; init; }

    public string Encoding { get; init; }

    public long ContentLengthBytes { get; init; }

    public string? TextContent { get; init; }

    public string? Base64Content { get; init; }
}
