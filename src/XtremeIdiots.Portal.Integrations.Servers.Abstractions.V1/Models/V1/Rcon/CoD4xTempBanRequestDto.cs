namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record CoD4xTempBanRequestDto
{
    public required string PlayerIdentifier { get; init; }
    public required int DurationMinutes { get; init; }
    public string? Reason { get; init; }
}
