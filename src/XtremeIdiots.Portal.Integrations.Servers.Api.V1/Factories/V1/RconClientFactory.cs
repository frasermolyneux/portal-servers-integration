﻿using XtremeIdiots.Portal.RepositoryApi.Abstractions.Constants;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Clients;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Factories.V1
{
    public class RconClientFactory : IRconClientFactory
    {
        private readonly ILogger<RconClientFactory> _logger;

        public RconClientFactory(ILogger<RconClientFactory> logger)
        {
            _logger = logger;
        }

        public IRconClient CreateInstance(GameType gameType, Guid gameServerId, string hostname, int queryPort, string rconPassword)
        {
            IRconClient rconClient;

            switch (gameType)
            {
                case GameType.CallOfDuty2:
                case GameType.CallOfDuty4:
                case GameType.CallOfDuty5:
                    rconClient = new Quake3RconClient(_logger);
                    break;
                case GameType.Insurgency:
                case GameType.Rust:
                case GameType.Left4Dead2:
                    rconClient = new SourceRconClient(_logger);
                    break;
                default:
                    throw new Exception("Unsupported game type");
            }

            rconClient.Configure(gameType, gameServerId, hostname, queryPort, rconPassword);
            return rconClient;
        }
    }
}