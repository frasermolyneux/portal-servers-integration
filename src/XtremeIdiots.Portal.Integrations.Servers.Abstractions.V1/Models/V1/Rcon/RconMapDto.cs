namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon
{
    public record RconMapDto
    {
        public RconMapDto(string gameType, string mapName)
        {
            GameType = gameType;
            MapName = mapName;
        }

        public string GameType { get; set; }
        public string MapName { get; set; }
    }
}
