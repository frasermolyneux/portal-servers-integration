namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Files;

public record FileMutationResultDto
{
    public FileMutationResultDto(
        FileMutationOperation operation,
        FileMutationOutcome outcome,
        string path,
        string? sourcePath = null,
        string? destinationPath = null,
        long? bytesWritten = null)
    {
        Operation = operation;
        Outcome = outcome;
        Path = path;
        SourcePath = sourcePath;
        DestinationPath = destinationPath;
        BytesWritten = bytesWritten;
    }

    public FileMutationOperation Operation { get; init; }

    public FileMutationOutcome Outcome { get; init; }

    public string Path { get; init; }

    public string? SourcePath { get; init; }

    public string? DestinationPath { get; init; }

    public long? BytesWritten { get; init; }
}
