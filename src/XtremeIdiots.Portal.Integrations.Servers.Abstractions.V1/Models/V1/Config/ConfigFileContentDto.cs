namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Config;

public record ConfigFileContentDto
{
    public ConfigFileContentDto(string filePath, string content)
    {
        FilePath = filePath;
        Content = content;
    }

    public string FilePath { get; set; }
    public string Content { get; set; }
}
