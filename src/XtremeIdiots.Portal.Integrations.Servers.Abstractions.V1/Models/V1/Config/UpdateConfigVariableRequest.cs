namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Config;

public record UpdateConfigVariableRequest
{
    public required string Value { get; init; }
}
