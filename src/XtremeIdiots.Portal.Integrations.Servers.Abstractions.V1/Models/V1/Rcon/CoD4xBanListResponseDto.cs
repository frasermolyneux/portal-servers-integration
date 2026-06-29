namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public record CoD4xBanListResponseDto
{
    public IList<CoD4xBanEntryDto> Entries { get; set; } = [];
    public int ActiveBanCount { get; set; }
    public string RawResponse { get; set; } = string.Empty;
}
