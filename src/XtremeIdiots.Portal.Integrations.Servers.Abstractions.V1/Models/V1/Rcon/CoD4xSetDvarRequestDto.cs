namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record CoD4xSetDvarRequestDto
{
    public string? DvarName { get; init; }

    public string? Value { get; init; }
}
