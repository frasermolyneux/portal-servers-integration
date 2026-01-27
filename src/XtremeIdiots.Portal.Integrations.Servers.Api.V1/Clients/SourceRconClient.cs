using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.Models.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Clients
{
    public class SourceRconClient : IRconClient
    {
        private readonly ILogger _logger;

        private readonly Regex _playerRegex =
            new Regex(
                "^\\#\\s([0-9]+)\\s([0-9]+)\\s\\\"(.+)\\\"\\s([STEAM0-9:_]+)\\s+([0-9:]+)\\s([0-9]+)\\s([0-9]+)\\s([a-z]+)\\s([0-9]+)\\s((?:(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])\\.){3}(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])):?(-?[0-9]{1,5})");

        // ReSharper disable once NotAccessedField.Local
        private GameType _gameType;
        private string _hostname;
        private int _queryPort;
        private string _rconPassword;

        private int _sequenceId = 1;

        private Guid _serverId;
        private TcpClient _tcpClient;

        public SourceRconClient(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Configure(GameType gameType, Guid gameServerId, string hostname, int queryPort, string rconPassword)
        {
            _logger.LogDebug("[{GameServerId}] Configuring Source rcon client for {GameType} with endpoint {Hostname}:{QueryPort}", gameServerId, gameType, hostname, queryPort);

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
                var match = _playerRegex.Match(line);

                if (!match.Success)
                    continue;

                var num = match.Groups[1].ToString();
                var name = match.Groups[3].ToString();
                var guid = match.Groups[4].ToString();
                var ping = match.Groups[6].ToString();
                var rate = match.Groups[9].ToString();
                var ipAddress = match.Groups[10].ToString();

                int.TryParse(num, out int numInt);
                int.TryParse(ping, out int pingInt);
                int.TryParse(rate, out int rateInt);

                _logger.LogDebug("[{GameServerId}] Player {Name} with {Guid} and {IpAddress} parsed from result", _serverId, name, guid, ipAddress);

                players.Add(new SourceRconPlayer
                {
                    Num = numInt,
                    Ping = pingInt,
                    Guid = guid,
                    Name = name,
                    IpAddress = ipAddress,
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
                var playerStatus = PlayerStatus();
                
                // Parse the status output to extract the map
                // Status format includes: "map     :  de_dust2 at: 0 x, 0 y, 0 z"
                var lines = playerStatus.Split('\n');
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (trimmedLine.StartsWith("map"))
                    {
                        // Extract map name from format: "map     :  de_dust2 at: 0 x, 0 y, 0 z"
                        var mapMatch = Regex.Match(trimmedLine, @"map\s*:\s*(\S+)");
                        if (mapMatch.Success)
                        {
                            var mapName = mapMatch.Groups[1].Value;
                            _logger.LogDebug("[{GameServerId}] Current map is {MapName}", _serverId, mapName);
                            return mapName;
                        }
                    }
                }

                _logger.LogWarning("[{GameServerId}] Map name not found in status output", _serverId);
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
            return Task.CompletedTask;
        }

        public Task<List<Quake3QueryMap>> GetMaps()
        {
            throw new NotImplementedException();
        }

        public Task<string> Restart()
        {
            return Task.FromResult("Not Implemented");
        }

        public Task<string> RestartMap()
        {
            return Task.FromResult("Not Implemented");
        }

        public Task<string> FastRestartMap()
        {
            return Task.FromResult("Not Implemented");
        }

        public Task<string> NextMap()
        {
            return Task.FromResult("Not Implemented");
        }

        public Task<string> KickPlayer(int clientId)
        {
            throw new NotImplementedException("Kicking players is not implemented for Source engine games");
        }

        public Task<string> BanPlayer(int clientId)
        {
            throw new NotImplementedException("Banning players is not implemented for Source engine games");
        }

        public Task<string> KickPlayerByName(string name)
        {
            throw new NotImplementedException("Kicking players by name is not implemented for Source engine games");
        }

        public Task<string> KickAllPlayers()
        {
            throw new NotImplementedException("Kicking all players is not implemented for Source engine games");
        }

        public Task<string> BanPlayerByName(string name)
        {
            throw new NotImplementedException("Banning players by name is not implemented for Source engine games");
        }

        public Task<string> TempBanPlayerByName(string name)
        {
            throw new NotImplementedException("Temporarily banning players by name is not implemented for Source engine games");
        }

        public Task<string> TempBanPlayer(int clientId)
        {
            throw new NotImplementedException("Temporarily banning players is not implemented for Source engine games");
        }

        public Task<string> UnbanPlayer(string name)
        {
            throw new NotImplementedException("Unbanning players is not implemented for Source engine games");
        }

        public Task<string> TellPlayer(int clientId, string message)
        {
            throw new NotImplementedException("Sending messages to specific players is not implemented for Source engine games");
        }

        public Task<string> ChangeMap(string mapName)
        {
            throw new NotImplementedException("Changing maps is not implemented for Source engine games");
        }

        public Task<string> GetServerInfo()
        {
            throw new NotImplementedException("Getting server info is not implemented for Source engine games");
        }

        public Task<string> GetSystemInfo()
        {
            throw new NotImplementedException("Getting system info is not implemented for Source engine games");
        }

        public Task<string> GetCommandList()
        {
            throw new NotImplementedException("Getting command list is not implemented for Source engine games");
        }

        private string PlayerStatus()
        {
            CreateConnection();

            var statusPackets = GetCommandPackets("status");

            _logger.LogDebug("[{GameServerId}] Total status packets retrieved from server: {Count}", _serverId, statusPackets.Count);

            var response = new StringBuilder();
            foreach (var packet in statusPackets) response.Append(packet.Body.Trim());

            return response.ToString();
        }

        private SourceRconPacket AuthPacket(string rconPassword)
        {
            return new SourceRconPacket(_sequenceId++, 3, rconPassword);
        }

        private SourceRconPacket ExecuteCommandPacket(string command)
        {
            return new SourceRconPacket(_sequenceId++, 2, command);
        }

        private SourceRconPacket EmptyResponsePacket()
        {
            return new SourceRconPacket(_sequenceId++, 0, string.Empty);
        }

        private void CreateConnection()
        {
            try
            {
                if (_tcpClient != null && _tcpClient.Connected)
                    return;

                _logger.LogDebug("[{GameServerId}] Creating a new TcpClient and attempting to authenticate", _serverId);

                _tcpClient = new TcpClient(_hostname, _queryPort) { ReceiveTimeout = 5000 };

                var authPackets = GetAuthPackets(_rconPassword);
                var authResultPacket = authPackets.SingleOrDefault(packet => packet.Type == 2);

                _logger.LogDebug("[{GameServerId}] Total auth packets retrieved from server: {Count}", _serverId, authPackets.Count());

                if (authResultPacket == null)
                {
                    _logger.LogError("[{GameServerId}] Could not establish authenticated session with server", _serverId);
                    throw new Exception("Could not establish authenticated session with server");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{GameServerId}] Could not establish TCP connection to server", _serverId);
                throw;
            }
        }

        private List<SourceRconPacket> GetAuthPackets(string rconPassword)
        {
            var endpoint = _tcpClient.Client.RemoteEndPoint;

            var authPacket = AuthPacket(rconPassword);
            _tcpClient.Client.Send(authPacket.PacketBytes);

            var responsePackets = new List<SourceRconPacket>();
            byte[] leftoverBytes = null;
            do
            {
                var tempBuffer = new byte[8192];
                var bytesRead = _tcpClient.Client.ReceiveFrom(tempBuffer, SocketFlags.None, ref endpoint);

                var bytesToProcess = tempBuffer.Take(bytesRead).ToArray();

                if (leftoverBytes != null) bytesToProcess = leftoverBytes.Concat(bytesToProcess).ToArray();

                var (packets, leftover) = BytesIntoPackets(bytesToProcess);
                responsePackets.AddRange(packets);

                leftoverBytes = leftover;
            } while (responsePackets.Count != 2);

            return responsePackets;
        }

        private List<SourceRconPacket> GetCommandPackets(string command)
        {
            var endpoint = _tcpClient.Client.RemoteEndPoint;

            var executeCommandPacket = ExecuteCommandPacket(command);
            _tcpClient.Client.Send(executeCommandPacket.PacketBytes);
            var emptyResponsePacket = EmptyResponsePacket();
            _tcpClient.Client.Send(emptyResponsePacket.PacketBytes);

            var responsePackets = new List<SourceRconPacket>();
            byte[] leftoverBytes = null;
            do
            {
                var tempBuffer = new byte[8192];
                var bytesRead = _tcpClient.Client.ReceiveFrom(tempBuffer, SocketFlags.None, ref endpoint);

                var bytesToProcess = tempBuffer.Take(bytesRead).ToArray();

                if (leftoverBytes != null) bytesToProcess = leftoverBytes.Concat(bytesToProcess).ToArray();

                var (packets, leftover) = BytesIntoPackets(bytesToProcess);
                responsePackets.AddRange(packets);

                leftoverBytes = leftover;
            } while (responsePackets.All(packet => packet.Id != emptyResponsePacket.Id));

            return responsePackets.Where(packet => packet.Id == executeCommandPacket.Id).ToList();
        }

        private static Tuple<List<SourceRconPacket>, byte[]> BytesIntoPackets(byte[] bytes)
        {
            var packets = new List<SourceRconPacket>();
            var offset = 0;

            try
            {
                do
                {
                    if (offset + 4 > bytes.Length)
                        break;

                    var size = BitConverter.ToInt32(bytes, offset);

                    if (size == 0)
                        break;

                    if (offset + size > bytes.Length)
                        break;

                    var id = BitConverter.ToInt32(bytes, offset + 4);
                    var type = BitConverter.ToInt32(bytes, offset + 8);
                    var body = Encoding.ASCII.GetString(bytes.Skip(offset + 12).Take(size - 6).ToArray()).Trim();

                    offset += 4 + size;

                    var packet = new SourceRconPacket(id, type, body);
                    packets.Add(packet);
                } while (true);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            var leftover = offset == bytes.Length ? null : bytes.Skip(offset).Take(bytes.Length - offset).ToArray();
            return new Tuple<List<SourceRconPacket>, byte[]>(packets, leftover);
        }
    }
}