using Microsoft.Extensions.Logging;
using Moq;
using XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Helpers;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Clients;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1
{
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

        public void Dispose()
        {
            _mockServer?.Dispose();
        }
    }
}
