namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon
{
    public record PlayerVerificationRequest
    {
        public string? ExpectedPlayerName { get; init; }
    }
}
