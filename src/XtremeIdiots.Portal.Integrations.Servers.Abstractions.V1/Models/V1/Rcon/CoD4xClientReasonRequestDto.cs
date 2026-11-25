namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record CoD4xClientReasonRequestDto
{
    // An omitted slot must not target slot zero.
    public int ClientId { get; init; } = -1;

    public string? Reason { get; init; }
}
