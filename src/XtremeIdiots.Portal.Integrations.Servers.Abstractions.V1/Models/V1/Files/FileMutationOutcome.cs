namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Files;

public enum FileMutationOutcome
{
    Created,
    Replaced,
    Deleted,
    AlreadyExists,
    Moved,
    Copied,
    Renamed,
    NoOp,
}
