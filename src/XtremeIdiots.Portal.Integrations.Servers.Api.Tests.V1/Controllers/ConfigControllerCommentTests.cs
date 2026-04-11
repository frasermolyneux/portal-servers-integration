using XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Controllers;

[Trait("Category", "Unit")]
public class ConfigControllerCommentTests
{
    private const string Newline = "\n";

    [Fact]
    public void UpsertManagedCommentBlock_InsertsNewBlock_AboveVariable()
    {
        var content = "// some comment\nset sv_maprotation \"gametype war map mp_backlot\"\nset sv_hostname \"test\"";
        var result = ConfigController.UpsertManagedCommentBlock(
            content, "sv_maprotation",
            ["Managed by XtremeIdiots Portal", "Do not edit manually"],
            Newline);

        var lines = result.Split(Newline);
        Assert.Equal("// some comment", lines[0]);
        Assert.Equal("// [Portal] Managed by XtremeIdiots Portal", lines[1]);
        Assert.Equal("// [Portal] Do not edit manually", lines[2]);
        Assert.StartsWith("set sv_maprotation", lines[3]);
        Assert.StartsWith("set sv_hostname", lines[4]);
    }

    [Fact]
    public void UpsertManagedCommentBlock_ReplacesExistingBlock()
    {
        var content = "// [Portal] Old managed comment\n// [Portal] Old second line\nset sv_maprotation \"gametype war map mp_backlot\"";
        var result = ConfigController.UpsertManagedCommentBlock(
            content, "sv_maprotation",
            ["New managed comment"],
            Newline);

        var lines = result.Split(Newline);
        Assert.Equal("// [Portal] New managed comment", lines[0]);
        Assert.StartsWith("set sv_maprotation", lines[1]);
        Assert.Equal(2, lines.Length);
    }

    [Fact]
    public void UpsertManagedCommentBlock_RemovesBlock_WhenEmptyArray()
    {
        var content = "// [Portal] Old comment\nset sv_maprotation \"gametype war map mp_backlot\"";
        var result = ConfigController.UpsertManagedCommentBlock(
            content, "sv_maprotation",
            [],
            Newline);

        var lines = result.Split(Newline);
        Assert.Single(lines);
        Assert.StartsWith("set sv_maprotation", lines[0]);
    }

    [Fact]
    public void UpsertManagedCommentBlock_PreservesUnrelatedComments()
    {
        var content = "// Game config\n// Author: admin\nset sv_maprotation \"gametype war map mp_backlot\"";
        var result = ConfigController.UpsertManagedCommentBlock(
            content, "sv_maprotation",
            ["Managed by Portal"],
            Newline);

        var lines = result.Split(Newline);
        Assert.Equal("// Game config", lines[0]);
        Assert.Equal("// Author: admin", lines[1]);
        Assert.Equal("// [Portal] Managed by Portal", lines[2]);
        Assert.StartsWith("set sv_maprotation", lines[3]);
    }

    [Fact]
    public void UpsertManagedCommentBlock_HandlesCrLfNewlines()
    {
        var crlf = "\r\n";
        var content = $"// [Portal] Old comment{crlf}set sv_maprotation \"gametype war map mp_backlot\"{crlf}set sv_hostname \"test\"";
        var result = ConfigController.UpsertManagedCommentBlock(
            content, "sv_maprotation",
            ["New comment"],
            crlf);

        Assert.Contains($"// [Portal] New comment{crlf}set sv_maprotation", result);
        Assert.Contains($"{crlf}set sv_hostname", result);
    }

    [Fact]
    public void UpsertManagedCommentBlock_VariableNotFound_ReturnsUnchanged()
    {
        var content = "set sv_hostname \"test\"";
        var result = ConfigController.UpsertManagedCommentBlock(
            content, "sv_maprotation",
            ["Managed by Portal"],
            Newline);

        Assert.Equal(content, result);
    }

    [Fact]
    public void UpsertManagedCommentBlock_VariableAtFirstLine_InsertsAbove()
    {
        var content = "set sv_maprotation \"gametype war map mp_backlot\"";
        var result = ConfigController.UpsertManagedCommentBlock(
            content, "sv_maprotation",
            ["Managed by Portal"],
            Newline);

        var lines = result.Split(Newline);
        Assert.Equal("// [Portal] Managed by Portal", lines[0]);
        Assert.StartsWith("set sv_maprotation", lines[1]);
    }

    [Fact]
    public void UpsertManagedCommentBlock_CommentedOutVariable_IsNotMatched()
    {
        // ConfigVariableRegex only matches active "set" lines, not "//set" lines
        var content = "//set sv_maprotation \"gametype war map mp_backlot\"";
        var result = ConfigController.UpsertManagedCommentBlock(
            content, "sv_maprotation",
            ["Managed by Portal"],
            Newline);

        // Should return unchanged since the variable regex doesn't match commented-out lines
        Assert.Equal(content, result);
    }

    [Fact]
    public void UpsertManagedCommentBlock_SkipsEmptyCommentLines()
    {
        var content = "set sv_maprotation \"gametype war map mp_backlot\"";
        var result = ConfigController.UpsertManagedCommentBlock(
            content, "sv_maprotation",
            ["Managed by Portal", "", "Do not edit"],
            Newline);

        var lines = result.Split(Newline);
        Assert.Equal("// [Portal] Managed by Portal", lines[0]);
        Assert.Equal("// [Portal] Do not edit", lines[1]);
        Assert.StartsWith("set sv_maprotation", lines[2]);
    }

    [Fact]
    public void UpsertManagedCommentBlock_RemovesOrphanedManagedComments_SeparatedByBlankLine()
    {
        var content = "// [Portal] Old orphaned comment\n\n// [Portal] Adjacent comment\nset sv_maprotation \"gametype war map mp_backlot\"";
        var result = ConfigController.UpsertManagedCommentBlock(
            content, "sv_maprotation",
            ["New comment"],
            Newline);

        var lines = result.Split(Newline);
        // Blank line preserved, both old managed comments removed, new one inserted
        Assert.Equal("", lines[0]);
        Assert.Equal("// [Portal] New comment", lines[1]);
        Assert.StartsWith("set sv_maprotation", lines[2]);
        Assert.Equal(3, lines.Length);
    }

    [Fact]
    public void UpsertManagedCommentBlock_RemovesOrphanedManagedComments_SeparatedByRegularComment()
    {
        var content = "// [Portal] Old orphaned comment\n// User's own comment\n// [Portal] Adjacent comment\nset sv_maprotation \"gametype war map mp_backlot\"";
        var result = ConfigController.UpsertManagedCommentBlock(
            content, "sv_maprotation",
            ["New comment"],
            Newline);

        var lines = result.Split(Newline);
        // User's comment preserved, both managed comments removed, new one inserted
        Assert.Equal("// User's own comment", lines[0]);
        Assert.Equal("// [Portal] New comment", lines[1]);
        Assert.StartsWith("set sv_maprotation", lines[2]);
        Assert.Equal(3, lines.Length);
    }
}
