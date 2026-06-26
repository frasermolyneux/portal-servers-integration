using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MX.Api.Abstractions;
using MX.Observability.ApplicationInsights.Auditing;
using Newtonsoft.Json;
using System.Net;
using System.Text.Json;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;
using XtremeIdiots.Portal.Integrations.Servers.Api.Controllers.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.Interfaces.V1;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Constants;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Interfaces.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.Configurations;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.GameServers;
using XtremeIdiots.Portal.Repository.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Tests.V1.Controllers;

[Trait("Category", "Unit")]
public class RconControllerTests
{
    private static readonly string[] SayMessageBatch = ["one", "two", "three"];
    private static readonly string[] TellMessageBatch = ["first", "second", "third"];

    private readonly Mock<ILogger<RconController>> _mockLogger = new();
    private readonly Mock<IRepositoryApiClient> _mockRepositoryApiClient = new() { DefaultValue = DefaultValue.Mock };
    private readonly Mock<IVersionedGameServersEventsApi> _mockVersionedGameServerEventsApi = new();
    private readonly Mock<IGameServersEventsApi> _mockGameServerEventsApi = new();
    private readonly Mock<IRconClientFactory> _mockRconClientFactory = new();
    private readonly Mock<IAuditLogger> _mockAuditLogger = new();
    private readonly TelemetryClient _telemetryClient;

    public RconControllerTests()
    {
        var telemetryConfig = new TelemetryConfiguration { TelemetryChannel = new Mock<ITelemetryChannel>().Object };
        _telemetryClient = new TelemetryClient(telemetryConfig);

        _mockVersionedGameServerEventsApi.SetupGet(x => x.V1).Returns(_mockGameServerEventsApi.Object);
        _mockRepositoryApiClient.SetupGet(x => x.GameServersEvents).Returns(_mockVersionedGameServerEventsApi.Object);
    }

    private RconController CreateController() => new(
        _mockLogger.Object,
        _mockRepositoryApiClient.Object,
        _mockRconClientFactory.Object,
        _telemetryClient,
        _mockAuditLogger.Object);

    private void SetupValidServerAndRconConfig(Guid gameServerId, GameType gameType = GameType.CallOfDuty4x)
    {
        var gameServerJson = JsonConvert.SerializeObject(new { GameServerId = gameServerId, GameType = (int)gameType, Hostname = "127.0.0.1", QueryPort = 28960 });
        var gameServerDto = JsonConvert.DeserializeObject<GameServerDto>(gameServerJson)!;
        var gameServerResult = new ApiResult<GameServerDto>(HttpStatusCode.OK, new ApiResponse<GameServerDto>(gameServerDto));

        _mockRepositoryApiClient
            .Setup(x => x.GameServers.V1.GetGameServer(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(gameServerResult);

        var configJson = JsonConvert.SerializeObject(new { Configuration = /*lang=json,strict*/ "{\"password\":\"secret\"}" });
        var configDto = JsonConvert.DeserializeObject<ConfigurationDto>(configJson)!;
        var configResult = new ApiResult<ConfigurationDto>(HttpStatusCode.OK, new ApiResponse<ConfigurationDto>(configDto));

        _mockRepositoryApiClient
            .Setup(x => x.GameServerConfigurations.V1.GetConfiguration(gameServerId, "rcon", It.IsAny<CancellationToken>()))
            .ReturnsAsync(configResult);
    }

    [Fact]
    public async Task GetDvar_WhenCoD4xUnknownCvarResponse_ReturnsNotFoundResult()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        SetupValidServerAndRconConfig(gameServerId);

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient.Setup(x => x.GetDvar("unknown_dvar")).ReturnsAsync("^1Bad command or cvar: unknown_dvar");

        _mockRconClientFactory
            .Setup(x => x.CreateInstance(It.IsAny<GameType>(), gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var controller = CreateController();

        // Act
        var result = await ((IRconApi)controller).GetDvar(gameServerId, "unknown_dvar");

        // Assert
        Assert.True(result.IsNotFound);
    }

    [Fact]
    public async Task GetDvar_WhenCoD4xValueContainsColorCodes_ReturnsNormalizedValue()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        SetupValidServerAndRconConfig(gameServerId);

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient.Setup(x => x.GetDvar("sv_hostname")).ReturnsAsync("\"sv_hostname\" is: \"^1Xtreme ^7Idiots\"");

        _mockRconClientFactory
            .Setup(x => x.CreateInstance(It.IsAny<GameType>(), gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var controller = CreateController();

        // Act
        var result = await ((IRconApi)controller).GetDvar(gameServerId, "sv_hostname");

        // Assert
        Assert.NotNull(result.Result?.Data);
        Assert.Equal("Xtreme Idiots", result.Result!.Data!.Value);
    }

    [Fact]
    public async Task GetDvar_WhenCoD4xValueContainsAlphabeticColorCodes_ReturnsNormalizedValue()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        SetupValidServerAndRconConfig(gameServerId);

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient.Setup(x => x.GetDvar("g_motd")).ReturnsAsync("\"g_motd\" is: \"^aWelcome ^ZHome\"");

        _mockRconClientFactory
            .Setup(x => x.CreateInstance(It.IsAny<GameType>(), gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var controller = CreateController();

        // Act
        var result = await ((IRconApi)controller).GetDvar(gameServerId, "g_motd");

        // Assert
        Assert.NotNull(result.Result?.Data);
        Assert.Equal("Welcome Home", result.Result!.Data!.Value);
    }

    [Fact]
    public async Task TakeScreenshot_WhenValidRequestForCoD4x_ReturnsSuccessAndCallsRcon()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        SetupValidServerAndRconConfig(gameServerId, GameType.CallOfDuty4x);

        var trimmedIdentifier = "2310346615957836592";
        var mockRconClient = new Mock<IRconClient>();
        mockRconClient.Setup(x => x.TakeScreenshot(trimmedIdentifier, It.IsAny<CancellationToken>())).ReturnsAsync("ok");

        _mockRconClientFactory
            .Setup(x => x.CreateInstance(It.IsAny<GameType>(), gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var controller = CreateController();

        // Act
        var result = await ((IRconApi)controller).TakeScreenshot(gameServerId, new TakeScreenshotRequestDto { PlayerIdentifier = $"  {trimmedIdentifier}  " });

        // Assert
        Assert.True(result.IsSuccess);
        mockRconClient.Verify(x => x.TakeScreenshot(trimmedIdentifier, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TakeScreenshot_WhenIdentifierIsInvalid_ReturnsBadRequestWithStableErrorCode()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await ((IRconApi)controller).TakeScreenshot(Guid.NewGuid(), new TakeScreenshotRequestDto { PlayerIdentifier = "abc;drop" });

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(ErrorCodes.INVALID_PLAYER_IDENTIFIER, result.Result?.Errors?.Single().Code);
    }

    [Fact]
    public async Task TakeScreenshot_WhenServerIsNotCoD4x_ReturnsBadRequestWithStableErrorCode()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        SetupValidServerAndRconConfig(gameServerId, GameType.CallOfDuty4);
        var controller = CreateController();

        // Act
        var result = await ((IRconApi)controller).TakeScreenshot(gameServerId, new TakeScreenshotRequestDto { PlayerIdentifier = "2310346615957836592" });

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(ErrorCodes.OPERATION_NOT_SUPPORTED_FOR_GAME_TYPE, result.Result?.Errors?.Single().Code);
    }

    [Fact]
    public async Task ResolvePlayer_WhenQueryIsEmpty_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await ((IRconApi)controller).ResolvePlayer(Guid.NewGuid(), new ResolvePlayerRequestDto { PlayerQuery = "   " });

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(ErrorCodes.INVALID_REQUEST, result.Result?.Errors?.Single().Code);
    }

    [Fact]
    public async Task ResolvePlayer_WhenMaxSuggestionsOutOfRange_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = await ((IRconApi)controller).ResolvePlayer(Guid.NewGuid(), new ResolvePlayerRequestDto { PlayerQuery = "fraser", MaxSuggestions = 6 });

        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(ErrorCodes.INVALID_REQUEST, result.Result?.Errors?.Single().Code);
    }

    [Fact]
    public async Task ResolvePlayer_WhenUniqueMatchExists_ReturnsResolved()
    {
        var gameServerId = Guid.NewGuid();
        SetupValidServerAndRconConfig(gameServerId);

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient.Setup(x => x.GetPlayers()).Returns([
            CreateRconPlayer(2, "^1Fraser", "g-1"),
            CreateRconPlayer(5, "SomeoneElse", "g-2")
        ]);

        _mockRconClientFactory
            .Setup(x => x.CreateInstance(It.IsAny<GameType>(), gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var controller = CreateController();

        var result = await ((IRconApi)controller).ResolvePlayer(gameServerId, new ResolvePlayerRequestDto { PlayerQuery = "fraser" });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Result?.Data);
        Assert.Equal(ResolvePlayerStatus.Resolved, result.Result!.Data!.Status);
        Assert.Equal(2, result.Result.Data.ResolvedPlayer?.Slot);
    }

    [Fact]
    public async Task ResolvePlayer_WhenMultipleCloseMatchesExist_ReturnsAmbiguous()
    {
        var gameServerId = Guid.NewGuid();
        SetupValidServerAndRconConfig(gameServerId);

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient.Setup(x => x.GetPlayers()).Returns([
            CreateRconPlayer(1, "Frase", "g-1"),
            CreateRconPlayer(2, "Frazz", "g-2"),
            CreateRconPlayer(3, "Other", "g-3")
        ]);

        _mockRconClientFactory
            .Setup(x => x.CreateInstance(It.IsAny<GameType>(), gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var controller = CreateController();

        var result = await ((IRconApi)controller).ResolvePlayer(gameServerId, new ResolvePlayerRequestDto { PlayerQuery = "fra", MaxSuggestions = 2 });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Result?.Data);
        Assert.Equal(ResolvePlayerStatus.Ambiguous, result.Result!.Data!.Status);
        Assert.Equal(2, result.Result.Data.Suggestions.Count);
    }

    [Fact]
    public async Task Say_WhenEventWriteFails_StillReturnsSuccess()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        SetupValidServerAndRconConfig(gameServerId);

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient.Setup(x => x.Say("Hello all")).Returns(Task.CompletedTask);

        _mockRconClientFactory
            .Setup(x => x.CreateInstance(It.IsAny<GameType>(), gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        _mockGameServerEventsApi
            .Setup(x => x.CreateGameServerEvent(It.IsAny<CreateGameServerEventDto>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("write failed"));

        var controller = CreateController();

        // Act
        var result = await ((IRconApi)controller).Say(gameServerId, "Hello all");

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Say_WhenMessageExceedsLimit_SplitsMessageWithSuffixAndReturnsSuccess()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        SetupValidServerAndRconConfig(gameServerId);

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient.Setup(x => x.Say(It.IsAny<string>())).Returns(Task.CompletedTask);

        _mockRconClientFactory
            .Setup(x => x.CreateInstance(It.IsAny<GameType>(), gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var controller = CreateController();
        var longMessage = new string('A', 400);

        // Act
        var result = await ((IRconApi)controller).Say(gameServerId, longMessage);

        // Assert
        Assert.True(result.IsSuccess);
        mockRconClient.Verify(x => x.Say(It.Is<string>(m => m.EndsWith("(1/4)"))), Times.Once);
        mockRconClient.Verify(x => x.Say(It.Is<string>(m => m.EndsWith("(2/4)"))), Times.Once);
        mockRconClient.Verify(x => x.Say(It.Is<string>(m => m.EndsWith("(3/4)"))), Times.Once);
        mockRconClient.Verify(x => x.Say(It.Is<string>(m => m.EndsWith("(4/4)"))), Times.Once);
    }

    [Fact]
    public async Task Say_WithMessageList_UsesBestEffortAndReturnsSuccessWhenAtLeastOneChunkSends()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        SetupValidServerAndRconConfig(gameServerId);

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient
            .SetupSequence(x => x.Say(It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("boom"))
            .Returns(Task.CompletedTask)
            .Returns(Task.CompletedTask);

        _mockRconClientFactory
            .Setup(x => x.CreateInstance(It.IsAny<GameType>(), gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var controller = CreateController();

        // Act
        var result = await ((IRconApi)controller).Say(gameServerId, SayMessageBatch);

        // Assert
        Assert.True(result.IsSuccess);
        mockRconClient.Verify(x => x.Say(It.IsAny<string>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Say_Action_WithStringBody_ForwardsTrimmedMessage()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        SetupValidServerAndRconConfig(gameServerId);

        var forwardedMessages = new List<string>();
        var mockRconClient = new Mock<IRconClient>();
        mockRconClient
            .Setup(x => x.Say(It.IsAny<string>()))
            .Callback(forwardedMessages.Add)
            .Returns(Task.CompletedTask);

        _mockRconClientFactory
            .Setup(x => x.CreateInstance(It.IsAny<GameType>(), gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var controller = CreateController();
        var requestBody = ParseJsonElement(/*lang=json,strict*/ "\"  hello world  \"");

        // Act
        var result = await controller.Say(gameServerId, requestBody);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(["hello world"], forwardedMessages);
    }

    [Fact]
    public async Task Say_Action_WithArrayBody_ForwardsTrimmedStringEntriesOnly()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        SetupValidServerAndRconConfig(gameServerId);

        var forwardedMessages = new List<string>();
        var mockRconClient = new Mock<IRconClient>();
        mockRconClient
            .Setup(x => x.Say(It.IsAny<string>()))
            .Callback(forwardedMessages.Add)
            .Returns(Task.CompletedTask);

        _mockRconClientFactory
            .Setup(x => x.CreateInstance(It.IsAny<GameType>(), gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var controller = CreateController();
        var requestBody = ParseJsonElement(/*lang=json,strict*/ "[\" first \", 123, null, \"\", \" second\"]");

        // Act
        var result = await controller.Say(gameServerId, requestBody);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(["first", "second"], forwardedMessages);
    }

    [Fact]
    public async Task Say_Action_WithObjectBody_ForwardsMessageCollection()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        SetupValidServerAndRconConfig(gameServerId);

        var forwardedMessages = new List<string>();
        var mockRconClient = new Mock<IRconClient>();
        mockRconClient
            .Setup(x => x.Say(It.IsAny<string>()))
            .Callback(forwardedMessages.Add)
            .Returns(Task.CompletedTask);

        _mockRconClientFactory
            .Setup(x => x.CreateInstance(It.IsAny<GameType>(), gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var controller = CreateController();
        var requestBody = ParseJsonElement(/*lang=json,strict*/ "{\"messages\":[\" one \",\"two\"],\"message\":\"ignored\"}");

        // Act
        var result = await controller.Say(gameServerId, requestBody);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(["one", "two"], forwardedMessages);
    }

    [Fact]
    public async Task Say_Action_WithNullBody_ReturnsBadRequest()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.Say(Guid.NewGuid(), null);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Say_Action_WithInvalidBodyKind_ReturnsBadRequest()
    {
        // Arrange
        var controller = CreateController();
        var requestBody = ParseJsonElement(/*lang=json,strict*/ "true");

        // Act
        var result = await controller.Say(Guid.NewGuid(), requestBody);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task TellPlayer_Action_WithStringBody_ForwardsTrimmedMessage()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        SetupValidServerAndRconConfig(gameServerId);

        var forwardedMessages = new List<string>();
        var mockRconClient = new Mock<IRconClient>();
        mockRconClient
            .Setup(x => x.TellPlayer(7, It.IsAny<string>()))
            .Callback<int, string>((_, message) => forwardedMessages.Add(message))
            .ReturnsAsync("ok");

        _mockRconClientFactory
            .Setup(x => x.CreateInstance(It.IsAny<GameType>(), gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var controller = CreateController();
        var requestBody = ParseJsonElement(/*lang=json,strict*/ "\"  hello world  \"");

        // Act
        var result = await controller.TellPlayer(gameServerId, 7, requestBody);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(["hello world"], forwardedMessages);
    }

    [Fact]
    public async Task TellPlayer_Action_WithArrayBody_ForwardsTrimmedStringEntriesOnly()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        SetupValidServerAndRconConfig(gameServerId);

        var forwardedMessages = new List<string>();
        var mockRconClient = new Mock<IRconClient>();
        mockRconClient
            .Setup(x => x.TellPlayer(7, It.IsAny<string>()))
            .Callback<int, string>((_, message) => forwardedMessages.Add(message))
            .ReturnsAsync("ok");

        _mockRconClientFactory
            .Setup(x => x.CreateInstance(It.IsAny<GameType>(), gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var controller = CreateController();
        var requestBody = ParseJsonElement(/*lang=json,strict*/ "[\" first \",123,null,\"\",\" second\"]");

        // Act
        var result = await controller.TellPlayer(gameServerId, 7, requestBody);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(["first", "second"], forwardedMessages);
    }

    [Fact]
    public async Task TellPlayer_Action_WithObjectBody_ForwardsMessageCollection()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        SetupValidServerAndRconConfig(gameServerId);

        var forwardedMessages = new List<string>();
        var mockRconClient = new Mock<IRconClient>();
        mockRconClient
            .Setup(x => x.TellPlayer(7, It.IsAny<string>()))
            .Callback<int, string>((_, message) => forwardedMessages.Add(message))
            .ReturnsAsync("ok");

        _mockRconClientFactory
            .Setup(x => x.CreateInstance(It.IsAny<GameType>(), gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var controller = CreateController();
        var requestBody = ParseJsonElement(/*lang=json,strict*/ "{\"messages\":[\" one \",\"two\"],\"message\":\"ignored\"}");

        // Act
        var result = await controller.TellPlayer(gameServerId, 7, requestBody);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(["one", "two"], forwardedMessages);
    }

    [Fact]
    public async Task TellPlayer_Action_WithNullBody_ReturnsBadRequest()
    {
        // Arrange
        var controller = CreateController();

        // Act
        var result = await controller.TellPlayer(Guid.NewGuid(), 7, null);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task TellPlayer_Action_WithInvalidBodyKind_ReturnsBadRequest()
    {
        // Arrange
        var controller = CreateController();
        var requestBody = ParseJsonElement(/*lang=json,strict*/ "false");

        // Act
        var result = await controller.TellPlayer(Guid.NewGuid(), 7, requestBody);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task TellPlayerWithVerification_WhenNamesOnlyDifferByColorCodesAndWhitespace_ReturnsSuccess()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        SetupValidServerAndRconConfig(gameServerId);

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient
            .Setup(x => x.GetPlayers())
            .Returns([
                CreateRconPlayer(3, "^1Totty>XI<Adm^7", "2310346613733334073")
            ]);

        mockRconClient
            .Setup(x => x.TellPlayer(3, It.IsAny<string>()))
            .ReturnsAsync("Tell command sent to player");

        _mockRconClientFactory
            .Setup(x => x.CreateInstance(It.IsAny<GameType>(), gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var controller = CreateController();

        // Act
        var result = await ((IRconApi)controller)
            .TellPlayerWithVerification(gameServerId, 3, "hello", "  Totty>XI<Adm  ");

        // Assert
        Assert.True(result.IsSuccess);
        mockRconClient.Verify(x => x.TellPlayer(3, "hello"), Times.Once);
    }

    [Fact]
    public async Task TellPlayerWithVerification_WithMessageList_SendsEachMessageInBestEffortMode()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        SetupValidServerAndRconConfig(gameServerId);

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient
            .Setup(x => x.GetPlayers())
            .Returns([
                CreateRconPlayer(3, "^1Totty>XI<Adm^7", "2310346613733334073")
            ]);

        mockRconClient
            .SetupSequence(x => x.TellPlayer(3, It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("chunk failed"))
            .ReturnsAsync("ok")
            .ReturnsAsync("ok");

        _mockRconClientFactory
            .Setup(x => x.CreateInstance(It.IsAny<GameType>(), gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var controller = CreateController();

        // Act
        var result = await ((IRconApi)controller)
            .TellPlayerWithVerification(gameServerId, 3, TellMessageBatch, "Totty>XI<Adm");

        // Assert
        Assert.True(result.IsSuccess);
        mockRconClient.Verify(x => x.TellPlayer(3, It.IsAny<string>()), Times.Exactly(3));
    }

    [Fact]
    public async Task TellPlayerWithVerification_Action_WithObjectBody_ForwardsMessageAndExpectedName()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        SetupValidServerAndRconConfig(gameServerId);

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient
            .Setup(x => x.GetPlayers())
            .Returns([
                CreateRconPlayer(7, "^1Alpha^7", "2310346613733334073")
            ]);

        var forwardedMessages = new List<string>();
        mockRconClient
            .Setup(x => x.TellPlayer(7, It.IsAny<string>()))
            .Callback<int, string>((_, message) => forwardedMessages.Add(message))
            .ReturnsAsync("ok");

        _mockRconClientFactory
            .Setup(x => x.CreateInstance(It.IsAny<GameType>(), gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var controller = CreateController();
        var requestBody = ParseJsonElement(/*lang=json,strict*/ "{\"message\":\"  hello  \",\"expectedPlayerName\":\"  Alpha  \"}");

        // Act
        var result = await controller.TellPlayerWithVerification(gameServerId, 7, requestBody);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(["hello"], forwardedMessages);
    }

    [Fact]
    public async Task TellPlayerWithVerification_Action_WithMessageListBody_ForwardsAllMessages()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        SetupValidServerAndRconConfig(gameServerId);

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient
            .Setup(x => x.GetPlayers())
            .Returns([
                CreateRconPlayer(7, "^1Alpha^7", "2310346613733334073")
            ]);

        var forwardedMessages = new List<string>();
        mockRconClient
            .Setup(x => x.TellPlayer(7, It.IsAny<string>()))
            .Callback<int, string>((_, message) => forwardedMessages.Add(message))
            .ReturnsAsync("ok");

        _mockRconClientFactory
            .Setup(x => x.CreateInstance(It.IsAny<GameType>(), gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var controller = CreateController();
        var requestBody = ParseJsonElement(/*lang=json,strict*/ "{\"messages\":[\" one \",\"two\"],\"expectedPlayerName\":\"Alpha\"}");

        // Act
        var result = await controller.TellPlayerWithVerification(gameServerId, 7, requestBody);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(["one", "two"], forwardedMessages);
    }

    [Fact]
    public async Task TellPlayerWithVerification_Action_WithMismatchedExpectedName_ReturnsBadRequest()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        SetupValidServerAndRconConfig(gameServerId);

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient
            .Setup(x => x.GetPlayers())
            .Returns([
                CreateRconPlayer(7, "^1Alpha^7", "2310346613733334073")
            ]);

        _mockRconClientFactory
            .Setup(x => x.CreateInstance(It.IsAny<GameType>(), gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var controller = CreateController();
        var requestBody = ParseJsonElement(/*lang=json,strict*/ "{\"message\":\"hello\",\"expectedPlayerName\":\"Beta\"}");

        // Act
        var result = await controller.TellPlayerWithVerification(gameServerId, 7, requestBody);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var responseBody = Assert.IsType<ApiResponse>(badRequest.Value);
        Assert.Equal(ErrorCodes.PLAYER_VERIFICATION_FAILED, responseBody.Errors?.SingleOrDefault()?.Code);
        mockRconClient.Verify(x => x.TellPlayer(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task TellPlayerWithVerification_WhenNormalizedNamesDiffer_ReturnsBadRequestWithVerificationCode()
    {
        // Arrange
        var gameServerId = Guid.NewGuid();
        SetupValidServerAndRconConfig(gameServerId);

        var mockRconClient = new Mock<IRconClient>();
        mockRconClient
            .Setup(x => x.GetPlayers())
            .Returns([
                CreateRconPlayer(3, "^2Totty>XI<Adm", "2310346613733334073")
            ]);

        _mockRconClientFactory
            .Setup(x => x.CreateInstance(It.IsAny<GameType>(), gameServerId, "127.0.0.1", 28960, "secret"))
            .Returns(mockRconClient.Object);

        var controller = CreateController();

        // Act
        var result = await ((IRconApi)controller)
            .TellPlayerWithVerification(gameServerId, 3, "hello", "DifferentPlayer");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(ErrorCodes.PLAYER_VERIFICATION_FAILED, result.Result?.Errors?.Single().Code);
        mockRconClient.Verify(x => x.TellPlayer(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
    }

    private static IRconPlayer CreateRconPlayer(int slot, string name, string guid)
    {
        var mockPlayer = new Mock<IRconPlayer>();
        mockPlayer.SetupProperty(x => x.Num, slot);
        mockPlayer.SetupProperty(x => x.Name, name);
        mockPlayer.SetupProperty(x => x.Guid, guid);
        mockPlayer.SetupProperty(x => x.IpAddress, "127.0.0.1");
        mockPlayer.SetupProperty(x => x.Ping, 50);
        mockPlayer.SetupProperty(x => x.Rate, 25000);

        return mockPlayer.Object;
    }

    private static JsonElement ParseJsonElement(string json)
    {
        return System.Text.Json.JsonSerializer.Deserialize<JsonElement>(json);
    }
}
