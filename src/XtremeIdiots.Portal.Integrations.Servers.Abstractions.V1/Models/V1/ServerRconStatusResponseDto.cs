namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1
{
    public record ServerRconStatusResponseDto
    {
        public IList<ServerRconPlayerDto> Players { get; set; } = new List<ServerRconPlayerDto>();
    }
}
