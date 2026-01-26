namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon
{
    public record PlayerNameRequest
    {
        public required string Name { get; init; }
    }
}
