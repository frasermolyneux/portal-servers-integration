namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record TakeScreenshotRequestDto
{
    public required string PlayerIdentifier { get; init; }
}
