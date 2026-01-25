namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon
{
    public record TellPlayerWithVerificationRequest
    {
        public required string Message { get; init; }
        public string? ExpectedPlayerName { get; init; }
    }
}
