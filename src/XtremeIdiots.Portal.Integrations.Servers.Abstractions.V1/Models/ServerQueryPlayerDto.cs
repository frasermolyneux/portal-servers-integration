namespace XtremeIdiots.Portal.ServersApi.Abstractions.Models
{
    public record ServerQueryPlayerDto
    {
        public string? Name { get; set; }
        public int Score { get; set; }
    }
}
