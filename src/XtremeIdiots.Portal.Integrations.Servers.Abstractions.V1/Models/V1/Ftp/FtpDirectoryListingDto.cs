using System.Text.Json.Serialization;

namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Ftp;

public record FtpDirectoryListingDto
{
    public FtpDirectoryListingDto(string currentPath, string? parentPath, List<FtpItemDto> items)
    {
        CurrentPath = currentPath;
        ParentPath = parentPath;
        Items = items;
    }

    [JsonInclude]
    public string CurrentPath { get; internal set; }

    [JsonInclude]
    public string? ParentPath { get; internal set; }

    [JsonInclude]
    public List<FtpItemDto> Items { get; internal set; }
}
