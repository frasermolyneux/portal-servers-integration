namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Maps;

public record MapVerificationResultDto
{
    public MapVerificationResultDto(string mapName, bool existsOnServer)
    {
        MapName = mapName;
        ExistsOnServer = existsOnServer;
    }

    public string MapName { get; set; }
    public bool ExistsOnServer { get; set; }
}
