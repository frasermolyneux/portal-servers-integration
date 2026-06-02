using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Helpers;

[Trait("Category", "Unit")]
public class PlayerResolutionMatcherTests
{
    [Theory]
    [InlineData("^1Fra.ser ^7Molyneux", "frasermolyneux")]
    [InlineData("  ^aCOD4x_Player!! ", "cod4xplayer")]
    [InlineData("[XI]-Noob", "xinoob")]
    public void Normalize_StripsColorCodesAndPunctuation(string input, string expected)
    {
        var result = PlayerResolutionMatcher.Normalize(input);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ResolvePlayer_ExactNormalizedMatch_ReturnsResolved()
    {
        var result = PlayerResolutionMatcher.ResolvePlayer(
        [
            new TestRconPlayer(3, "^1Fraser"),
            new TestRconPlayer(4, "AnotherPlayer")
        ],
        "fraser",
        3);

        Assert.Equal(ResolvePlayerStatus.Resolved, result.Status);
        Assert.NotNull(result.ResolvedPlayer);
        Assert.Equal("^1Fraser", result.ResolvedPlayer!.Name);
        Assert.Equal(3, result.ResolvedPlayer.Slot);
    }

    [Fact]
    public void ResolvePlayer_UniqueContainsMatch_ReturnsResolved()
    {
        var result = PlayerResolutionMatcher.ResolvePlayer(
        [
            new TestRconPlayer(2, "Alpha-One"),
            new TestRconPlayer(9, "Bravo-Two")
        ],
        "avo tw",
        3);

        Assert.Equal(ResolvePlayerStatus.Resolved, result.Status);
        Assert.NotNull(result.ResolvedPlayer);
        Assert.Equal("Bravo-Two", result.ResolvedPlayer!.Name);
    }

    [Fact]
    public void ResolvePlayer_NoMatches_ReturnsNotFound()
    {
        var result = PlayerResolutionMatcher.ResolvePlayer(
        [
            new TestRconPlayer(1, "Alpha"),
            new TestRconPlayer(2, "Bravo")
        ],
        "charlie",
        3);

        Assert.Equal(ResolvePlayerStatus.NotFound, result.Status);
        Assert.Null(result.ResolvedPlayer);
        Assert.Empty(result.Suggestions);
    }

    [Fact]
    public void ResolvePlayer_AmbiguousCloseMatches_ReturnsAmbiguousWithOrderedSuggestions()
    {
        var result = PlayerResolutionMatcher.ResolvePlayer(
        [
            new TestRconPlayer(2, "Frazz"),
            new TestRconPlayer(1, "Frase"),
            new TestRconPlayer(3, "NotClose")
        ],
        "fra",
        3);

        Assert.Equal(ResolvePlayerStatus.Ambiguous, result.Status);
        Assert.Null(result.ResolvedPlayer);
        Assert.Equal(2, result.Suggestions.Count);
        Assert.Equal("Frase", result.Suggestions[0].Name);
        Assert.Equal("Frazz", result.Suggestions[1].Name);
    }

    [Fact]
    public void ResolvePlayer_SuggestionCountIsCapped()
    {
        var result = PlayerResolutionMatcher.ResolvePlayer(
        [
            new TestRconPlayer(1, "FraOne"),
            new TestRconPlayer(2, "FraTwo"),
            new TestRconPlayer(3, "FraThree"),
            new TestRconPlayer(4, "FraFour")
        ],
        "fra",
        2);

        Assert.Equal(ResolvePlayerStatus.Ambiguous, result.Status);
        Assert.Equal(2, result.Suggestions.Count);
    }

    private sealed class TestRconPlayer(int num, string? name, string? guid = null) : IRconPlayer
    {
        public int Num { get; set; } = num;
        public string? Guid { get; set; } = guid;
        public string? Name { get; set; } = name;
        public string? IpAddress { get; set; }
        public int Rate { get; set; }
        public int Ping { get; set; }
    }
}
