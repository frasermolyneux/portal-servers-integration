namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon
{
    public record RconCurrentMapDto
    {
        public RconCurrentMapDto(string mapName)
        {
            MapName = mapName;
        }

        public string MapName { get; set; }
    }
}
