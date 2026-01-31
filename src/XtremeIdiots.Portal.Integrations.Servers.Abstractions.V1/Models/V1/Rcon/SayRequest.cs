namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record SayRequest
{
    public required string Message { get; init; }
}
