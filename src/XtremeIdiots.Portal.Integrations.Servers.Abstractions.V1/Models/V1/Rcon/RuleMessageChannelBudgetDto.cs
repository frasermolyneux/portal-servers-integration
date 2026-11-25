using System.Text.Json.Serialization;
using XtremeIdiots.Portal.Settings.Contracts.V1.Contracts.RulesEngine;

namespace XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;

public static class RuleMessageChannels
{
    public const string RconPublicChat = "rcon.chat.public";
    public const string RconPrivateChat = "rcon.chat.private";
    public const string RconPublicScreen = "rcon.screen.public";
    public const string RconPrivateScreen = "rcon.screen.private";
    public const string RconPublicConsole = "rcon.console.public";
    public const string RconPrivateConsole = "rcon.console.private";
}

[JsonConverter(typeof(JsonStringEnumConverter<RuleMessageBudgetStatus>))]
public enum RuleMessageBudgetStatus
{
    Unknown = 0,
    Available = 1,
    Unsupported = 2,
    Unavailable = 3
}

public record RuleMessageChannelBudgetDto
{
    public RuleMessageBudgetStatus Status { get; init; }
    public MessageChannelBudget? Budget { get; init; }
    public RconMessageCharacterConstraintsDto? Constraints { get; init; }
    public string? Detail { get; init; }
}

public record RconMessageCharacterConstraintsDto
{
    public bool AsciiOnly { get; init; }
    public bool ControlsDisallowed { get; init; }
    public string DisallowedCharacters { get; init; } = string.Empty;

    /// <summary>Rejects the CoD localization insertion sequence &amp;&amp; followed by an ASCII digit.</summary>
    public bool DisallowLocalizationInserts { get; init; }
}
