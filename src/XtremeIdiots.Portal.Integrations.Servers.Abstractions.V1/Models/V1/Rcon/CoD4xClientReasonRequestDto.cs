namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record CoD4xClientReasonRequestDto
{
    public int ClientId { get; init; }

    public string? Reason { get; init; }
}
