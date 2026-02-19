using XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Testing.Tests;

[Trait("Category", "Unit")]
public class ServersDtoFactoryTests
{
    [Fact]
    public void CreateQueryStatusResponse_WithDefaults_ReturnsPopulatedDto()
    {
        var dto = ServersDtoFactory.CreateQueryStatusResponse();

        Assert.Equal("Test Server", dto.ServerName);
        Assert.Equal("mp_crash", dto.Map);
        Assert.Equal("default", dto.Mod);
        Assert.Equal(32, dto.MaxPlayers);
        Assert.Equal(10, dto.PlayerCount);
        Assert.Equal(2, dto.Players.Count);
        Assert.NotEmpty(dto.ServerParams);
    }

    [Fact]
    public void CreateQueryStatusResponse_WithCustomValues_ReturnsOverriddenDto()
    {
        var dto = ServersDtoFactory.CreateQueryStatusResponse(
            serverName: "Custom Server",
            map: "mp_backlot",
            maxPlayers: 64,
            playerCount: 20);

        Assert.Equal("Custom Server", dto.ServerName);
        Assert.Equal("mp_backlot", dto.Map);
        Assert.Equal(64, dto.MaxPlayers);
        Assert.Equal(20, dto.PlayerCount);
    }

    [Fact]
    public void CreateQueryPlayer_WithDefaults_ReturnsPopulatedDto()
    {
        var dto = ServersDtoFactory.CreateQueryPlayer();

        Assert.Equal("Player1", dto.Name);
        Assert.Equal(100, dto.Score);
    }

    [Fact]
    public void CreateRconStatusResponse_WithDefaults_HasPlayers()
    {
        var dto = ServersDtoFactory.CreateRconStatusResponse();

        Assert.Single(dto.Players);
    }

    [Fact]
    public void CreateRconPlayer_WithDefaults_ReturnsPopulatedDto()
    {
        var dto = ServersDtoFactory.CreateRconPlayer();

        Assert.Equal("TestPlayer", dto.Name);
        Assert.Equal("abc123", dto.Guid);
        Assert.Equal("192.168.1.100", dto.IpAddress);
        Assert.Equal(50, dto.Ping);
    }

    [Fact]
    public void CreateRconPlayer_WithCustomValues_ReturnsOverriddenDto()
    {
        var dto = ServersDtoFactory.CreateRconPlayer(num: 5, name: "CustomPlayer", ping: 200);

        Assert.Equal(5, dto.Num);
        Assert.Equal("CustomPlayer", dto.Name);
        Assert.Equal(200, dto.Ping);
    }

    [Fact]
    public void CreateRconMapCollection_WithDefaults_HasMaps()
    {
        var dto = ServersDtoFactory.CreateRconMapCollection();

        Assert.NotNull(dto);
    }

    [Fact]
    public void CreateRconMap_WithDefaults_ReturnsPopulatedDto()
    {
        var dto = ServersDtoFactory.CreateRconMap();

        Assert.Equal("dm", dto.GameType);
        Assert.Equal("mp_crash", dto.MapName);
    }

    [Fact]
    public void CreateRconCurrentMap_WithDefaults_ReturnsPopulatedDto()
    {
        var dto = ServersDtoFactory.CreateRconCurrentMap();

        Assert.Equal("mp_crash", dto.MapName);
    }

    [Fact]
    public void CreateServerMapsCollection_WithDefaults_HasMaps()
    {
        var dto = ServersDtoFactory.CreateServerMapsCollection();

        Assert.NotNull(dto);
    }

    [Fact]
    public void CreateServerMap_WithDefaults_ReturnsPopulatedDto()
    {
        var dto = ServersDtoFactory.CreateServerMap();

        Assert.Equal("mp_crash", dto.Name);
        Assert.Equal("mp_crash.bsp", dto.FullName);
    }

    [Fact]
    public void CreateServerMap_WithCustomValues_ReturnsOverriddenDto()
    {
        var modified = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var dto = ServersDtoFactory.CreateServerMap(name: "mp_backlot", fullName: "mp_backlot.bsp", modified: modified);

        Assert.Equal("mp_backlot", dto.Name);
        Assert.Equal("mp_backlot.bsp", dto.FullName);
        Assert.Equal(modified, dto.Modified);
    }
}
