using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Clients;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Factories.V1;

public class RconClientFactory(ILogger<RconClientFactory> logger) : IRconClientFactory
{
    private readonly ILogger<RconClientFactory> _logger = logger;

    public IRconClient CreateInstance(GameType gameType, Guid gameServerId, string hostname, int queryPort, string rconPassword)
    {
        ArgumentNullException.ThrowIfNull(logger);

        IRconClient rconClient = gameType switch
        {
            GameType.CallOfDuty2 or GameType.CallOfDuty4 or GameType.CallOfDuty5 => new Quake3RconClient(_logger),
            GameType.Insurgency or GameType.Rust or GameType.Left4Dead2 => new SourceRconClient(_logger),
            _ => throw new NotSupportedException($"Game type {gameType} is not supported for RCON operations")
        };

        rconClient.Configure(gameType, gameServerId, hostname, queryPort, rconPassword);
        return rconClient;
    }
}