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

            const string expectedCommand = "tempban 2310346615957836592 15 \"toxicity\"";
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

            const string expectedCommand = "tempban 2310346615957836592 30 \"sync-ban\"";
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
