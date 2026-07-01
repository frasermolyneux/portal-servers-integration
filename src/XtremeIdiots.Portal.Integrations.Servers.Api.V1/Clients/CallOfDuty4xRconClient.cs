using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Clients;

public class CallOfDuty4xRconClient(ILogger logger) : Quake3RconClient(logger), ICallOfDuty4xRconClient
{
    private static readonly char[] InvalidCommandTokenCharacters = ['"', ';', '\r', '\n'];
    private static readonly char[] InvalidCommandTextCharacters = [';', '\r', '\n'];

    public Task<string> PermBan(string player, string reason)
    {
        var command = AppendReason($"permban {FormatCommandTarget(player)}", reason);
        return ExecuteCommand(command, "Attempting CoD4x permban for target {Target}", "Failed to execute CoD4x permban command", player);
    }

    public Task<string> BanUser(string player, string reason)
    {
        var command = AppendReason($"banUser {FormatCommandTarget(player)}", reason);
        return ExecuteCommand(command, "Attempting CoD4x banUser for target {Target}", "Failed to execute CoD4x banUser command", player);
    }

    public Task<string> BanClient(int clientId, string reason)
    {
        var command = AppendReason($"banClient {clientId}", reason);
        return ExecuteCommand(command, "Attempting CoD4x banClient for slot {ClientId}", "Failed to execute CoD4x banClient command", clientId);
    }

    public Task<string> TempBan(string player, int durationMinutes, string reason)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(durationMinutes, 1);
        var command = AppendReason($"tempban {FormatCommandTarget(player)} {durationMinutes}", reason);
        return ExecuteCommand(command, "Attempting CoD4x tempban for target {Target} with duration {DurationMinutes}", "Failed to execute CoD4x tempban command", player, durationMinutes);
    }

    public Task<string> Unban(string playerIdentifier)
    {
        return ExecuteCommand($"unban {FormatCommandTarget(playerIdentifier)}", "Attempting CoD4x unban for target {Target}", "Failed to execute CoD4x unban command", playerIdentifier);
    }

    public Task<string> UnbanUser(string playerIdentifier)
    {
        return ExecuteCommand($"unbanUser {FormatCommandTarget(playerIdentifier)}", "Attempting CoD4x unbanUser for target {Target}", "Failed to execute CoD4x unbanUser command", playerIdentifier);
    }

    public Task<string> Kick(string player, string reason)
    {
        var command = AppendReason($"kick {FormatCommandTarget(player)}", reason);
        return ExecuteCommand(command, "Attempting CoD4x kick for target {Target}", "Failed to execute CoD4x kick command", player);
    }

    public Task<string> ClientKick(int clientId, string reason)
    {
        var command = AppendReason($"clientKick {clientId}", reason);
        return ExecuteCommand(command, "Attempting CoD4x clientKick for slot {ClientId}", "Failed to execute CoD4x clientKick command", clientId);
    }

    public Task<string> OnlyKick(int clientId, string reason)
    {
        var command = AppendReason($"onlykick {clientId}", reason);
        return ExecuteCommand(command, "Attempting CoD4x onlykick for slot {ClientId}", "Failed to execute CoD4x onlykick command", clientId);
    }

    public Task<string> Status()
    {
        return ExecuteCommand("status", "Attempting CoD4x status command", "Failed to execute CoD4x status command");
    }

    public Task<string> MiniStatus()
    {
        return ExecuteCommand("ministatus", "Attempting CoD4x ministatus command", "Failed to execute CoD4x ministatus command");
    }

    public Task<string> DumpUser(string userId)
    {
        return ExecuteCommand($"dumpuser {FormatCommandTarget(userId)}", "Attempting CoD4x dumpuser for target {Target}", "Failed to execute CoD4x dumpuser command", userId);
    }

    public Task<string> DumpBanList()
    {
        return ExecuteCommand("dumpbanlist", "Attempting CoD4x dumpbanlist command", "Failed to execute CoD4x dumpbanlist command");
    }

    public Task<string> ServerInfo()
    {
        return ExecuteCommand("serverinfo", "Attempting CoD4x serverinfo command", "Failed to execute CoD4x serverinfo command");
    }

    public Task<string> SystemInfo()
    {
        return ExecuteCommand("systeminfo", "Attempting CoD4x systeminfo command", "Failed to execute CoD4x systeminfo command");
    }

    public Task<string> ScreenSay(string message)
    {
        return ExecuteCommand($"screensay {Quote(message)}", "Attempting CoD4x screensay", "Failed to execute CoD4x screensay command");
    }

    public Task<string> ConSay(string message)
    {
        return ExecuteCommand($"consay {Quote(message)}", "Attempting CoD4x consay", "Failed to execute CoD4x consay command");
    }

    public Task<string> Tell(string client, string message)
    {
        return ExecuteCommand($"tell {FormatCommandTarget(client)} {Quote(message)}", "Attempting CoD4x tell for target {Target}", "Failed to execute CoD4x tell command", client);
    }

    public Task<string> ScreenTell(string client, string message)
    {
        return ExecuteCommand($"screentell {FormatCommandTarget(client)} {Quote(message)}", "Attempting CoD4x screentell for target {Target}", "Failed to execute CoD4x screentell command", client);
    }

    public Task<string> ConTell(string client, string message)
    {
        return ExecuteCommand($"contell {FormatCommandTarget(client)} {Quote(message)}", "Attempting CoD4x contell for target {Target}", "Failed to execute CoD4x contell command", client);
    }

    public Task<string> Map(string mapName)
    {
        return ExecuteCommand($"map {FormatCommandToken(mapName, nameof(mapName))}", "Attempting CoD4x map load for {MapName}", "Failed to execute CoD4x map command", mapName);
    }

    public Task<string> MapRestart()
    {
        return ExecuteCommand("map_restart", "Attempting CoD4x map_restart command", "Failed to execute CoD4x map_restart command");
    }

    public Task<string> FastRestart()
    {
        return ExecuteCommand("fast_restart", "Attempting CoD4x fast_restart command", "Failed to execute CoD4x fast_restart command");
    }

    public Task<string> MapRotate()
    {
        return ExecuteCommand("map_rotate", "Attempting CoD4x map_rotate command", "Failed to execute CoD4x map_rotate command");
    }

    public Task<string> Gametype(string gameType)
    {
        return ExecuteCommand($"gametype {FormatCommandToken(gameType, nameof(gameType))}", "Attempting CoD4x gametype change to {GameTypeName}", "Failed to execute CoD4x gametype command", gameType);
    }

    public Task<string> KillServer()
    {
        return ExecuteCommand("killserver", "Attempting CoD4x killserver command", "Failed to execute CoD4x killserver command");
    }

    public Task<string> Record(string client, string? demoName = null)
    {
        var command = string.IsNullOrWhiteSpace(demoName)
            ? $"record {FormatCommandTarget(client)}"
            : $"record {FormatCommandTarget(client)} {Quote(demoName)}";

        return ExecuteCommand(command, "Attempting CoD4x record for target {Target}", "Failed to execute CoD4x record command", client);
    }

    public Task<string> StopRecord(string client)
    {
        return ExecuteCommand($"stoprecord {FormatCommandTarget(client)}", "Attempting CoD4x stoprecord for target {Target}", "Failed to execute CoD4x stoprecord command", client);
    }

    public Task<string> AdminAddAdmin(string user, int power)
    {
        return ExecuteCommand($"AdminAddAdmin {FormatCommandTarget(user)} {power}", "Attempting CoD4x AdminAddAdmin for user {User}", "Failed to execute CoD4x AdminAddAdmin command", user);
    }

    public Task<string> AdminRemoveAdmin(string user)
    {
        return ExecuteCommand($"AdminRemoveAdmin {FormatCommandTarget(user)}", "Attempting CoD4x AdminRemoveAdmin for user {User}", "Failed to execute CoD4x AdminRemoveAdmin command", user);
    }

    public Task<string> AdminListAdmins()
    {
        return ExecuteCommand("AdminListAdmins", "Attempting CoD4x AdminListAdmins command", "Failed to execute CoD4x AdminListAdmins command");
    }

    public Task<string> AdminChangePassword(string user, string newPassword)
    {
        return ExecuteCommand($"AdminChangePassword {FormatCommandTarget(user)} {Quote(newPassword)}", "Attempting CoD4x AdminChangePassword for user {User}", "Failed to execute CoD4x AdminChangePassword command", user);
    }

    public Task<string> AdminChangeCommandPower(string command, int minPower)
    {
        return ExecuteCommand($"AdminChangeCommandPower {FormatCommandToken(command, nameof(command))} {minPower}", "Attempting CoD4x AdminChangeCommandPower for command {Command}", "Failed to execute CoD4x AdminChangeCommandPower command", command);
    }

    public Task<string> AdminListCommands()
    {
        return ExecuteCommand("AdminListCommands", "Attempting CoD4x AdminListCommands command", "Failed to execute CoD4x AdminListCommands command");
    }

    public Task<string> Undercover(bool enabled)
    {
        return ExecuteCommand($"undercover {(enabled ? 1 : 0)}", "Attempting CoD4x undercover mode change", "Failed to execute CoD4x undercover command");
    }

    public Task<string> Set(string dvarName, string value)
    {
        return ExecuteCommand($"set {FormatCommandToken(dvarName, nameof(dvarName))} {Quote(value)}", "Attempting CoD4x set for dvar {DvarName}", "Failed to execute CoD4x set command", dvarName);
    }

    public Task<string> Seta(string dvarName, string value)
    {
        return ExecuteCommand($"seta {FormatCommandToken(dvarName, nameof(dvarName))} {Quote(value)}", "Attempting CoD4x seta for dvar {DvarName}", "Failed to execute CoD4x seta command", dvarName);
    }

    public Task<string> Sets(string dvarName, string value)
    {
        return ExecuteCommand($"sets {FormatCommandToken(dvarName, nameof(dvarName))} {Quote(value)}", "Attempting CoD4x sets for dvar {DvarName}", "Failed to execute CoD4x sets command", dvarName);
    }

    public Task<string> Setu(string dvarName, string value)
    {
        return ExecuteCommand($"setu {FormatCommandToken(dvarName, nameof(dvarName))} {Quote(value)}", "Attempting CoD4x setu for dvar {DvarName}", "Failed to execute CoD4x setu command", dvarName);
    }

    public Task<string> CvarList()
    {
        return ExecuteCommand("cvarlist", "Attempting CoD4x cvarlist command", "Failed to execute CoD4x cvarlist command");
    }

    public Task<string> CmdList()
    {
        return ExecuteCommand("cmdlist", "Attempting CoD4x cmdlist command", "Failed to execute CoD4x cmdlist command");
    }

    public Task<string> LoadPlugin(string pluginName)
    {
        return ExecuteCommand($"loadPlugin {FormatCommandToken(pluginName, nameof(pluginName))}", "Attempting CoD4x loadPlugin for {PluginName}", "Failed to execute CoD4x loadPlugin command", pluginName);
    }

    public Task<string> UnloadPlugin(string pluginName)
    {
        return ExecuteCommand($"unloadPlugin {FormatCommandToken(pluginName, nameof(pluginName))}", "Attempting CoD4x unloadPlugin for {PluginName}", "Failed to execute CoD4x unloadPlugin command", pluginName);
    }

    public Task<string> Plugins()
    {
        return ExecuteCommand("plugins", "Attempting CoD4x plugins command", "Failed to execute CoD4x plugins command");
    }

    public Task<string> PluginInfo(string pluginName)
    {
        return ExecuteCommand($"pluginInfo {FormatCommandToken(pluginName, nameof(pluginName))}", "Attempting CoD4x pluginInfo for {PluginName}", "Failed to execute CoD4x pluginInfo command", pluginName);
    }

    public Task<string> BanPlayerByPlayerIdentifier(string playerIdentifier, string reason)
    {
        return ExecuteCommand(
            AppendReason($"permban {FormatCommandTarget(playerIdentifier)}", reason),
            "Attempting to permanently ban CoD4x player identifier {PlayerIdentifier}",
            "Failed to execute CoD4x permanent ban command",
            playerIdentifier);
    }

    public Task<string> TempBanPlayerByPlayerIdentifier(string playerIdentifier, int durationMinutes, string reason)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(durationMinutes, 1);

        return ExecuteCommand(
            AppendReason($"tempban {FormatCommandTarget(playerIdentifier)} {durationMinutes}", reason),
            "Attempting to temporarily ban CoD4x player identifier {PlayerIdentifier} for {DurationMinutes} minutes",
            "Failed to execute CoD4x temporary ban command",
            playerIdentifier,
            durationMinutes);
    }

    public Task<string> UnbanPlayerByPlayerIdentifier(string playerIdentifier)
    {
        return ExecuteCommand(
            $"unban {FormatCommandTarget(playerIdentifier)}",
            "Attempting to unban CoD4x player identifier {PlayerIdentifier}",
            "Failed to execute CoD4x unban command",
            playerIdentifier);
    }

    public override Task<string> TakeScreenshot(string playerIdentifier, CancellationToken cancellationToken = default)
    {
        return SendCommandWithRetry(
            $"getss {FormatCommandTarget(playerIdentifier)}",
            "Attempting to trigger screenshot for player identifier {PlayerIdentifier}",
            "Failed to trigger screenshot for player identifier {PlayerIdentifier}",
            cancellationToken,
            playerIdentifier);
    }

    private Task<string> ExecuteCommand(string command, string debugMessage, string warningMessage, params object?[] debugMessageArgs)
    {
        return SendCommandWithRetry(command, debugMessage, warningMessage, debugMessageArgs);
    }

    private static string FormatCommandTarget(string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);

        var normalized = target.Trim();
        if (normalized.Length > 1 && normalized[0] == '"' && normalized[^1] == '"')
        {
            normalized = normalized[1..^1].Trim();
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(normalized);

        if (normalized.IndexOfAny(InvalidCommandTextCharacters) >= 0)
        {
            throw new ArgumentException("Target contains unsupported characters.", nameof(target));
        }

        return normalized.Any(char.IsWhiteSpace) || normalized.Contains('"')
            ? Quote(normalized)
            : normalized;
    }

    private static string FormatCommandToken(string token, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token, paramName);
        var normalized = token.Trim();

        if (normalized.Any(char.IsWhiteSpace) || normalized.IndexOfAny(InvalidCommandTokenCharacters) >= 0)
        {
            throw new ArgumentException("Value contains unsupported characters.", paramName);
        }

        return normalized;
    }

    private static string AppendReason(string command, string reason)
    {
        return string.IsNullOrWhiteSpace(reason)
            ? command
            : $"{command} {Quote(reason)}";
    }

    private static string Quote(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();

        if (normalized.IndexOfAny(InvalidCommandTextCharacters) >= 0)
        {
            throw new ArgumentException("Value contains unsupported characters.", nameof(value));
        }

        return $"\"{normalized.Replace("\"", "\\\"")}\"";
    }
}
