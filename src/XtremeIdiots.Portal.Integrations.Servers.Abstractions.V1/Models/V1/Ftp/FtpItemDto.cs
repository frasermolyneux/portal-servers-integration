using System.Text.Json.Serialization;

namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Ftp;

public record FtpItemDto
{
    public FtpItemDto(string name, string fullPath, FtpItemType type, long? size, DateTime? modified)
    {
        Name = name;
        FullPath = fullPath;
        Type = type;
        Size = size;
        Modified = modified;
    }

    [JsonInclude]
    public string Name { get; internal set; }

    [JsonInclude]
    public string FullPath { get; internal set; }

    [JsonInclude]
    public FtpItemType Type { get; internal set; }

    [JsonInclude]
    public long? Size { get; internal set; }

    [JsonInclude]
    public DateTime? Modified { get; internal set; }
}
