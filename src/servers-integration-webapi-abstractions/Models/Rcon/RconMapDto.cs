namespace XtremeIdiots.Portal.ServersApi.Abstractions.Models.Rcon
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
