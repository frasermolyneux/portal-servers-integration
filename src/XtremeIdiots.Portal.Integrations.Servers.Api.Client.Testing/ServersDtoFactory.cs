using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Maps;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing;

/// <summary>
/// Static factory methods for creating test DTOs with sensible defaults.
/// All methods support parameter overrides for customisation.
/// </summary>
public static class ServersDtoFactory
{
    public static ServerQueryStatusResponseDto CreateQueryStatusResponse(
        string? serverName = "Test Server",
        string? map = "mp_crash",
        string? mod = "default",
        int maxPlayers = 32,
        int playerCount = 10,
        IDictionary<string, string>? serverParams = null,
        IList<ServerQueryPlayerDto>? players = null)
    {
        return new ServerQueryStatusResponseDto
        {
            ServerName = serverName,
            Map = map,
            Mod = mod,
            MaxPlayers = maxPlayers,
            PlayerCount = playerCount,
            ServerParams = serverParams ?? new Dictionary<string, string>
            {
                ["sv_hostname"] = serverName ?? "Test Server",
                ["g_gametype"] = "dm"
            },
            Players = players ?? [CreateQueryPlayer(), CreateQueryPlayer("Player2", 50)]
        };
    }

    public static ServerQueryPlayerDto CreateQueryPlayer(
        string? name = "Player1",
        int score = 100)
    {
        return new ServerQueryPlayerDto
        {
            Name = name,
            Score = score
        };
    }

    public static ServerRconStatusResponseDto CreateRconStatusResponse(
        IList<ServerRconPlayerDto>? players = null)
    {
        return new ServerRconStatusResponseDto
        {
            Players = players ?? [CreateRconPlayer()]
        };
    }

    public static ServerRconPlayerDto CreateRconPlayer(
        int num = 0,
        string? guid = "abc123",
        string? name = "TestPlayer",
        string? ipAddress = "192.168.1.100",
        int rate = 25000,
        int ping = 50)
    {
        return new ServerRconPlayerDto
        {
            Num = num,
            Guid = guid,
            Name = name,
            IpAddress = ipAddress,
            Rate = rate,
            Ping = ping
        };
    }

    public static RconMapCollectionDto CreateRconMapCollection(
        IEnumerable<RconMapDto>? maps = null)
    {
        return new RconMapCollectionDto(maps ?? [CreateRconMap()]);
    }

    public static RconMapDto CreateRconMap(
        string gameType = "dm",
        string mapName = "mp_crash")
    {
        return new RconMapDto(gameType, mapName);
    }

    public static RconCurrentMapDto CreateRconCurrentMap(
        string mapName = "mp_crash")
    {
        return new RconCurrentMapDto(mapName);
    }

    public static ServerMapsCollectionDto CreateServerMapsCollection(
        IEnumerable<ServerMapDto>? maps = null)
    {
        return new ServerMapsCollectionDto(maps ?? [CreateServerMap()]);
    }

    public static ServerMapDto CreateServerMap(
        string name = "mp_crash",
        string fullName = "mp_crash.bsp",
        DateTime? modified = null)
    {
        return new ServerMapDto(name, fullName, modified ?? new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }
}
