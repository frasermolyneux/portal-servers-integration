namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Files;

public record FileEntriesCollectionDto
{
    public FileEntriesCollectionDto(string currentPath, string? parentPath, List<FileEntryDto> items, string? continuationToken = null)
    {
        CurrentPath = currentPath;
        ParentPath = parentPath;
        Items = items;
        ContinuationToken = continuationToken;
    }

    public string CurrentPath { get; init; }

    public string? ParentPath { get; init; }

    public List<FileEntryDto> Items { get; init; }

    public string? ContinuationToken { get; init; }
}
