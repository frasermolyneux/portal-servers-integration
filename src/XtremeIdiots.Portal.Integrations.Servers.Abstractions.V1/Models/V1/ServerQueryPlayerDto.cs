namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1
{
    public record ServerQueryPlayerDto
    {
        public string? Name { get; set; }
        public int Score { get; set; }
    }
}
