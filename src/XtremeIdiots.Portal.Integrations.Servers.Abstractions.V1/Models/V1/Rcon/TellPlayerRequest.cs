namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon
{
    public record TellPlayerRequest
    {
        public required string Message { get; init; }
    }
}
