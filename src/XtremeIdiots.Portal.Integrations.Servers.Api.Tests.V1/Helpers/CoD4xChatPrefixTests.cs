using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Helpers;

public class CoD4xChatPrefixTests
{
    [Theory]
    [InlineData("\"sv_consayname\" is: \"^2Server: ^7^7\" default: \"^2Server: ^7^7\"", "sv_consayname", 12)]
    [InlineData("\"sv_contellname\" is: \"^7\" default: \"^5Server^7->^5PM: ^7^7\"", "sv_contellname", 0)]
    public void TryGetByteCount_ReadsActualValueWithoutDisplaySuffix(string response, string name, int expected)
    {
        Assert.True(CoD4xChatPrefix.TryGetByteCount(name, response, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("\"sv_consayname\" is: \"missing suffix\"")]
    [InlineData("\"sv_contellname\" is: \"^7\" default: \"^7\"")]
    [InlineData("\"sv_consayname\" is: \"bad\"value^7\" default: \"^7\"")]
    [InlineData("\"sv_consayname\" is: \"fake^7\" default: \"value^7\" default: \"^7\"")]
    [InlineData("\"sv_consayname\" is: \"one^7\" default: \"^7\"\n\"sv_consayname\" is: \"two^7\" default: \"^7\"")]
    public void TryGetByteCount_RejectsAmbiguousOrUnrelatedResponses(string response)
    {
        Assert.False(CoD4xChatPrefix.TryGetByteCount("sv_consayname", response, out _));
    }
}
