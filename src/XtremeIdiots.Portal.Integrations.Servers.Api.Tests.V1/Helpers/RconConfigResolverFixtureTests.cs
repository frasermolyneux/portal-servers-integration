using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Helpers;

[Trait("Category", "Unit")]
public class RconConfigResolverFixtureTests
{
    public static IEnumerable<object[]> RconPayloadCases()
    {
        yield return new object[] { "Rcon/rcon-valid-password.json", "rcon-secret" };
        yield return new object[] { "Rcon/rcon-edge-empty-password.json", string.Empty };
        yield return new object[] { "Rcon/rcon-edge-null-password.json", null! };
        yield return new object[] { "Rcon/rcon-invalid-missing-password.json", null! };
        yield return new object[] { "Rcon/rcon-invalid-nonstring-password.json", null! };
    }

    [Theory]
    [MemberData(nameof(RconPayloadCases))]
    public void ParsePasswordFromConfig_WithFixturePayloads_ReturnsExpectedValue(string fixturePath, string? expectedPassword)
    {
        var payload = ResolverFixtureLoader.Load(fixturePath);

        var result = RconConfigResolver.ParsePasswordFromConfig(payload);

        Assert.Equal(expectedPassword, result);
    }

    [Fact]
    public void ParsePasswordFromConfig_WithMalformedJson_ReturnsNull()
    {
        var result = RconConfigResolver.ParsePasswordFromConfig("{\"password\":\"broken\"");

        Assert.Null(result);
    }

    [Fact]
    public void ParsePasswordFromConfig_WithWhitespacePayload_ReturnsNull()
    {
        var result = RconConfigResolver.ParsePasswordFromConfig(" \t ");

        Assert.Null(result);
    }
}
