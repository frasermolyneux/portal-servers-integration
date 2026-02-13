using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Models.V1
{
    internal class Quake3QueryResponse : IQueryResponse
    {
        public Quake3QueryResponse(Dictionary<string, string> serverParams, List<IQueryPlayer> players)
        {
            ServerParams = serverParams;
            Players = players;
        }

        public string ServerName => ServerParams.ContainsKey("sv_hostname") ? ServerParams["sv_hostname"] : string.Empty;

        public string Map => ServerParams.ContainsKey("mapname") ? ServerParams["mapname"] : string.Empty;
        public string Mod => ServerParams.ContainsKey("fs_game") ? ServerParams["fs_game"] : string.Empty;
        public int MaxPlayers => ServerParams.ContainsKey("sv_maxclients") ? Convert.ToInt32(ServerParams["sv_maxclients"]) : 0;

        public int PlayerCount => Players.Count;

        public IDictionary<string, string> ServerParams { get; set; }
        public IList<IQueryPlayer> Players { get; set; }
    }
}