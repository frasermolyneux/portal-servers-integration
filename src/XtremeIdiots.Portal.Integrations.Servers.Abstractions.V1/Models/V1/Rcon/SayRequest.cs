namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record SayRequest
{
    public string? Message { get; init; }

    public IReadOnlyCollection<string>? Messages { get; init; }
}
