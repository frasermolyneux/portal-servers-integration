namespace XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;

public interface ICallOfDuty4xRconClient
{
    Task<string> PermBan(string player, string reason);
    Task<string> BanUser(string player, string reason);
    Task<string> BanClient(int clientId, string reason);
    Task<string> TempBan(string player, int durationMinutes, string reason);
    Task<string> Unban(string playerIdentifier);
    Task<string> UnbanUser(string playerIdentifier);
    Task<string> Kick(string player, string reason);
    Task<string> ClientKick(int clientId, string reason);
    Task<string> OnlyKick(int clientId, string reason);

    Task<string> Status();
    Task<string> MiniStatus();
    Task<string> DumpUser(string userId);
    Task<string> ServerInfo();
    Task<string> SystemInfo();

    Task Say(string message);
    Task<string> ScreenSay(string message);
    Task<string> ConSay(string message);
    Task<string> Tell(string client, string message);
    Task<string> ScreenTell(string client, string message);
    Task<string> ConTell(string client, string message);

    Task<string> Map(string mapName);
    Task<string> MapRestart();
    Task<string> FastRestart();
    Task<string> MapRotate();
    Task<string> Gametype(string gameType);

    Task<string> KillServer();
    Task<string> Record(string client, string? demoName = null);
    Task<string> StopRecord(string client);

    Task<string> AdminAddAdmin(string user, int power);
    Task<string> AdminRemoveAdmin(string user);
    Task<string> AdminListAdmins();
    Task<string> AdminChangePassword(string user, string newPassword);
    Task<string> AdminChangeCommandPower(string command, int minPower);
    Task<string> AdminListCommands();

    Task<string> Undercover(bool enabled);
    Task<string> Set(string dvarName, string value);
    Task<string> Seta(string dvarName, string value);
    Task<string> Sets(string dvarName, string value);
    Task<string> Setu(string dvarName, string value);
    Task<string> CvarList();
    Task<string> CmdList();

    Task<string> LoadPlugin(string pluginName);
    Task<string> UnloadPlugin(string pluginName);
    Task<string> Plugins();
    Task<string> PluginInfo(string pluginName);

    Task<string> TakeScreenshot(string playerIdentifier, CancellationToken cancellationToken = default);
    Task<string> BanPlayerByPlayerIdentifier(string playerIdentifier);
    Task<string> TempBanPlayerByPlayerIdentifier(string playerIdentifier, int durationMinutes);
    Task<string> UnbanPlayerByPlayerIdentifier(string playerIdentifier);
}
