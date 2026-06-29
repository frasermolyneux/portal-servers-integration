namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Files;

public record FileEntryDto
{
    public FileEntryDto(string name, string fullPath, FileEntryType type, long? size, DateTime? modified)
    {
        Name = name;
        FullPath = fullPath;
        Type = type;
        Size = size;
        Modified = modified;
    }

    public string Name { get; init; }

    public string FullPath { get; init; }

    public FileEntryType Type { get; init; }

    public long? Size { get; init; }

    public DateTime? Modified { get; init; }
}
