namespace XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1
{
    public interface IQueryResponse
    {
        string ServerName { get; }
        string Map { get; }
        string Mod { get; }
        int MaxPlayers { get; }
        int PlayerCount { get; }

        IDictionary<string, string> ServerParams { get; }
        IList<IQueryPlayer> Players { get; }
    }
}