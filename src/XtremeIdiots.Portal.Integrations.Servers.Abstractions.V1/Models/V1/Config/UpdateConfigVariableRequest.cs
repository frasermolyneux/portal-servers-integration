namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Config;

public record UpdateConfigVariableRequest
{
    public required string Value { get; init; }

    /// <summary>
    /// Optional comment lines to insert/update above the config variable.
    /// Each line will be prefixed with "// [Portal] " and rendered as a managed comment block.
    /// Pass an empty array to remove any existing managed comment block.
    /// </summary>
    public string[]? CommentLines { get; init; }
}
