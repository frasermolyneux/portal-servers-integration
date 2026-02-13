using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Clients;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Factories.V1;

public class QueryClientFactory(ILogger<QueryClientFactory> logger) : IQueryClientFactory
{
    public IQueryClient CreateInstance(GameType gameType, string hostname, int queryPort)
    {
        ArgumentNullException.ThrowIfNull(logger);

        IQueryClient queryClient = gameType switch
        {
            GameType.CallOfDuty2 or GameType.CallOfDuty4 or GameType.CallOfDuty5 => new Quake3QueryClient(logger),
            GameType.Insurgency or GameType.Rust or GameType.Left4Dead2 => new SourceQueryClient(logger),
            _ => throw new NotSupportedException($"Game type {gameType} is not supported for query operations")
        };

        queryClient.Configure(hostname, queryPort);
        return queryClient;
    }
}