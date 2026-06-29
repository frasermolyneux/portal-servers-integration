namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Files;

public record PatchFileEntryRequestDto
{
    public FileEntryPatchOperation Operation { get; init; }

    public string? SourcePath { get; init; }

    public string? DestinationPath { get; init; }

    public bool Overwrite { get; init; }

    public bool CreateDestinationDirectories { get; init; }

    public bool Recursive { get; init; }
}
