namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record TellPlayerWithVerificationRequest
{
    public string? Message { get; init; }

    public IReadOnlyCollection<string>? Messages { get; init; }

    public string? ExpectedPlayerName { get; init; }
}
