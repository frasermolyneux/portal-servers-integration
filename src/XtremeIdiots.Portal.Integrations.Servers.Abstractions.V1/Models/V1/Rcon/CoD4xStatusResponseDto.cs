namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record CoD4xStatusResponseDto
{
    public string? Hostname { get; set; }
    public string? Version { get; set; }
    public string? UdpEndpoint { get; set; }
    public string? OperatingSystem { get; set; }
    public string? ServerType { get; set; }
    public string? MapName { get; set; }
    public IList<CoD4xStatusPlayerDto> Players { get; set; } = [];
    public string RawResponse { get; set; } = string.Empty;
}
