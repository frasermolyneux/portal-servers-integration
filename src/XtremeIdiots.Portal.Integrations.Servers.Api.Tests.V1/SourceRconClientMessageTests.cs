using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using XtremeIdiots.Portal.Integrations.Servers.Api.Models.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Clients;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1;

[Trait("Category", "Unit")]
public class SourceRconClientMessageTests
{
    [Theory]
    [InlineData("cancel")]
    [InlineData("closed")]
    [InlineData("success")]
    public async Task Say_HandlesResponsesAndInterruptionWithoutResending(string scenario)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var cancellation = new CancellationTokenSource();
            var client = new SourceRconClient(Mock.Of<ILogger>());
            client.Configure(GameType.Insurgency, Guid.NewGuid(), "127.0.0.1",
                ((IPEndPoint)listener.LocalEndpoint).Port, "test");

            var dispatch = Task.Run(() => client.Say(" hello ", cancellation.Token));
            using var connection = await listener.AcceptTcpClientAsync(timeout.Token);
            await using var stream = connection.GetStream();
            await ReadPacket(stream, timeout.Token);
            byte[] authentication = [.. new SourceRconPacket(1, 0, "").PacketBytes, .. new SourceRconPacket(1, 2, "").PacketBytes];
            await stream.WriteAsync(authentication.AsMemory(0, 12), timeout.Token);
            await Task.Delay(25, timeout.Token);
            await stream.WriteAsync(authentication.AsMemory(12), timeout.Token);
            var command = await ReadPacket(stream, timeout.Token);
            var terminator = await ReadPacket(stream, timeout.Token);
            Assert.Contains("say \" hello \"", Encoding.ASCII.GetString(command));

            if (scenario == "cancel")
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dispatch.WaitAsync(TimeSpan.FromSeconds(3)));
                Assert.Equal(0, await stream.ReadAsync(new byte[1], timeout.Token));
            }
            else if (scenario == "closed")
            {
                connection.Close();
                await Assert.ThrowsAsync<IOException>(() => dispatch.WaitAsync(TimeSpan.FromSeconds(3)));
            }
            else
            {
                byte[] response = [
                    .. new SourceRconPacket(BitConverter.ToInt32(command), 0, "OK").PacketBytes,
                    .. new SourceRconPacket(BitConverter.ToInt32(terminator), 0, "").PacketBytes
                ];
                await stream.WriteAsync(response, timeout.Token);
                await dispatch.WaitAsync(TimeSpan.FromSeconds(3));
            }
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task<byte[]> ReadPacket(NetworkStream stream, CancellationToken cancellationToken)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, cancellationToken);
        var body = new byte[BitConverter.ToInt32(header)];
        await stream.ReadExactlyAsync(body, cancellationToken);
        return body;
    }
}
