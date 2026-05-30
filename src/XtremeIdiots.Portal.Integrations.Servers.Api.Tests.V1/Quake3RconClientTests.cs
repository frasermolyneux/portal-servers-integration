using Microsoft.Extensions.Logging;
using Moq;
using XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Helpers;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Clients;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1
{
    [Trait("Category", "Unit")]
    public class Quake3RconClientTests : IDisposable
    {
        private readonly Mock<ILogger<Quake3RconClient>> _loggerMock;
        private readonly Quake3RconClient _rconClient;
        private readonly MockUdpServer _mockServer;
        private readonly Guid _testServerId = Guid.NewGuid();
        private const string TestRconPassword = "testpass";

        public Quake3RconClientTests()
        {
            _loggerMock = new Mock<ILogger<Quake3RconClient>>();
            _rconClient = new Quake3RconClient(_loggerMock.Object);
            _mockServer = new MockUdpServer();

            // Configure the client to connect to our mock server
            _rconClient.Configure(
                GameType.CallOfDuty4,
                _testServerId,
                "127.0.0.1",
                _mockServer.Port,
                TestRconPassword
            );
        }

        [Fact]
        public async Task KickPlayerByName_SendsCorrectCommand()
        {
            // Arrange
            var playerName = "TestPlayer";
            var expectedResponse = "Player kicked";
            var commandReceived = false;

            _mockServer.RegisterCommandHandler($"kick \"{playerName}\"", cmd =>
            {
                commandReceived = true;
                return MockUdpServer.CreateQuake3Response(expectedResponse);
            });
            _mockServer.Start();
            await Task.Delay(100); // Give server time to start

            // Act
            var result = await _rconClient.KickPlayerByName(playerName);

            // Assert
            await Task.Delay(200); // Give time for async processing
            Assert.True(commandReceived, "Expected command was not received by mock server");
            Assert.Contains(expectedResponse, result);
        }

        [Fact]
        public async Task KickAllPlayers_SendsCorrectCommand()
        {
            // Arrange
            var expectedResponse = "All players kicked";
            var commandReceived = false;

            _mockServer.RegisterCommandHandler("kickall", cmd =>
            {
                commandReceived = true;
                return MockUdpServer.CreateQuake3Response(expectedResponse);
            });
            _mockServer.Start();
            await Task.Delay(100);

            // Act
            var result = await _rconClient.KickAllPlayers();

            // Assert
            await Task.Delay(200);
            Assert.True(commandReceived, "Expected command was not received by mock server");
            Assert.Contains(expectedResponse, result);
        }

        [Fact]
        public async Task BanPlayerByName_SendsCorrectCommand()
        {
            // Arrange
            var playerName = "TestPlayer";
            var expectedResponse = "Player banned";
            var commandReceived = false;

            _mockServer.RegisterCommandHandler($"banUser \"{playerName}\"", cmd =>
            {
                commandReceived = true;
                return MockUdpServer.CreateQuake3Response(expectedResponse);
            });
            _mockServer.Start();
            await Task.Delay(100);

            // Act
            var result = await _rconClient.BanPlayerByName(playerName);

            // Assert
            await Task.Delay(200);
            Assert.True(commandReceived, "Expected command was not received by mock server");
            Assert.Contains(expectedResponse, result);
        }

        [Fact]
        public async Task TempBanPlayerByName_SendsCorrectCommand()
        {
            // Arrange
            var playerName = "TestPlayer";
            var expectedResponse = "Player temp banned";
            var commandReceived = false;

            _mockServer.RegisterCommandHandler($"tempBanUser \"{playerName}\"", cmd =>
            {
                commandReceived = true;
                return MockUdpServer.CreateQuake3Response(expectedResponse);
            });
            _mockServer.Start();
            await Task.Delay(100);

            // Act
            var result = await _rconClient.TempBanPlayerByName(playerName);

            // Assert
            await Task.Delay(200);
            Assert.True(commandReceived, "Expected command was not received by mock server");
            Assert.Contains(expectedResponse, result);
        }

        [Fact]
        public async Task TempBanPlayer_SendsCorrectCommand()
        {
            // Arrange
            var clientId = 5;
            var expectedResponse = "Client temp banned";
            var commandReceived = false;

            _mockServer.RegisterCommandHandler($"tempBanClient {clientId}", cmd =>
            {
                commandReceived = true;
                return MockUdpServer.CreateQuake3Response(expectedResponse);
            });
            _mockServer.Start();
            await Task.Delay(100);

            // Act
            var result = await _rconClient.TempBanPlayer(clientId);

            // Assert
            await Task.Delay(200);
            Assert.True(commandReceived, "Expected command was not received by mock server");
            Assert.Contains(expectedResponse, result);
        }

        [Fact]
        public async Task UnbanPlayer_SendsCorrectCommand()
        {
            // Arrange
            var playerName = "TestPlayer";
            var expectedResponse = "Player unbanned";
            var commandReceived = false;

            _mockServer.RegisterCommandHandler($"unbanuser \"{playerName}\"", cmd =>
            {
                commandReceived = true;
                return MockUdpServer.CreateQuake3Response(expectedResponse);
            });
            _mockServer.Start();
            await Task.Delay(100);

            // Act
            var result = await _rconClient.UnbanPlayer(playerName);

            // Assert
            await Task.Delay(200);
            Assert.True(commandReceived, "Expected command was not received by mock server");
            Assert.Contains(expectedResponse, result);
        }

        [Fact]
        public async Task TellPlayer_SendsCorrectCommand()
        {
            // Arrange
            var clientId = 3;
            var message = "Hello player";
            var expectedResponse = "Message sent";
            var commandReceived = false;

            _mockServer.RegisterCommandHandler($"tell {clientId} \"{message}\"", cmd =>
            {
                commandReceived = true;
                return MockUdpServer.CreateQuake3Response(expectedResponse);
            });
            _mockServer.Start();
            await Task.Delay(100);

            // Act
            var result = await _rconClient.TellPlayer(clientId, message);

            // Assert
            await Task.Delay(200);
            Assert.True(commandReceived, "Expected command was not received by mock server");
            Assert.Contains(expectedResponse, result);
        }

        [Fact]
        public async Task ChangeMap_SendsCorrectCommand()
        {
            // Arrange
            var mapName = "mp_crash";
            var expectedResponse = "Changing map";
            var commandReceived = false;

            _mockServer.RegisterCommandHandler($"map {mapName}", cmd =>
            {
                commandReceived = true;
                return MockUdpServer.CreateQuake3Response(expectedResponse);
            });
            _mockServer.Start();
            await Task.Delay(100);

            // Act
            var result = await _rconClient.ChangeMap(mapName);

            // Assert
            await Task.Delay(200);
            Assert.True(commandReceived, "Expected command was not received by mock server");
            Assert.Contains(expectedResponse, result);
        }

        [Fact]
        public async Task GetServerInfo_SendsCorrectCommand()
        {
            // Arrange
            var expectedResponse = "sv_hostname: Test Server\nmap: mp_crash";
            var commandReceived = false;

            _mockServer.RegisterCommandHandler("serverinfo", cmd =>
            {
                commandReceived = true;
                return MockUdpServer.CreateQuake3Response(expectedResponse);
            });
            _mockServer.Start();
            await Task.Delay(100);

            // Act
            var result = await _rconClient.GetServerInfo();

            // Assert
            await Task.Delay(200);
            Assert.True(commandReceived, "Expected command was not received by mock server");
            Assert.Contains("Test Server", result);
        }

        [Fact]
        public async Task GetSystemInfo_SendsCorrectCommand()
        {
            // Arrange
            var expectedResponse = "System: Linux\nCPU: x64";
            var commandReceived = false;

            _mockServer.RegisterCommandHandler("systeminfo", cmd =>
            {
                commandReceived = true;
                return MockUdpServer.CreateQuake3Response(expectedResponse);
            });
            _mockServer.Start();
            await Task.Delay(100);

            // Act
            var result = await _rconClient.GetSystemInfo();

            // Assert
            await Task.Delay(200);
            Assert.True(commandReceived, "Expected command was not received by mock server");
            Assert.Contains("Linux", result);
        }

        [Fact]
        public async Task GetCommandList_SendsCorrectCommand()
        {
            // Arrange
            var expectedResponse = "kick\nban\nstatus\nmap";
            var commandReceived = false;

            _mockServer.RegisterCommandHandler("cmdlist", cmd =>
            {
                commandReceived = true;
                return MockUdpServer.CreateQuake3Response(expectedResponse);
            });
            _mockServer.Start();
            await Task.Delay(100);

            // Act
            var result = await _rconClient.GetCommandList();

            // Assert
            await Task.Delay(200);
            Assert.True(commandReceived, "Expected command was not received by mock server");
            Assert.Contains("kick", result);
        }

        [Fact]
        public async Task Say_SendsCorrectCommand()
        {
            // Arrange
            var message = "Server announcement";
            var commandReceived = false;

            _mockServer.RegisterCommandHandler($"say \"{message}\"", cmd =>
            {
                commandReceived = true;
                return MockUdpServer.CreateQuake3Response("Message sent to all players");
            });
            _mockServer.Start();
            await Task.Delay(100);

            // Act
            await _rconClient.Say(message);

            // Assert
            await Task.Delay(200);
            Assert.True(commandReceived, "Expected command was not received by mock server");
        }

        [Fact]
        public async Task GetCurrentMap_ReturnsMapName()
        {
            // Arrange
            var expectedMap = "mp_crash";
            var serverInfoResponse = $"mapname {expectedMap}\nsv_hostname TestServer\ng_gametype dm\nsv_maxclients 32";
            var commandReceived = false;

            _mockServer.RegisterCommandHandler("serverinfo", cmd =>
            {
                commandReceived = true;
                return MockUdpServer.CreateQuake3Response(serverInfoResponse);
            });
            _mockServer.Start();
            await Task.Delay(100);

            // Act
            var result = await _rconClient.GetCurrentMap();

            // Assert
            await Task.Delay(200);
            Assert.True(commandReceived, "Expected command was not received by mock server");
            Assert.Equal(expectedMap, result);
        }

        [Fact]
        public async Task GetCurrentMap_ReturnsUnknown_WhenMapNameNotInResponse()
        {
            // Arrange
            var serverInfoResponse = "sv_hostname TestServer\ng_gametype dm\nsv_maxclients 32";

            _mockServer.RegisterCommandHandler("serverinfo", cmd =>
            {
                return MockUdpServer.CreateQuake3Response(serverInfoResponse);
            });
            _mockServer.Start();
            await Task.Delay(100);

            // Act
            var result = await _rconClient.GetCurrentMap();

            // Assert
            await Task.Delay(200);
            Assert.Equal("Unknown", result);
        }

        [Fact]
        public async Task GetPlayers_Cod4x_ParsesPlayerIdAsGuid()
        {
            // Arrange — CoD4x status output has an extra steamid column between playerid and name.
            // Columns: num score ping playerid(19) steamid(17 or 0) name lastmsg address qport rate
            // The regex must capture the 19-digit playerid as the GUID (group 4) and treat
            // steamid as a non-captured column so downstream index positions stay aligned with CoD4.
            _rconClient.Configure(
                GameType.CallOfDuty4x,
                _testServerId,
                "127.0.0.1",
                _mockServer.Port,
                TestRconPassword
            );

            var statusResponse =
                "map: mp_atp\n" +
                "num score ping playerid          steamid          name            lastmsg address               qport rate\n" +
                "--- ----- ---- ----------------- ---------------- --------------- ------- --------------------- ----- -----\n" +
                "  0    42   78 2310346616629847491 76561198076145247 BloodyEye 50 81.97.115.41:28960 -1234 25000\n" +
                "  1     5    0 1234567890123456789 0 Mikey 12 192.168.1.50:28961 -5678 25000\n";

            _mockServer.RegisterCommandHandler("status", cmd =>
                MockUdpServer.CreateQuake3Response(statusResponse));
            _mockServer.Start();
            await Task.Delay(100);

            // Act
            var players = _rconClient.GetPlayers();

            // Assert
            Assert.Equal(2, players.Count);

            Assert.Equal(0, players[0].Num);
            Assert.Equal(78, players[0].Ping);
            Assert.Equal("2310346616629847491", players[0].Guid);
            Assert.Equal("BloodyEye", players[0].Name);
            Assert.Equal("81.97.115.41", players[0].IpAddress);

            Assert.Equal(1, players[1].Num);
            Assert.Equal("1234567890123456789", players[1].Guid);
            Assert.Equal("Mikey", players[1].Name);
            Assert.Equal("192.168.1.50", players[1].IpAddress);
        }

        [Fact]
        public async Task GetPlayers_Cod4x_ToleratesPrimZmbiCnctPingStates()
        {
            // Arrange — connecting / zombie / primed players in CoD4x show alphabetic ping states.
            // Verify the regex accepts PRIM / ZMBI / CNCT in the ping column without dropping rows.
            _rconClient.Configure(
                GameType.CallOfDuty4x,
                _testServerId,
                "127.0.0.1",
                _mockServer.Port,
                TestRconPassword
            );

            var statusResponse =
                "map: mp_crash\n" +
                "num score ping playerid          steamid          name            lastmsg address               qport rate\n" +
                "--- ----- ---- ----------------- ---------------- --------------- ------- --------------------- ----- -----\n" +
                "  0     0 PRIM 1111111111111111111 0 Alpha 25 10.0.0.1:28960 -1 25000\n" +
                "  1     0 ZMBI 2222222222222222222 0 Beta 25 10.0.0.2:28960 -2 25000\n" +
                "  2     0 CNCT 3333333333333333333 0 Gamma 25 10.0.0.3:28960 -3 25000\n";

            _mockServer.RegisterCommandHandler("status", cmd =>
                MockUdpServer.CreateQuake3Response(statusResponse));
            _mockServer.Start();
            await Task.Delay(100);

            // Act
            var players = _rconClient.GetPlayers();

            // Assert — all three rows parse; non-numeric ping falls back to 0.
            Assert.Equal(3, players.Count);
            Assert.Equal("1111111111111111111", players[0].Guid);
            Assert.Equal(0, players[0].Ping);
            Assert.Equal("2222222222222222222", players[1].Guid);
            Assert.Equal(0, players[1].Ping);
            Assert.Equal("3333333333333333333", players[2].Guid);
            Assert.Equal(0, players[2].Ping);
        }

        public void Dispose()
        {
            _mockServer?.Dispose();
        }
    }
}
