namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Maps;

public record MapVerificationRequestDto
{
    public required List<string> MapNames { get; init; }
}
