using System.Reflection;
using System.Text.RegularExpressions;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Clients;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1;

[Trait("Category", "Unit")]
public class SourceRconClientRegexTests
{
    [Fact]
    public void PlayerRegex_ParsesBracketedIpv6Address()
    {
        // Arrange
        var regex = GetPlayerRegex();
        const string statusLine = "# 2 42 \"srt4fun\" STEAM_1:0:12345 00:10 53 0 active 25000 [2607:9b00:a200::10]:28960";

        // Act
        var match = regex.Match(statusLine);

        // Assert
        Assert.True(match.Success);
        Assert.Equal("2607:9b00:a200::10", NormalizeIp(match.Groups["ip"].Value));
    }

    [Fact]
    public void PlayerRegex_ParsesIpv4Address()
    {
        // Arrange
        var regex = GetPlayerRegex();
        const string statusLine = "# 3 43 \"playerTwo\" STEAM_1:0:55555 00:08 70 0 active 25000 192.168.1.50:28960";

        // Act
        var match = regex.Match(statusLine);

        // Assert
        Assert.True(match.Success);
        Assert.Equal("192.168.1.50", match.Groups["ip"].Value);
    }

    private static Regex GetPlayerRegex()
    {
        var method = typeof(SourceRconClient).GetMethod("PlayerRegex", BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var result = method!.Invoke(null, null);

        Assert.NotNull(result);
        Assert.True(result is Regex);

        return (Regex)result!;
    }

    private static string NormalizeIp(string ipAddress)
    {
        if (ipAddress.Length > 2 && ipAddress[0] == '[' && ipAddress[^1] == ']')
        {
            return ipAddress[1..^1];
        }

        return ipAddress;
    }
}
