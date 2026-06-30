namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record RconStatusResponseDto
{
    public List<RconStatusPlayerDto> Players { get; init; } = [];
}
