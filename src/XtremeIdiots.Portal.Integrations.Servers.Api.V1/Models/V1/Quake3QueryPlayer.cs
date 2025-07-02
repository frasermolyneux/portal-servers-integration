using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Models.V1
{
    internal class Quake3QueryPlayer : IQueryPlayer
    {
        public int Ping { get; set; }
        public string Name { get; set; }
        public int Score { get; set; }
    }
}