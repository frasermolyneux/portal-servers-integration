using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Settings.Contracts.V1.Contracts.RulesEngine;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;

internal static class MessageChannelBudgetResolver
{
    private const int InterimDisplayBytes = 149;
    private const int UnverifiedPrefixAllowanceBytes = 32;
    private const int CoD4xPrivateScreenOrConsolePrefixBytes = 8; // "^5PM: ^7"

    public static async Task<RuleMessageChannelBudgetDto> ResolveAsync(
        GameType gameType, string channel, IRconClient client, ILogger logger, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var chat = channel is RuleMessageChannels.RconPublicChat or RuleMessageChannels.RconPrivateChat;
        var screenOrConsole = channel is RuleMessageChannels.RconPublicScreen or RuleMessageChannels.RconPrivateScreen
            or RuleMessageChannels.RconPublicConsole or RuleMessageChannels.RconPrivateConsole;
        var quake = gameType is GameType.CallOfDuty2 or GameType.CallOfDuty4 or GameType.CallOfDuty5 or GameType.CallOfDuty4x;
        var source = gameType is GameType.Insurgency or GameType.Left4Dead2 or GameType.Rust;

        if ((!chat && !screenOrConsole) ||
            (screenOrConsole && gameType != GameType.CallOfDuty4x) ||
            (chat && !quake && !source) ||
            (source && channel != RuleMessageChannels.RconPublicChat))
        {
            return Unsupported("This game and channel do not have a supported RCON message transport.");
        }

        var reserved = UnverifiedPrefixAllowanceBytes;
        var evidence = "Interim policy: 149 total display bytes and a 32-byte prefix allowance; this game's display cutoff and prefix bound are not independently verified.";

        if (gameType == GameType.CallOfDuty4x && chat)
        {
            var variable = channel == RuleMessageChannels.RconPublicChat ? "sv_consayname" : "sv_contellname";
            string response;
            try
            {
                response = await client.GetDvar(variable, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Unable to read CoD4x message prefix {Variable}", variable);
                return Unavailable("The current CoD4x message prefix could not be read.");
            }

            if (response is null || !CoD4xChatPrefix.TryGetValue(variable, response, out var prefix) ||
                prefix.Any(c => c < 32 || c == 127 || c is '"' or '\\') ||
                ContainsLocalizationInsert(prefix))
            {
                logger.LogWarning("The CoD4x message prefix {Variable} is unavailable or cannot be safely represented", variable);
                return Unavailable("The current CoD4x message prefix is unavailable or contains unsupported framing.");
            }

            reserved = prefix.Length;
            if (reserved >= InterimDisplayBytes)
            {
                return Unavailable("The current CoD4x message prefix leaves no room in the display budget.");
            }

            evidence = "CoD4x client chat copies at most 149 post-localization bytes (CoD4x_Server 0223d6f, _cg_servercmds_mp.asm); current RCON prefix bytes are reserved. Prefix localization can expand and is not verified.";
        }
        else if (gameType == GameType.CallOfDuty4x)
        {
            reserved = channel is RuleMessageChannels.RconPrivateScreen or RuleMessageChannels.RconPrivateConsole
                ? CoD4xPrivateScreenOrConsolePrefixBytes
                : 0;
            evidence = "Interim policy: 149 total display bytes; CoD4x screen/console display cutoffs are not independently verified. The native private prefix is reserved.";
        }

        return new RuleMessageChannelBudgetDto
        {
            Status = RuleMessageBudgetStatus.Available,
            Budget = new MessageChannelBudget(channel, InterimDisplayBytes, "rcon", evidence, reserved, gameType.ToString(),
                RconSafePrintableAscii: true),
            Constraints = new RconMessageCharacterConstraintsDto
            {
                AsciiOnly = true,
                ControlsDisallowed = true,
                DisallowedCharacters = ";\"\\",
                DisallowLocalizationInserts = gameType == GameType.CallOfDuty4x
            },
            Detail = evidence
        };
    }

    public static bool IsRepresentable(GameType gameType, string message) =>
        message.Length > 0 &&
        !message.Any(c => c < 32 || c > 126 || c is ';' or '"' or '\\') &&
        (gameType != GameType.CallOfDuty4x || !ContainsLocalizationInsert(message));

    private static bool ContainsLocalizationInsert(string value)
    {
        for (var i = 0; i + 2 < value.Length; i++)
        {
            if (value[i] == '&' && value[i + 1] == '&' && value[i + 2] is >= '0' and <= '9')
            {
                return true;
            }
        }

        return false;
    }

    private static RuleMessageChannelBudgetDto Unsupported(string detail) =>
        new() { Status = RuleMessageBudgetStatus.Unsupported, Detail = detail };

    private static RuleMessageChannelBudgetDto Unavailable(string detail) =>
        new() { Status = RuleMessageBudgetStatus.Unavailable, Detail = detail };
}
