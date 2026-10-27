using System.Net;
using System.Text;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MX.Api.Abstractions;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Config;
using XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Controllers;

[Trait("Category", "Unit")]
public class ConfigControllerTests
{
    private readonly Mock<ILogger<ConfigController>> _mockLogger = new();
    private readonly Mock<IGameServerFileTransportFactory> _mockFileTransportFactory = new();
    private readonly TelemetryClient _telemetryClient;

    public ConfigControllerTests()
    {
        var telemetryConfig = new TelemetryConfiguration { TelemetryChannel = new Mock<ITelemetryChannel>().Object };
        _telemetryClient = new TelemetryClient(telemetryConfig);
    }

    private ConfigController CreateController() => new(
        _mockLogger.Object,
        _mockFileTransportFactory.Object,
        _telemetryClient);

    [Theory]
    [InlineData("//set sv_mapRotation \"map mp_bog\"")]
    [InlineData("set another_variable \"map mp_bog\"")]
    [InlineData("set sv_mapRotation map mp_bog")]
    public async Task UpdateConfigVariable_WhenNoWritableEntry_ReturnsActionableErrorWithoutUploading(string content)
    {
        var session = SetupConfigFile(content);

        var result = await CreateController().UpdateConfigVariable(Guid.NewGuid(), "rotation.cfg", "sv_maprotation",
            new UpdateConfigVariableRequest { Value = "gametype war map mp_farm" });

        var response = Assert.IsType<ApiResponse>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal(400, ((ObjectResult)result).StatusCode);
        var error = Assert.Single(response.Errors!);
        Assert.Equal("CONFIG_VARIABLE_NOT_FOUND", error.Code);
        Assert.Contains("active, quoted", error.Message, StringComparison.Ordinal);
        Assert.Contains("rotation.cfg", error.Message, StringComparison.Ordinal);
        Assert.Contains("Select a different file", error.Message, StringComparison.Ordinal);
        session.Verify(x => x.UploadBytes(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateConfigVariable_WhenSessionConnectionFails_PreservesTheErrorEnvelope()
    {
        _mockFileTransportFactory.Setup(x => x.CreateSession(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<IGameServerFileTransportSession>(HttpStatusCode.InternalServerError,
                new ApiResponse<IGameServerFileTransportSession>(new ApiError("FTP_CONNECTION_FAILED", "Connection failed"))));

        var result = await CreateController().UpdateConfigVariable(Guid.NewGuid(), "rotation.cfg", "sv_maprotation",
            new UpdateConfigVariableRequest { Value = "gametype war map mp_bog" });

        var response = Assert.IsType<ApiResponse>(Assert.IsType<ObjectResult>(result).Value);
        Assert.Equal("FTP_CONNECTION_FAILED", Assert.Single(response.Errors!).Code);
    }

    [Fact]
    public async Task UpdateConfigVariable_WhenActiveEntryFollowsComment_UpdatesOnlyTheActiveEntry()
    {
        const string content = "//set sv_mapRotation \"map mp_bog\"\r\nset SV_MAPROTATION \"\"\r\n";
        var session = SetupConfigFile(content);
        byte[]? uploaded = null;
        session.Setup(x => x.UploadBytes("rotation.cfg", It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], CancellationToken>((_, bytes, _) => uploaded = bytes)
            .Returns(Task.CompletedTask);

        var result = await CreateController().UpdateConfigVariable(Guid.NewGuid(), "rotation.cfg", "sv_maprotation",
            new UpdateConfigVariableRequest { Value = "gametype war map mp_farm" });

        Assert.Equal(200, Assert.IsType<StatusCodeResult>(result).StatusCode);
        Assert.NotNull(uploaded);
        Assert.Equal("//set sv_mapRotation \"map mp_bog\"\r\nset SV_MAPROTATION \"gametype war map mp_farm\"\r\n", Encoding.UTF8.GetString(uploaded));
    }

    private Mock<IGameServerFileTransportSession> SetupConfigFile(string content)
    {
        var session = new Mock<IGameServerFileTransportSession>();
        session.SetupGet(x => x.Transport).Returns(new ResolvedFileTransport(FileTransportType.Ftp, "ftp",
            new FileTransportCredentials("synthetic-host", 21, "synthetic-user", "synthetic-password")));
        session.Setup(x => x.FileExists(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        session.Setup(x => x.DownloadBytes(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Encoding.UTF8.GetBytes(content));
        _mockFileTransportFactory.Setup(x => x.CreateSession(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<IGameServerFileTransportSession>(HttpStatusCode.OK, new ApiResponse<IGameServerFileTransportSession>(session.Object)));
        return session;
    }

    [Fact]
    public async Task GetConfigFile_WhenSessionConnectionFails_ReturnsConnectionError()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        var failedResult = new ApiResult<IGameServerFileTransportSession>(
            HttpStatusCode.InternalServerError,
            new ApiResponse<IGameServerFileTransportSession>(new ApiError("FTP_CONNECTION_FAILED", "Connection failed")));

        _mockFileTransportFactory
            .Setup(x => x.CreateSession(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedResult);

        var controller = CreateController();

        // Act
        var result = await controller.GetConfigFile(gameServerId, "server.cfg");

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(200, objectResult.StatusCode);
        var response = Assert.IsType<ApiResponse<ConfigFileContentDto>>(objectResult.Value);
        Assert.Equal("FTP_CONNECTION_FAILED", response.Errors?.FirstOrDefault()?.Code);
    }
}
