using Polly;

using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.Models.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Clients
{
    public class Quake3RconClient : IRconClient
    {
        private readonly ILogger _logger;

        private GameType _gameType;
        private string _hostname;
        private int _queryPort;
        private string _rconPassword;

        private Guid _serverId;

        public Quake3RconClient(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Configure(GameType gameType, Guid gameServerId, string hostname, int queryPort, string rconPassword)
        {
            _logger.LogDebug("[{GameServerId}] Configuring Quake3 rcon client for {GameType} with endpoint {Hostname}:{QueryPort}", gameServerId, gameType, hostname, queryPort);

            _gameType = gameType;
            _serverId = gameServerId;
            _hostname = hostname;
            _queryPort = queryPort;
            _rconPassword = rconPassword;
        }

        public List<IRconPlayer> GetPlayers()
        {
            _logger.LogDebug("[{GameServerId}] Attempting to get a list of players from the server", _serverId);

            var players = new List<IRconPlayer>();

            var playerStatus = PlayerStatus();
            var lines = playerStatus.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
            for (var i = 3; i < lines.Count; i++)
            {
                var line = lines[i];
                var match = GameTypeRegex(_gameType).Match(line);

                if (!match.Success)
                    continue;

                var num = match.Groups[1].ToString();
                var score = match.Groups[2].ToString();
                var ping = match.Groups[3].ToString();
                var guid = match.Groups[4].ToString();
                var name = match.Groups[5].ToString().Trim();
                var ipAddress = match.Groups[7].ToString();
                var qPort = match.Groups[9].ToString();
                var rate = match.Groups[10].ToString();

                int.TryParse(num, out int numInt);
                int.TryParse(score, out int scoreInt);
                int.TryParse(ping, out int pingInt);
                int.TryParse(rate, out int rateInt);

                _logger.LogDebug("[{GameServerId}] Player {Name} with {Guid} and {IpAddress} parsed from result", _serverId, name, guid, ipAddress);

                players.Add(new Quake3RconPlayer
                {
                    Num = numInt,
                    Score = scoreInt,
                    Ping = pingInt,
                    Guid = guid,
                    Name = name,
                    IpAddress = ipAddress,
                    QPort = qPort,
                    Rate = rateInt
                });
            }

            return players;
        }

        public string GetCurrentMap()
        {
            _logger.LogDebug("[{GameServerId}] Attempting to get current map from the server", _serverId);

            try
            {
                var serverInfo = GetServerInfo().Result;
                
                // Parse the server info to extract the mapname
                // Server info format is key-value pairs separated by newlines: "mapname mp_crash\nsv_hostname ..."
                var lines = serverInfo.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (trimmedLine.StartsWith("mapname "))
                    {
                        var mapName = trimmedLine.Substring("mapname ".Length).Trim();
                        _logger.LogDebug("[{GameServerId}] Current map is {MapName}", _serverId, mapName);
                        return mapName;
                    }
                }

                _logger.LogWarning("[{GameServerId}] Map name not found in server info", _serverId);
                return "Unknown";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[{GameServerId}] Failed to get current map from server", _serverId);
                return "Unknown";
            }
        }

        public Task Say(string message)
        {
            _logger.LogDebug("[{GameServerId}] Attempting to send '{message}' to the server", _serverId, message);

            Policy.Handle<Exception>()
                .WaitAndRetry(GetRetryTimeSpans(), (result, timeSpan, retryCount, context) => { _logger.LogWarning("[{serverName}] Failed to execute rcon command - retry count: {count}", _serverId, retryCount); })
                .Execute(() => GetCommandPackets($"say \"{message}\""));

            return Task.CompletedTask;
        }

        public Task<List<Quake3QueryMap>> GetMaps()
        {
            var maps = MapRotation();

            var mapList = new List<Quake3QueryMap>();
            // The map rotation is returned in the format:
            // gametype {gameType} map {mapName}
            // or in the format map {mapName}
            // The game type is optional
            var mapRegex = new Regex(@"(?:gametype\s+([a-zA-Z0-9]+)\s+)?map\s+([a-zA-Z0-9_]+)");

            var matches = mapRegex.Matches(maps);
            foreach (Match match in matches)
            {
                var gameType = match.Groups[1].Success ? match.Groups[1].ToString() : "";
                var mapName = match.Groups[2].ToString();

                mapList.Add(new Quake3QueryMap { GameType = gameType, MapName = mapName });
            }

            return Task.FromResult(mapList);
        }

        public Task<string> Restart()
        {
            _logger.LogDebug("[{GameServerId}] Attempting to send restart the server", _serverId);

            var packets = Policy.Handle<Exception>()
                .WaitAndRetry(GetRetryTimeSpans(), (result, timeSpan, retryCount, context) => { _logger.LogWarning("[{serverName}] Failed to execute rcon command - retry count: {count}", _serverId, retryCount); })
                .Execute(() => GetCommandPackets("quit", true));

            return Task.FromResult("Restart command sent to the server");
        }

        public Task<string> RestartMap()
        {
            _logger.LogDebug("[{GameServerId}] Attempting to restart the current map", _serverId);

            var packets = Policy.Handle<Exception>()
                .WaitAndRetry(GetRetryTimeSpans(), (result, timeSpan, retryCount, context) => { _logger.LogWarning("[{serverName}] Failed to execute rcon command - retry count: {count}", _serverId, retryCount); })
                .Execute(() => GetCommandPackets("map_restart"));

            return Task.FromResult(GetStringFromPackets(packets));
        }

        public Task<string> FastRestartMap()
        {
            _logger.LogDebug("[{GameServerId}] Attempting to fast restart the current map", _serverId);

            var packets = Policy.Handle<Exception>()
                .WaitAndRetry(GetRetryTimeSpans(), (result, timeSpan, retryCount, context) => { _logger.LogWarning("[{serverName}] Failed to execute rcon command - retry count: {count}", _serverId, retryCount); })
                .Execute(() => GetCommandPackets("fast_restart"));

            return Task.FromResult(GetStringFromPackets(packets));
        }

        public Task<string> NextMap()
        {
            _logger.LogDebug("[{GameServerId}] Attempting to rotate to the next map", _serverId);

            var packets = Policy.Handle<Exception>()
                .WaitAndRetry(GetRetryTimeSpans(), (result, timeSpan, retryCount, context) => { _logger.LogWarning("[{serverName}] Failed to execute rcon command - retry count: {count}", _serverId, retryCount); })
                .Execute(() => GetCommandPackets("map_rotate"));

            return Task.FromResult(GetStringFromPackets(packets));
        }

        public Task<string> KickPlayer(int clientId)
        {
            _logger.LogDebug("[{GameServerId}] Attempting to kick client ID {ClientId} from the server", _serverId, clientId);

            var packets = Policy.Handle<Exception>()
                .WaitAndRetry(GetRetryTimeSpans(), (result, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning("[{ServerName}] Failed to execute kick command - retry count: {Count}", _serverId, retryCount);
                })
                .Execute(() => GetCommandPackets($"clientkick {clientId}"));

            return Task.FromResult(GetStringFromPackets(packets));
        }

        public Task<string> BanPlayer(int clientId)
        {
            _logger.LogDebug("[{GameServerId}] Attempting to ban client ID {ClientId} from the server", _serverId, clientId);

            var packets = Policy.Handle<Exception>()
                .WaitAndRetry(GetRetryTimeSpans(), (result, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning("[{ServerName}] Failed to execute ban command - retry count: {Count}", _serverId, retryCount);
                })
                .Execute(() => GetCommandPackets($"banClient {clientId}"));

            return Task.FromResult(GetStringFromPackets(packets));
        }

        public Task<string> KickPlayerByName(string name)
        {
            _logger.LogDebug("[{GameServerId}] Attempting to kick player by name {Name} from the server", _serverId, name);

            var packets = Policy.Handle<Exception>()
                .WaitAndRetry(GetRetryTimeSpans(), (result, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning("[{ServerName}] Failed to execute kick command - retry count: {Count}", _serverId, retryCount);
                })
                .Execute(() => GetCommandPackets($"kick \"{name}\""));

            return Task.FromResult(GetStringFromPackets(packets));
        }

        public Task<string> KickAllPlayers()
        {
            _logger.LogDebug("[{GameServerId}] Attempting to kick all players from the server", _serverId);

            var packets = Policy.Handle<Exception>()
                .WaitAndRetry(GetRetryTimeSpans(), (result, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning("[{ServerName}] Failed to execute kickall command - retry count: {Count}", _serverId, retryCount);
                })
                .Execute(() => GetCommandPackets("kickall"));

            return Task.FromResult(GetStringFromPackets(packets));
        }

        public Task<string> BanPlayerByName(string name)
        {
            _logger.LogDebug("[{GameServerId}] Attempting to ban player by name {Name} from the server", _serverId, name);

            var packets = Policy.Handle<Exception>()
                .WaitAndRetry(GetRetryTimeSpans(), (result, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning("[{ServerName}] Failed to execute ban command - retry count: {Count}", _serverId, retryCount);
                })
                .Execute(() => GetCommandPackets($"banUser \"{name}\""));

            return Task.FromResult(GetStringFromPackets(packets));
        }

        public Task<string> TempBanPlayerByName(string name)
        {
            _logger.LogDebug("[{GameServerId}] Attempting to temporarily ban player by name {Name} from the server", _serverId, name);

            var packets = Policy.Handle<Exception>()
                .WaitAndRetry(GetRetryTimeSpans(), (result, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning("[{ServerName}] Failed to execute tempban command - retry count: {Count}", _serverId, retryCount);
                })
                .Execute(() => GetCommandPackets($"tempBanUser \"{name}\""));

            return Task.FromResult(GetStringFromPackets(packets));
        }

        public Task<string> TempBanPlayer(int clientId)
        {
            _logger.LogDebug("[{GameServerId}] Attempting to temporarily ban client ID {ClientId} from the server", _serverId, clientId);

            var packets = Policy.Handle<Exception>()
                .WaitAndRetry(GetRetryTimeSpans(), (result, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning("[{ServerName}] Failed to execute tempban command - retry count: {Count}", _serverId, retryCount);
                })
                .Execute(() => GetCommandPackets($"tempBanClient {clientId}"));

            return Task.FromResult(GetStringFromPackets(packets));
        }

        public Task<string> UnbanPlayer(string name)
        {
            _logger.LogDebug("[{GameServerId}] Attempting to unban player by name {Name} from the server", _serverId, name);

            var packets = Policy.Handle<Exception>()
                .WaitAndRetry(GetRetryTimeSpans(), (result, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning("[{ServerName}] Failed to execute unban command - retry count: {Count}", _serverId, retryCount);
                })
                .Execute(() => GetCommandPackets($"unbanuser \"{name}\""));

            return Task.FromResult(GetStringFromPackets(packets));
        }

        public Task<string> TellPlayer(int clientId, string message)
        {
            _logger.LogDebug("[{GameServerId}] Attempting to send message to client ID {ClientId}", _serverId, clientId);

            var packets = Policy.Handle<Exception>()
                .WaitAndRetry(GetRetryTimeSpans(), (result, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning("[{ServerName}] Failed to execute tell command - retry count: {Count}", _serverId, retryCount);
                })
                .Execute(() => GetCommandPackets($"tell {clientId} \"{message}\""));

            return Task.FromResult(GetStringFromPackets(packets));
        }

        public Task<string> ChangeMap(string mapName)
        {
            _logger.LogDebug("[{GameServerId}] Attempting to change map to {MapName}", _serverId, mapName);

            var packets = Policy.Handle<Exception>()
                .WaitAndRetry(GetRetryTimeSpans(), (result, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning("[{ServerName}] Failed to execute map change command - retry count: {Count}", _serverId, retryCount);
                })
                .Execute(() => GetCommandPackets($"map {mapName}"));

            return Task.FromResult(GetStringFromPackets(packets));
        }

        public Task<string> GetServerInfo()
        {
            _logger.LogDebug("[{GameServerId}] Attempting to get server info", _serverId);

            var packets = Policy.Handle<Exception>()
                .WaitAndRetry(GetRetryTimeSpans(), (result, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning("[{ServerName}] Failed to execute serverinfo command - retry count: {Count}", _serverId, retryCount);
                })
                .Execute(() => GetCommandPackets("serverinfo"));

            return Task.FromResult(GetStringFromPackets(packets));
        }

        public Task<string> GetSystemInfo()
        {
            _logger.LogDebug("[{GameServerId}] Attempting to get system info", _serverId);

            var packets = Policy.Handle<Exception>()
                .WaitAndRetry(GetRetryTimeSpans(), (result, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning("[{ServerName}] Failed to execute systeminfo command - retry count: {Count}", _serverId, retryCount);
                })
                .Execute(() => GetCommandPackets("systeminfo"));

            return Task.FromResult(GetStringFromPackets(packets));
        }

        public Task<string> GetCommandList()
        {
            _logger.LogDebug("[{GameServerId}] Attempting to get command list", _serverId);

            var packets = Policy.Handle<Exception>()
                .WaitAndRetry(GetRetryTimeSpans(), (result, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning("[{ServerName}] Failed to execute cmdlist command - retry count: {Count}", _serverId, retryCount);
                })
                .Execute(() => GetCommandPackets("cmdlist"));

            return Task.FromResult(GetStringFromPackets(packets));
        }

        private string PlayerStatus()
        {
            var packets = Policy.Handle<Exception>()
                .WaitAndRetry(GetRetryTimeSpans(), (result, timeSpan, retryCount, context) => { _logger.LogWarning("[{serverName}] Failed to execute rcon command - retry count: {count}", _serverId, retryCount); })
                .Execute(() => GetCommandPackets("status"));

            _logger.LogDebug("[{GameServerId}] Total status packets retrieved from server: {Count}", _serverId, packets.Count);

            return GetStringFromPackets(packets);
        }

        private string MapRotation()
        {
            var packets = Policy.Handle<Exception>()
                .WaitAndRetry(GetRetryTimeSpans(), (result, timeSpan, retryCount, context) => { _logger.LogWarning("[{serverName}] Failed to execute rcon command - retry count: {count}", _serverId, retryCount); })
                .Execute(() => GetCommandPackets("sv_mapRotation"));

            _logger.LogDebug("[{GameServerId}] Total status packets retrieved from server: {Count}", _serverId, packets.Count);

            return GetStringFromPackets(packets);
        }

        private string GetStringFromPackets(List<byte[]> packets)
        {
            var responseText = new StringBuilder();

            foreach (var packet in packets)
            {
                var text = Encoding.Default.GetString(packet);
                if (text.IndexOf("print", StringComparison.Ordinal) == 4) text = text.Substring(10);

                responseText.Append(text);
            }

            return responseText.ToString();
        }

        private static Regex GameTypeRegex(GameType gameType)
        {
            switch (gameType)
            {
                case GameType.CallOfDuty2:
                    return new Regex(
                        "^\\s*([0-9]+)\\s+([0-9-]+)\\s+([0-9]+)\\s+([0-9]+)\\s+(.*?)\\s+([0-9]+?)\\s*((?:(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])\\.){3}(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])):?(-?[0-9]{1,5})\\s*(-?[0-9]{1,5})\\s+([0-9]+)$");
                case GameType.CallOfDuty4:
                    return new Regex(
                        "^\\s*([0-9]+)\\s+([0-9-]+)\\s+([0-9]+)\\s+([0-9a-f]{32})\\s+(.*?)\\s+([0-9]+?)\\s*((?:(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])\\.){3}(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])):?(-?[0-9]{1,5})\\s*(-?[0-9]{1,5})\\s+([0-9]+)$");
                case GameType.CallOfDuty5:
                    return new Regex(
                        "^\\s*([0-9]+)\\s+([0-9-]+)\\s+([0-9]+)\\s+([0-9]+)\\s+(.*?)\\s+([0-9]+?)\\s*((?:(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])\\.){3}(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])):?(-?[0-9]{1,5})\\s*(-?[0-9]{1,5})\\s+([0-9]+)$");
                default:
                    throw new Exception("Unsupported game type");
            }
        }

        private static byte[] ExecuteCommandPacket(string rconPassword, string command)
        {
            //ÿÿÿÿrcon {rconPassword} {command}
            var prefix = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };
            var commandText = $"rcon {rconPassword} {command}";
            var commandBytes = Encoding.Default.GetBytes(commandText);

            return prefix.Concat(commandBytes).ToArray();
        }

        private List<byte[]> GetCommandPackets(string command, bool skipReceive = false)
        {
            UdpClient udpClient = null;

            try
            {
                var commandBytes = ExecuteCommandPacket(_rconPassword, command);
                var remoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);

                udpClient = new UdpClient() { Client = { SendTimeout = 5000, ReceiveTimeout = 5000 } };
                udpClient.Connect(_hostname, _queryPort);
                udpClient.Send(commandBytes, commandBytes.Length);

                var datagrams = new List<byte[]>();
                if (!skipReceive)
                {
                    do
                    {
                        var datagramBytes = udpClient.Receive(ref remoteIpEndPoint);
                        datagrams.Add(datagramBytes);

                        if (udpClient.Available == 0)
                            Thread.Sleep(500);
                    } while (udpClient.Available > 0);
                }

                return datagrams;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{serverName}] Failed to execute rcon command", _serverId);
                throw;
            }
            finally
            {
                udpClient?.Dispose();
            }
        }

        private static IEnumerable<TimeSpan> GetRetryTimeSpans()
        {
            var random = new Random();

            return new[]
            {
                TimeSpan.FromSeconds(random.Next(1)),
                TimeSpan.FromSeconds(random.Next(3)),
                TimeSpan.FromSeconds(random.Next(5))
            };
        }
    }
}