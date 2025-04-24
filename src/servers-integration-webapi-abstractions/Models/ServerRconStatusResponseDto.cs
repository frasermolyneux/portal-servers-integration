namespace XtremeIdiots.Portal.ServersApi.Abstractions.Models
{
    public record ServerRconStatusResponseDto
    {
        public IList<ServerRconPlayerDto> Players { get; set; } = new List<ServerRconPlayerDto>();
    }
}
