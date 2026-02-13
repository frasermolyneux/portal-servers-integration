using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Models.V1
{
    internal class SourceQueryResponse : IQueryResponse
    {
        public SourceQueryResponse(Dictionary<string, string> serverParams, List<IQueryPlayer> players)
        {
            ServerParams = serverParams;
            Players = players;
        }

        public string ServerName => ServerParams.ContainsKey("hostname") ? ServerParams["hostname"] : string.Empty;

        public string Map => ServerParams.ContainsKey("mapname") ? ServerParams["mapname"] : string.Empty;
        public string Mod => ServerParams.ContainsKey("modname") ? ServerParams["modname"] : string.Empty;
        public int MaxPlayers => ServerParams.ContainsKey("maxplayers") ? Convert.ToInt32(ServerParams["maxplayers"]) : 0;

        public int PlayerCount => Players.Count;

        public IDictionary<string, string> ServerParams { get; set; }
        public IList<IQueryPlayer> Players { get; set; }
    }
}