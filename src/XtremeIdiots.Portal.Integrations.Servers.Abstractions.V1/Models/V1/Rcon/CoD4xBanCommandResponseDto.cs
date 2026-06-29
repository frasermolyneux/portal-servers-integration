namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record CoD4xBanCommandResponseDto
{
    public string Outcome { get; set; } = "Unknown";
    public bool IsSuccess { get; set; }
    public string? PlayerIdentifier { get; set; }
    public string? PlayerName { get; set; }
    public string? BanReason { get; set; }
    public string? ErrorMessage { get; set; }
    public string RawResponse { get; set; } = string.Empty;
}
