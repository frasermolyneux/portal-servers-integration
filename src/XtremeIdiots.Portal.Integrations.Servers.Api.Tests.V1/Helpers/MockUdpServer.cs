using System.Net;
using System.Net.Sockets;
using System.Text;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Helpers
{
    /// <summary>
    /// A mock UDP server for testing RCON commands
    /// </summary>
    public class MockUdpServer : IDisposable
    {
        private readonly UdpClient _udpClient;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private Task? _listenTask;
        private readonly Dictionary<string, Func<string, byte[]>> _commandHandlers;
        
        public int Port { get; }

        public MockUdpServer(int port = 0)
        {
            _udpClient = new UdpClient(port);
            Port = ((IPEndPoint)_udpClient.Client.LocalEndPoint!).Port;
            _cancellationTokenSource = new CancellationTokenSource();
            _commandHandlers = [];
        }

        /// <summary>
        /// Registers a handler for a specific RCON command
        /// </summary>
        public void RegisterCommandHandler(string command, Func<string, byte[]> handler)
        {
            _commandHandlers[command] = handler;
        }

        /// <summary>
        /// Starts listening for incoming UDP packets
        /// </summary>
        public void Start()
        {
            _listenTask = Task.Run(() => ListenAsync(_cancellationTokenSource.Token));
        }

        private async Task ListenAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var result = await _udpClient.ReceiveAsync();
                    if (cancellationToken.IsCancellationRequested) break;
                    
                    var receivedData = result.Buffer;
                    var remoteEndPoint = result.RemoteEndPoint;

                    // Parse the RCON command
                    // Format: ÿÿÿÿrcon {password} {command}
                    var dataString = Encoding.Default.GetString(receivedData);
                    
                    // Check for RCON prefix (4 bytes of 0xFF)
                    if (receivedData.Length > 4 && 
                        receivedData[0] == 0xFF && receivedData[1] == 0xFF && 
                        receivedData[2] == 0xFF && receivedData[3] == 0xFF)
                    {
                        var commandText = dataString.Substring(4);
                        
                        // Extract the actual command (skip "rcon password ")
                        var parts = commandText.Split(' ', 3);
                        if (parts.Length >= 3 && parts[0] == "rcon")
                        {
                            var command = parts[2];
                            
                            // Find matching handler
                            foreach (var kvp in _commandHandlers)
                            {
                                if (command.StartsWith(kvp.Key))
                                {
                                    var response = kvp.Value(command);
                                    await _udpClient.SendAsync(response, response.Length, remoteEndPoint);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
            catch (Exception)
            {
                // Ignore exceptions during shutdown
            }
        }

        /// <summary>
        /// Creates a standard Quake3 RCON response packet
        /// </summary>
        public static byte[] CreateQuake3Response(string content)
        {
            byte[] prefix = [0xFF, 0xFF, 0xFF, 0xFF];
            var printCommand = Encoding.Default.GetBytes("print\n");
            var contentBytes = Encoding.Default.GetBytes(content);
            
            return prefix.Concat(printCommand).Concat(contentBytes).ToArray();
        }

        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _listenTask?.Wait(TimeSpan.FromMilliseconds(500));
            _udpClient?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
}
