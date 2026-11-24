using Microsoft.Extensions.Logging;
using Moq;
using XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Helpers;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Clients;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;
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

        [Theory]
        [InlineData("kick", "clientkick 3")]
        [InlineData("public", "say \" hello \"")]
        [InlineData("private", "tell 3 \" hello \"")]
        public async Task RulesNativeDispatch_SendsOneUnconfirmedCommand(string effect, string expectedCommand)
        {
            var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _mockServer.RegisterCommandHandler(expectedCommand, command =>
            {
                received.TrySetResult(command);
                return MockUdpServer.CreateQuake3Response("ok");
            });
            _mockServer.Start();

            switch (effect)
            {
                case "kick":
                    await _rconClient.DispatchKick(3, CancellationToken.None);
                    break;
                case "public":
                    await _rconClient.DispatchPublicMessage(" hello ", CancellationToken.None);
                    break;
                case "private":
                    await _rconClient.DispatchPrivateMessage(3, " hello ", CancellationToken.None);
                    break;
            }

            Assert.Equal(expectedCommand, await received.Task.WaitAsync(TimeSpan.FromSeconds(5)));
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
            var commandReceived = false;

            _mockServer.RegisterCommandHandler($"tell {clientId} \"{message}\"", cmd =>
            {
                commandReceived = true;
                return MockUdpServer.CreateQuake3Response("Message sent");
            });
            _mockServer.Start();
            await Task.Delay(100);

            // Act
            var result = await _rconClient.TellPlayer(clientId, message);

            // Assert
            await Task.Delay(200);
            Assert.True(commandReceived, "Expected command was not received by mock server");
            Assert.Equal("Tell command sent to player", result);
        }

        [Fact]
        public async Task TellPlayer_DoesNotRequireResponsePacket()
        {
            // Arrange
            var clientId = 4;
            var message = "No response expected";

            _mockServer.Start();
            await Task.Delay(100);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            var result = await _rconClient.TellPlayer(clientId, message);
            stopwatch.Stop();

            // Assert
            Assert.Equal("Tell command sent to player", result);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), "Tell should not block waiting for a response packet.");
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
        public async Task TakeScreenshot_SendsCorrectCommand()
        {
            // Arrange
            var playerIdentifier = "2310346615957836592";
            var expectedResponse = "screenshot queued";
            var commandReceived = false;

            _mockServer.RegisterCommandHandler($"getss {playerIdentifier}", cmd =>
            {
                commandReceived = true;
                return MockUdpServer.CreateQuake3Response(expectedResponse);
            });
            _mockServer.Start();
            await Task.Delay(100);

            // Act
            var result = await _rconClient.TakeScreenshot(playerIdentifier);

            // Assert
            await Task.Delay(200);
            Assert.True(commandReceived, "Expected command was not received by mock server");
            Assert.Contains(expectedResponse, result);
        }

        [Fact]
        public async Task BanPlayerByPlayerIdentifier_SendsCorrectCommandForCoD4x()
        {
            // Arrange
            var cod4xRconClient = new CallOfDuty4xRconClient(new Mock<ILogger<CallOfDuty4xRconClient>>().Object);
            cod4xRconClient.Configure(GameType.CallOfDuty4x, _testServerId, "127.0.0.1", _mockServer.Port, TestRconPassword);

            var playerIdentifier = "2310346615957836592";
            var reason = "sync-ban";
            var expectedResponse = "permban queued";
            var commandReceived = false;

            _mockServer.RegisterCommandHandler($"permban {playerIdentifier} \"{reason}\"", cmd =>
            {
                commandReceived = true;
                return MockUdpServer.CreateQuake3Response(expectedResponse);
            });
            _mockServer.Start();
            await Task.Delay(100);

            // Act
            var result = await cod4xRconClient.BanPlayerByPlayerIdentifier(playerIdentifier, reason);

            // Assert
            await Task.Delay(200);
            Assert.True(commandReceived, "Expected command was not received by mock server");
            Assert.Contains(expectedResponse, result);
        }

        [Fact]
        public async Task TempBanPlayerByPlayerIdentifier_SendsCorrectCommandForCoD4x()
        {
            // Arrange
            var cod4xRconClient = new CallOfDuty4xRconClient(new Mock<ILogger<CallOfDuty4xRconClient>>().Object);
            cod4xRconClient.Configure(GameType.CallOfDuty4x, _testServerId, "127.0.0.1", _mockServer.Port, TestRconPassword);

            var playerIdentifier = "2310346615957836592";
            const int durationMinutes = 15;
            var reason = "sync-ban";
            var expectedResponse = "tempban queued";
            var commandReceived = false;

            _mockServer.RegisterCommandHandler($"tempban {playerIdentifier} {durationMinutes} \"{reason}\"", cmd =>
            {
                commandReceived = true;
                return MockUdpServer.CreateQuake3Response(expectedResponse);
            });
            _mockServer.Start();
            await Task.Delay(100);

            // Act
            var result = await cod4xRconClient.TempBanPlayerByPlayerIdentifier(playerIdentifier, durationMinutes, reason);

            // Assert
            await Task.Delay(200);
            Assert.True(commandReceived, "Expected command was not received by mock server");
            Assert.Contains(expectedResponse, result);
        }

        [Fact]
        public async Task UnbanPlayerByPlayerIdentifier_SendsCorrectCommandForCoD4x()
        {
            // Arrange
            var cod4xRconClient = new CallOfDuty4xRconClient(new Mock<ILogger<CallOfDuty4xRconClient>>().Object);
            cod4xRconClient.Configure(GameType.CallOfDuty4x, _testServerId, "127.0.0.1", _mockServer.Port, TestRconPassword);

            var playerIdentifier = "2310346615957836592";
            var expectedResponse = "unban queued";
            var commandReceived = false;

            _mockServer.RegisterCommandHandler($"unban {playerIdentifier}", cmd =>
            {
                commandReceived = true;
                return MockUdpServer.CreateQuake3Response(expectedResponse);
            });
            _mockServer.Start();
            await Task.Delay(100);

            // Act
            var result = await cod4xRconClient.UnbanPlayerByPlayerIdentifier(playerIdentifier);

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
        public async Task Say_DoesNotRequireResponsePacket()
        {
            // Arrange
            var message = "Fire and forget";

            _mockServer.Start();
            await Task.Delay(100);
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Act
            var ex = await Record.ExceptionAsync(() => _rconClient.Say(message));
            stopwatch.Stop();

            // Assert
            Assert.Null(ex);
            Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), "Say should not block waiting for a response packet.");
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
        public async Task GetPlayers_Cod4x_ParsesBracketedIpv6Address()
        {
            // Arrange
            _rconClient.Configure(
                GameType.CallOfDuty4x,
                _testServerId,
                "127.0.0.1",
                _mockServer.Port,
                TestRconPassword
            );

            var statusResponse =
                "map: mp_farm\n" +
                "num score ping playerid          steamid          name            lastmsg address                               qport rate\n" +
                "--- ----- ---- ----------------- ---------------- --------------- ------- ------------------------------------- ----- -----\n" +
                " 29   700   76 2310346615250149339 0 srt4fun 0 [2607:9b00:a200::10]:28960 40417 25000\n";

            _mockServer.RegisterCommandHandler("status", _ =>
                MockUdpServer.CreateQuake3Response(statusResponse));
            _mockServer.Start();
            await Task.Delay(100);

            // Act
            var players = _rconClient.GetPlayers();

            // Assert
            Assert.Single(players);
            Assert.Equal("2310346615250149339", players[0].Guid);
            Assert.Equal("srt4fun", players[0].Name);
            Assert.Equal("2607:9b00:a200::10", players[0].IpAddress);
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

        [Theory]
        [InlineData(GameType.CallOfDuty2, "123456789")]
        [InlineData(GameType.CallOfDuty4, "abcdef0123456789abcdef0123456789")]
        [InlineData(GameType.CallOfDuty5, "123456789")]
        public void GetPlayersWithRoster_PreservesObservedIdentitiesAndReportsUnresolvedRows(GameType gameType, string identity)
        {
            _rconClient.Configure(gameType, _testServerId, "127.0.0.1", _mockServer.Port, TestRconPassword);
            var missingIdentity = gameType == GameType.CallOfDuty4 ? new string('0', 32) : "0";
            var response =
                "map: mp_test\n" +
                "num score ping guid name lastmsg address qport rate\n" +
                "--- ----- ---- ---- ---- ------- ------- ----- ----\n" +
                $"0 10 50 {identity} Alice 1 127.0.0.1:28960 42 25000\n" +
                $"1 0 50 {missingIdentity} Pending 1 127.0.0.2:28960 42 25000\n" +
                "2 0 0 0 TestBot 0 bot 0 25000\n" +
                "3 malformed human row\n";
            _mockServer.RegisterCommandHandler("status", _ => MockUdpServer.CreateQuake3Response(response));
            _mockServer.Start();

            var (_, roster) = _rconClient.GetPlayersWithRoster(CancellationToken.None);

            Assert.Equal(RconRosterState.Partial, roster.State);
            var player = Assert.Single(roster.Players);
            Assert.Equal(identity, player.PlayerIdentifier);
            Assert.Equal(0, player.Slot);
            Assert.Equal(1, roster.MissingIdentityCount);
            Assert.Equal(1, roster.KnownBotCount);
            Assert.Equal(1, roster.UnparsedRowCount);
        }

        [Theory]
        [InlineData("map: mp_test\nnum score ping guid name lastmsg address qport rate\n--- ----- ---- ---- ---- ------- ------- ----- ----\n", RconRosterState.Partial)]
        [InlineData("", RconRosterState.Failed)]
        [InlineData("Bad rconpassword.", RconRosterState.Failed)]
        [InlineData("map: mp_test\nnum score ping guid name lastmsg address qport rate\n", RconRosterState.Failed)]
        public void GetPlayersWithRoster_DistinguishesEmptyStatusFromInvalidResponse(string response, RconRosterState expected)
        {
            _mockServer.RegisterCommandHandler("status", _ => MockUdpServer.CreateQuake3Response(response));
            _mockServer.Start();

            var (_, roster) = _rconClient.GetPlayersWithRoster(CancellationToken.None);

            Assert.Equal(expected, roster.State);
            Assert.Empty(roster.Players);
        }

        [Fact]
        public async Task GetPlayersWithRoster_CancellationInterruptsStatusRequest()
        {
            using var received = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            using var cancellation = new CancellationTokenSource();
            _mockServer.RegisterCommandHandler("status", _ =>
            {
                received.Set();
                release.Wait(TimeSpan.FromSeconds(10));
                return MockUdpServer.CreateQuake3Response("");
            });
            _mockServer.Start();

            var request = Task.Run(() => _rconClient.GetPlayersWithRoster(cancellation.Token));
            try
            {
                Assert.True(received.Wait(TimeSpan.FromSeconds(5)));
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await request.WaitAsync(TimeSpan.FromSeconds(2)));
            }
            finally
            {
                release.Set();
            }
        }

        public void Dispose()
        {
            _mockServer?.Dispose();
        }
    }
}
