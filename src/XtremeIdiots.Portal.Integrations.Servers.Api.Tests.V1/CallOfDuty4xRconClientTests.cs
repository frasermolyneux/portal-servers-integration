using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Moq;
using XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Helpers;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Clients;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1
{
    [Trait("Category", "Unit")]
    public class CallOfDuty4xRconClientTests
    {
        private static readonly Guid TestServerId = Guid.NewGuid();
        private const string TestRconPassword = "testpass";

        [Theory]
        [InlineData("screensay")]
        [InlineData("consay")]
        [InlineData("tell")]
        [InlineData("screentell")]
        [InlineData("contell")]
        public async Task MessageCommand_DispatchesOnceWithoutRequiringAResponse(string command)
        {
            using var server = new UdpClient(0);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var client = new CallOfDuty4xRconClient(Mock.Of<ILogger>());
            client.Configure(GameType.CallOfDuty4x, TestServerId, "127.0.0.1",
                ((IPEndPoint)server.Client.LocalEndPoint!).Port, TestRconPassword);

            var dispatch = Task.Run(() => command switch
            {
                "screensay" => client.ScreenSay("hello"),
                "consay" => client.ConSay("hello"),
                "tell" => client.Tell("5", "hello"),
                "screentell" => client.ScreenTell("5", "hello"),
                _ => client.ConTell("5", "hello")
            });
            var packet = await server.ReceiveAsync(timeout.Token);
            var result = await dispatch.WaitAsync(timeout.Token);

            Assert.Contains(command, Encoding.ASCII.GetString(packet.Buffer));
            Assert.Contains("delivery unconfirmed", result);
            Assert.Equal(0, server.Available);
        }

        [Fact]
        public async Task PortalPluginHealth_ReturnsRconOutput()
        {
            using var server = new MockUdpServer();
            var client = CreateClient(server);
            server.RegisterCommandHandler("portalpluginhealth", _ =>
                MockUdpServer.CreateQuake3Response("rulesProtocolVersion=1 nativeCapabilities=cod4x.player.drop"));
            server.Start();

            var response = await client.PortalPluginHealth(CancellationToken.None);

            Assert.Contains("rulesProtocolVersion=1 nativeCapabilities=cod4x.player.drop", response);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task PortalPluginHealth_CancellationDoesNotRetry(bool cancelBeforeSend)
        {
            using var server = new MockUdpServer();
            using var cancellation = new CancellationTokenSource();
            var logger = new Mock<ILogger<CallOfDuty4xRconClient>>();
            var client = new CallOfDuty4xRconClient(logger.Object);
            client.Configure(GameType.CallOfDuty4x, TestServerId, "127.0.0.1", server.Port, TestRconPassword);
            var received = 0;
            server.RegisterCommandHandler("portalpluginhealth", _ =>
            {
                Interlocked.Increment(ref received);
                cancellation.Cancel();
                return MockUdpServer.CreateQuake3Response("ok");
            });
            server.Start();
            if (cancelBeforeSend)
            {
                cancellation.Cancel();
            }

            var action = Task.Run(() => client.PortalPluginHealth(cancellation.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => action.WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.Equal(cancelBeforeSend ? 0 : 1, Volatile.Read(ref received));
            logger.Verify(x => x.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);
        }

        [Theory]
        [InlineData("set")]
        [InlineData("seta")]
        [InlineData("sets")]
        [InlineData("setu")]
        public async Task AacpMapList_RemainsOneQuotedDvarValue(string command)
        {
            using var server = new MockUdpServer();
            var client = CreateClient(server);
            var received = RegisterExactCommandHandler(server, $"{command} scr_aacp_maps_1 \"mp_bog;mp_custom\"");
            server.Start();

            await (command switch
            {
                "set" => client.Set("scr_aacp_maps_1", "mp_bog;mp_custom"),
                "seta" => client.Seta("scr_aacp_maps_1", "mp_bog;mp_custom"),
                "sets" => client.Sets("scr_aacp_maps_1", "mp_bog;mp_custom"),
                _ => client.Setu("scr_aacp_maps_1", "mp_bog;mp_custom")
            });

            Assert.True(received());
        }

        [Theory]
        [InlineData("scr_aacp_maps_1", "mp_bog;mp_custom\nquit")]
        [InlineData("scr_aacp_maps_1", "mp_bog;mp_custom\rquit")]
        [InlineData("scr_aacp_maps_1", "mp_bog\";quit")]
        [InlineData("scr_aacp_maps_1", "mp_bog;mp_custom map_restart")]
        [InlineData("sv_hostname", "mp_bog;mp_custom")]
        public async Task AacpMapList_DoesNotRelaxOtherCommandValidation(string variable, string value)
        {
            using var server = new MockUdpServer();
            var client = CreateClient(server);

            await Assert.ThrowsAsync<ArgumentException>(() => client.Set(variable, value));
        }

        [Theory]
        [InlineData("set")]
        [InlineData("seta")]
        [InlineData("sets")]
        [InlineData("setu")]
        public async Task ClearRotation_EmitsExplicitEmptyQuotedValue(string command)
        {
            using var server = new MockUdpServer();
            var client = CreateClient(server);
            var received = RegisterExactCommandHandler(server, $"{command} sv_maprotation \"\"");
            server.Start();

            await (command switch
            {
                "set" => client.Set("sv_maprotation", ""),
                "seta" => client.Seta("sv_maprotation", ""),
                "sets" => client.Sets("sv_maprotation", ""),
                _ => client.Setu("sv_maprotation", "")
            });

            Assert.True(received());
        }

        [Fact]
        public async Task PermBan_SendsExpectedCommand()
        {
            using var mockServer = new MockUdpServer();
            var client = CreateClient(mockServer);

            const string expectedCommand = "permban 2310346615957836592 \"cheating\"";
            var exactMatch = RegisterExactCommandHandler(mockServer, expectedCommand);

            mockServer.Start();
            await Task.Delay(100);

            await client.PermBan("2310346615957836592", "cheating");

            await Task.Delay(200);
            Assert.True(exactMatch());
        }

        [Fact]
        public async Task BanUser_QuotesTargetWithWhitespace()
        {
            using var mockServer = new MockUdpServer();
            var client = CreateClient(mockServer);

            const string expectedCommand = "banUser \"Test Player\" \"aimbot\"";
            var exactMatch = RegisterExactCommandHandler(mockServer, expectedCommand);

            mockServer.Start();
            await Task.Delay(100);

            await client.BanUser("Test Player", "aimbot");

            await Task.Delay(200);
            Assert.True(exactMatch());
        }

        [Fact]
        public async Task BanUser_NormalizesPreQuotedTarget()
        {
            using var mockServer = new MockUdpServer();
            var client = CreateClient(mockServer);

            const string expectedCommand = "banUser \"Test Player\" \"aimbot\"";
            var exactMatch = RegisterExactCommandHandler(mockServer, expectedCommand);

            mockServer.Start();
            await Task.Delay(100);

            await client.BanUser("\"Test Player\"", "aimbot");

            await Task.Delay(200);
            Assert.True(exactMatch());
        }

        [Fact]
        public async Task ClientKick_SendsExpectedCommand()
        {
            using var mockServer = new MockUdpServer();
            var client = CreateClient(mockServer);

            const string expectedCommand = "clientKick 12 \"afk\"";
            var exactMatch = RegisterExactCommandHandler(mockServer, expectedCommand);

            mockServer.Start();
            await Task.Delay(100);

            await client.ClientKick(12, "afk");

            await Task.Delay(200);
            Assert.True(exactMatch());
        }

        [Fact]
        public async Task TempBan_SendsExpectedCommand()
        {
            using var mockServer = new MockUdpServer();
            var client = CreateClient(mockServer);

            const string expectedCommand = "tempban 2310346615957836592 15m \"toxicity\"";
            var exactMatch = RegisterExactCommandHandler(mockServer, expectedCommand);

            mockServer.Start();
            await Task.Delay(100);

            await client.TempBan("2310346615957836592", 15, "toxicity");

            await Task.Delay(200);
            Assert.True(exactMatch());
        }

        [Fact]
        public async Task BanPlayerByPlayerIdentifier_SendsExpectedCommand()
        {
            using var mockServer = new MockUdpServer();
            var client = CreateClient(mockServer);

            const string expectedCommand = "permban 2310346615957836592 \"sync-ban\"";
            var exactMatch = RegisterExactCommandHandler(mockServer, expectedCommand);

            mockServer.Start();
            await Task.Delay(100);

            await client.BanPlayerByPlayerIdentifier("2310346615957836592", "sync-ban");

            await Task.Delay(200);
            Assert.True(exactMatch());
        }

        [Fact]
        public async Task TempBanPlayerByPlayerIdentifier_SendsExpectedCommand()
        {
            using var mockServer = new MockUdpServer();
            var client = CreateClient(mockServer);

            const string expectedCommand = "tempban 2310346615957836592 30m \"sync-ban\"";
            var exactMatch = RegisterExactCommandHandler(mockServer, expectedCommand);

            mockServer.Start();
            await Task.Delay(100);

            await client.TempBanPlayerByPlayerIdentifier("2310346615957836592", 30, "sync-ban");

            await Task.Delay(200);
            Assert.True(exactMatch());
        }

        [Fact]
        public async Task UnbanPlayerByPlayerIdentifier_SendsExpectedCommand()
        {
            using var mockServer = new MockUdpServer();
            var client = CreateClient(mockServer);

            const string expectedCommand = "unban 2310346615957836592";
            var exactMatch = RegisterExactCommandHandler(mockServer, expectedCommand);

            mockServer.Start();
            await Task.Delay(100);

            await client.UnbanPlayerByPlayerIdentifier("2310346615957836592");

            await Task.Delay(200);
            Assert.True(exactMatch());
        }

        [Fact]
        public async Task TakeScreenshot_SendsExpectedCommand()
        {
            using var mockServer = new MockUdpServer();
            var client = CreateClient(mockServer);

            const string expectedCommand = "getss 2310346615957836592";
            var exactMatch = RegisterExactCommandHandler(mockServer, expectedCommand);

            mockServer.Start();
            await Task.Delay(100);

            await client.TakeScreenshot("2310346615957836592");

            await Task.Delay(200);
            Assert.True(exactMatch());
        }

        [Fact]
        public async Task ScreenTell_SendsExpectedCommand()
        {
            using var mockServer = new MockUdpServer();
            var client = CreateClient(mockServer);

            const string expectedCommand = "screentell 5 \"watch language\"";
            var exactMatch = RegisterExactCommandHandler(mockServer, expectedCommand);

            mockServer.Start();
            await Task.Delay(100);

            await client.ScreenTell("5", "watch language");

            await Task.Delay(200);
            Assert.True(exactMatch());
        }

        [Fact]
        public async Task Record_WithDemoName_SendsExpectedCommand()
        {
            using var mockServer = new MockUdpServer();
            var client = CreateClient(mockServer);

            const string expectedCommand = "record 7 \"round_01\"";
            var exactMatch = RegisterExactCommandHandler(mockServer, expectedCommand);

            mockServer.Start();
            await Task.Delay(100);

            await client.Record("7", "round_01");

            await Task.Delay(200);
            Assert.True(exactMatch());
        }

        [Fact]
        public async Task AdminChangeCommandPower_SendsExpectedCommand()
        {
            using var mockServer = new MockUdpServer();
            var client = CreateClient(mockServer);

            const string expectedCommand = "AdminChangeCommandPower kick 40";
            var exactMatch = RegisterExactCommandHandler(mockServer, expectedCommand);

            mockServer.Start();
            await Task.Delay(100);

            await client.AdminChangeCommandPower("kick", 40);

            await Task.Delay(200);
            Assert.True(exactMatch());
        }

        [Fact]
        public async Task Seta_SendsExpectedCommand()
        {
            using var mockServer = new MockUdpServer();
            var client = CreateClient(mockServer);

            const string expectedCommand = "seta g_password \"secret pass\"";
            var exactMatch = RegisterExactCommandHandler(mockServer, expectedCommand);

            mockServer.Start();
            await Task.Delay(100);

            await client.Seta("g_password", "secret pass");

            await Task.Delay(200);
            Assert.True(exactMatch());
        }

        [Fact]
        public async Task LoadPlugin_SendsExpectedCommand()
        {
            using var mockServer = new MockUdpServer();
            var client = CreateClient(mockServer);

            const string expectedCommand = "loadPlugin xtreme-admins";
            var exactMatch = RegisterExactCommandHandler(mockServer, expectedCommand);

            mockServer.Start();
            await Task.Delay(100);

            await client.LoadPlugin("xtreme-admins");

            await Task.Delay(200);
            Assert.True(exactMatch());
        }

        [Fact]
        public async Task MapRotate_SendsExpectedCommand()
        {
            using var mockServer = new MockUdpServer();
            var client = CreateClient(mockServer);

            const string expectedCommand = "map_rotate";
            var exactMatch = RegisterExactCommandHandler(mockServer, expectedCommand);

            mockServer.Start();
            await Task.Delay(100);

            await client.MapRotate();

            await Task.Delay(200);
            Assert.True(exactMatch());
        }

        [Fact]
        public async Task PluginInfo_SendsExpectedCommand()
        {
            using var mockServer = new MockUdpServer();
            var client = CreateClient(mockServer);

            const string expectedCommand = "pluginInfo xtreme-admins";
            var exactMatch = RegisterExactCommandHandler(mockServer, expectedCommand);

            mockServer.Start();
            await Task.Delay(100);

            await client.PluginInfo("xtreme-admins");

            await Task.Delay(200);
            Assert.True(exactMatch());
        }

        private static CallOfDuty4xRconClient CreateClient(MockUdpServer mockServer)
        {
            var logger = new Mock<ILogger<CallOfDuty4xRconClient>>();
            var client = new CallOfDuty4xRconClient(logger.Object);

            client.Configure(
                GameType.CallOfDuty4x,
                TestServerId,
                "127.0.0.1",
                mockServer.Port,
                TestRconPassword);

            return client;
        }

        private static Func<bool> RegisterExactCommandHandler(MockUdpServer mockServer, string expectedCommand)
        {
            var commandReceived = false;
            var exactMatch = false;

            mockServer.RegisterCommandHandler(expectedCommand, command =>
            {
                commandReceived = true;
                exactMatch = string.Equals(command, expectedCommand, StringComparison.Ordinal);
                return MockUdpServer.CreateQuake3Response("ok");
            });

            return () => commandReceived && exactMatch;
        }
    }
}
