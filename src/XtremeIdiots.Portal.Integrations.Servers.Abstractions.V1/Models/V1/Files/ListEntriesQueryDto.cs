namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Files;

public record ListEntriesQueryDto
{
    public string? Path { get; init; }

    public bool Recursive { get; init; }

    public bool IncludeHidden { get; init; }

    public int? PageSize { get; init; }

    public string? ContinuationToken { get; init; }
}
