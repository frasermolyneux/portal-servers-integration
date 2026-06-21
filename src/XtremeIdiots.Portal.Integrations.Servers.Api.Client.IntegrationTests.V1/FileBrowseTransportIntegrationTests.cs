using System.Net;
using MX.Api.Abstractions;
using Newtonsoft.Json;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.Configurations;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.GameServers;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.IntegrationTests.V1;

[Trait("Category", "Integration")]
public class FileBrowseTransportIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public FileBrowseTransportIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync()
    {
        _factory.ResetMocks();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task BrowseDirectory_WhenTransportDisabled_ReturnsCredentialsMissing()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        SetupGameServer(gameServerId, fileTransportEnabled: false, fileTransportType: FileTransportType.Unknown, ftpEnabled: false);

        // Act
        var response = await _client.GetAsync($"/v1.0/file-browse/{gameServerId}/browse");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("FTP_CREDENTIALS_MISSING", content);
        _factory.MockRepositoryApiClient.Verify(
            x => x.GameServerConfigurations.V1.GetConfiguration(gameServerId, "sftp", It.IsAny<CancellationToken>()),
            Times.Never);
        _factory.MockRepositoryApiClient.Verify(
            x => x.GameServerConfigurations.V1.GetConfiguration(gameServerId, "ftp", It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task BrowseDirectory_WhenOnlyLegacyFtpFlagIsEnabled_ReturnsCredentialsMissingWithoutConfigLookup()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        SetupGameServer(gameServerId, fileTransportEnabled: false, fileTransportType: FileTransportType.Unknown, ftpEnabled: true);
        SetupConfiguration(gameServerId, "sftp", "{\"hostname\":\"localhost\",\"username\":\"demo\",\"password\":\"secret\"}");

        // Act
        var response = await _client.GetAsync($"/v1.0/file-browse/{gameServerId}/browse");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("FTP_CREDENTIALS_MISSING", content);
        _factory.MockRepositoryApiClient.Verify(
            x => x.GameServerConfigurations.V1.GetConfiguration(gameServerId, "ftp", It.IsAny<CancellationToken>()),
            Times.Never);
        _factory.MockRepositoryApiClient.Verify(
            x => x.GameServerConfigurations.V1.GetConfiguration(gameServerId, "sftp", It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task BrowseDirectory_WhenSftpSelected_DoesNotFallbackToFtpConfig()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        SetupGameServer(gameServerId, fileTransportEnabled: true, fileTransportType: FileTransportType.Sftp, ftpEnabled: true);
        SetupConfiguration(gameServerId, "ftp", "{\"hostname\":\"localhost\",\"username\":\"demo\",\"password\":\"secret\"}");

        // Act
        var response = await _client.GetAsync($"/v1.0/file-browse/{gameServerId}/browse");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("FTP_CREDENTIALS_MISSING", content);
        _factory.MockRepositoryApiClient.Verify(
            x => x.GameServerConfigurations.V1.GetConfiguration(gameServerId, "sftp", It.IsAny<CancellationToken>()),
            Times.Once);
        _factory.MockRepositoryApiClient.Verify(
            x => x.GameServerConfigurations.V1.GetConfiguration(gameServerId, "ftp", It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private void SetupGameServer(Guid gameServerId, bool fileTransportEnabled, FileTransportType fileTransportType, bool ftpEnabled)
    {
        var json = JsonConvert.SerializeObject(new
        {
            GameServerId = gameServerId,
            Title = "Server",
            GameType = (int)GameType.CallOfDuty4,
            Hostname = "127.0.0.1",
            QueryPort = 28960,
            FileTransportEnabled = fileTransportEnabled,
            FileTransportType = fileTransportType,
            FtpEnabled = ftpEnabled,
        });

        var gameServerDto = JsonConvert.DeserializeObject<GameServerDto>(json)!;
        var apiResponse = new ApiResponse<GameServerDto>(gameServerDto);
        var apiResult = new ApiResult<GameServerDto>(HttpStatusCode.OK, apiResponse);

        _factory.MockRepositoryApiClient
            .Setup(x => x.GameServers.V1.GetGameServer(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResult);
    }

    private void SetupConfiguration(Guid gameServerId, string configNamespace, string rawConfig)
    {
        var json = JsonConvert.SerializeObject(new
        {
            Namespace = configNamespace,
            Configuration = rawConfig,
            LastModifiedUtc = DateTime.UtcNow,
        });

        var configurationDto = JsonConvert.DeserializeObject<ConfigurationDto>(json)!;
        var apiResponse = new ApiResponse<ConfigurationDto>(configurationDto);
        var apiResult = new ApiResult<ConfigurationDto>(HttpStatusCode.OK, apiResponse);

        _factory.MockRepositoryApiClient
            .Setup(x => x.GameServerConfigurations.V1.GetConfiguration(gameServerId, configNamespace, It.IsAny<CancellationToken>()))
            .ReturnsAsync(apiResult);
    }
}
