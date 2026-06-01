using System.Net;
using MX.Api.Abstractions;
using Newtonsoft.Json;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.Configurations;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.GameServers;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Helpers;

[Trait("Category", "Unit")]
public class FileTransportResolverTests
{
    private readonly Mock<IRepositoryApiClient> _repositoryApiClient = new() { DefaultValue = DefaultValue.Mock };

    private FileTransportResolver CreateResolver() => new(_repositoryApiClient.Object);

    [Fact]
    public async Task Resolve_WhenTransportTypeIsSftp_UsesSftpNamespaceAndDefaultsPort22()
    {
        var gameServerId = Guid.NewGuid();
        var gameServer = CreateGameServerDto(fileTransportEnabled: true, fileTransportType: FileTransportType.Sftp, ftpEnabled: false);
        var configuration = CreateConfigurationDto("sftp", "{\"hostname\":\"sftp-host\",\"username\":\"demo\",\"password\":\"secret\"}");

        _repositoryApiClient
            .Setup(x => x.GameServers.V1.GetGameServer(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<GameServerDto>(HttpStatusCode.OK, new ApiResponse<GameServerDto>(gameServer)));

        _repositoryApiClient
            .Setup(x => x.GameServerConfigurations.V1.GetConfiguration(gameServerId, "sftp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<ConfigurationDto>(HttpStatusCode.OK, new ApiResponse<ConfigurationDto>(configuration)));

        var resolver = CreateResolver();

        var result = await resolver.Resolve(gameServerId);

        Assert.True(result.IsSuccess);
        Assert.Equal(FileTransportType.Sftp, result.Result!.Data!.TransportType);
        Assert.Equal("sftp", result.Result.Data.ConfigurationNamespace);
        Assert.Equal(22, result.Result.Data.Credentials.Port);

        _repositoryApiClient.Verify(
            x => x.GameServerConfigurations.V1.GetConfiguration(gameServerId, "sftp", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Resolve_WhenLegacyFtpEnabled_FallsBackToFtpNamespace()
    {
        var gameServerId = Guid.NewGuid();
        var gameServer = CreateGameServerDto(fileTransportEnabled: false, fileTransportType: FileTransportType.Unknown, ftpEnabled: true);
        var configuration = CreateConfigurationDto("ftp", "{\"hostname\":\"ftp-host\",\"username\":\"demo\",\"password\":\"secret\",\"port\":2121}");

        _repositoryApiClient
            .Setup(x => x.GameServers.V1.GetGameServer(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<GameServerDto>(HttpStatusCode.OK, new ApiResponse<GameServerDto>(gameServer)));

        _repositoryApiClient
            .Setup(x => x.GameServerConfigurations.V1.GetConfiguration(gameServerId, "ftp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<ConfigurationDto>(HttpStatusCode.OK, new ApiResponse<ConfigurationDto>(configuration)));

        var resolver = CreateResolver();

        var result = await resolver.Resolve(gameServerId);

        Assert.True(result.IsSuccess);
        Assert.Equal(FileTransportType.Ftp, result.Result!.Data!.TransportType);
        Assert.Equal("ftp", result.Result.Data.ConfigurationNamespace);
        Assert.Equal(2121, result.Result.Data.Credentials.Port);
    }

    [Fact]
    public async Task Resolve_WhenTransportNotEnabled_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();
        var gameServer = CreateGameServerDto(fileTransportEnabled: false, fileTransportType: FileTransportType.Unknown, ftpEnabled: false);

        _repositoryApiClient
            .Setup(x => x.GameServers.V1.GetGameServer(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<GameServerDto>(HttpStatusCode.OK, new ApiResponse<GameServerDto>(gameServer)));

        var resolver = CreateResolver();

        var result = await resolver.Resolve(gameServerId);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.NotNull(result.Result?.Errors);
        Assert.Equal("FTP_CREDENTIALS_MISSING", result.Result!.Errors!.First().Code);
    }

    [Fact]
    public async Task Resolve_WhenConfigurationIsMalformed_ReturnsBadRequest()
    {
        var gameServerId = Guid.NewGuid();
        var gameServer = CreateGameServerDto(fileTransportEnabled: true, fileTransportType: FileTransportType.Sftp, ftpEnabled: false);
        var malformedConfiguration = CreateConfigurationDto("sftp", "{\"hostname\":\"sftp-host\"");

        _repositoryApiClient
            .Setup(x => x.GameServers.V1.GetGameServer(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<GameServerDto>(HttpStatusCode.OK, new ApiResponse<GameServerDto>(gameServer)));

        _repositoryApiClient
            .Setup(x => x.GameServerConfigurations.V1.GetConfiguration(gameServerId, "sftp", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<ConfigurationDto>(HttpStatusCode.OK, new ApiResponse<ConfigurationDto>(malformedConfiguration)));

        var resolver = CreateResolver();

        var result = await resolver.Resolve(gameServerId);

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }

    private static GameServerDto CreateGameServerDto(bool fileTransportEnabled, FileTransportType fileTransportType, bool ftpEnabled)
    {
        var json = JsonConvert.SerializeObject(new
        {
            GameServerId = Guid.NewGuid(),
            Title = "Server",
            GameType = GameType.CallOfDuty4,
            Hostname = "localhost",
            QueryPort = 28960,
            FileTransportEnabled = fileTransportEnabled,
            FileTransportType = fileTransportType,
            FtpEnabled = ftpEnabled,
            RconEnabled = true,
        });

        return JsonConvert.DeserializeObject<GameServerDto>(json)!;
    }

    private static ConfigurationDto CreateConfigurationDto(string @namespace, string configuration)
    {
        var json = JsonConvert.SerializeObject(new
        {
            Namespace = @namespace,
            Configuration = configuration,
            LastModifiedUtc = DateTime.UtcNow,
        });

        return JsonConvert.DeserializeObject<ConfigurationDto>(json)!;
    }
}
