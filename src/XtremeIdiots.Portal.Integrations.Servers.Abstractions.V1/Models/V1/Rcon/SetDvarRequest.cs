namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record SetDvarRequest
{
    public required string DvarName { get; init; }

    public required string Value { get; init; }
}
