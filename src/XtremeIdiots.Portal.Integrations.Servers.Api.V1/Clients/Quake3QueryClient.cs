using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.Models.V1;

// ReSharper disable StringLiteralTypo

namespace XtremeIdiots.Portal.Integrations.Servers.Api.V1.Clients;

public partial class Quake3QueryClient(ILogger logger) : IQueryClient
{
    [GeneratedRegex("(?<score>.+) (?<ping>.+) \\\"(?<name>.+)\\\"")]
    private static partial Regex PlayerRegexPattern();

    private readonly ILogger _logger = logger;

    private string? Hostname { get; set; }
    private int QueryPort { get; set; }

    public void Configure(string hostname, int queryPort)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);

        if (queryPort == 0)
            throw new ArgumentNullException(nameof(queryPort));

        Hostname = hostname;
        QueryPort = queryPort;
    }

    public Task<IQueryResponse> GetServerStatus()
    {
        var queryResult = Query(GetStatusPacket());

        var lines = queryResult[3..].Split('\n');
        if (lines.Length < 2) return null;

        var serverParams = GetParams(lines[1].Split('\\'));

        if (lines.Length <= 2)
            return Task.FromResult((IQueryResponse)new Quake3QueryResponse(serverParams, new List<IQueryPlayer>()));

        var players = new List<IQueryPlayer>();
        for (var i = 2; i < lines.Length; i++)
        {
            if (lines[i].Length == 0) continue;
            players.Add(ParsePlayer(lines[i]));
        }

        return Task.FromResult((IQueryResponse)new Quake3QueryResponse(serverParams, players));
    }

    private static byte[] GetStatusPacket()
    {
        //ÿÿÿÿgetstatus
        return [0xFF, 0xFF, 0xFF, 0xFF, 0x67, 0x65, 0x74, 0x73, 0x74, 0x61, 0x74, 0x75, 0x73];
    }

    private static IQueryPlayer ParsePlayer(string playerInfo)
    {
        var regPattern = PlayerRegexPattern();
        var regMatch = regPattern.Match(playerInfo);

        var player = new Quake3QueryPlayer
        {
            Name = regMatch.Groups["name"].Value,
            Score = int.Parse(regMatch.Groups["score"].Value),
            Ping = int.Parse(regMatch.Groups["ping"].Value)
        };

        return player;
    }

    private static Dictionary<string, string> GetParams(IReadOnlyList<string> parts)
    {
        var serverParams = new Dictionary<string, string>();

        for (var i = 0; i < parts.Count; i++)
        {
            if (parts[i].Length == 0) continue;
            var key = parts[i++];
            var val = parts[i];

            if (key == "final") break;
            if (key == "querid") continue;

            serverParams[key] = val;
        }

        return serverParams;
    }

    private string Query(byte[] commandBytes)
    {
        var commandAsString = Encoding.UTF8.GetString(commandBytes);
        _logger.LogInformation("Executing {command} command against {hostname}:{port}", commandAsString, Hostname, QueryPort);

        UdpClient? udpClient = null;

        try
        {
            var remoteIpEndPoint = new IPEndPoint(IPAddress.Any, 0);

            udpClient = new UdpClient() { Client = { SendTimeout = 5000, ReceiveTimeout = 5000 } };
            udpClient.Connect(Hostname, QueryPort);
            udpClient.Send(commandBytes, commandBytes.Length);

            var datagrams = new List<string>();
            do
            {
                var datagramBytes = udpClient.Receive(ref remoteIpEndPoint);
                var datagramText = Encoding.Default.GetString(datagramBytes);

                datagrams.Add(datagramText);

                if (udpClient.Available == 0)
                    Task.Delay(500).Wait();
            } while (udpClient.Available > 0);

            var responseText = new StringBuilder();

            foreach (var datagram in datagrams)
            {
                var text = datagram;
                if (text.Length > 4 && text.AsSpan(4, 5).SequenceEqual("print")) text = text[10..];

                responseText.Append(text);
            }

            return responseText.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute {command} against {hostname}:{port}", commandAsString, Hostname, QueryPort);
            throw;
        }
        finally
        {
            udpClient?.Dispose();
        }
    }
}