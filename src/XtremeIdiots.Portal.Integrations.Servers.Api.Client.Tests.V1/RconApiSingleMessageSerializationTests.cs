using System.Net;
using Microsoft.Extensions.Logging;
using MX.Api.Client;
using MX.Api.Client.Configuration;
using RestSharp;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;
using XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Tests.V1;

[Trait("Category", "Unit")]
public class RconApiSingleMessageSerializationTests
{
    [Fact]
    public async Task Say_SingleMessage_SendsJsonStringRequestBody()
    {
        // Arrange
        var restClientService = new FakeRestClientService();
        var api = CreateApi(restClientService);
        var gameServerId = Guid.NewGuid();
        const string message = "hello world";

        // Act
        var result = await api.Say(gameServerId, message);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        Assert.NotNull(restClientService.LastRequest);
        Assert.Single(restClientService.LastRequest!.Parameters, p => p.Type == ParameterType.RequestBody);
        var bodyParameter = restClientService.LastRequest!.Parameters.FirstOrDefault(p => p.Type == ParameterType.RequestBody);
        Assert.NotNull(bodyParameter);
        Assert.Equal("\"hello world\"", bodyParameter.Value?.ToString());
    }

    [Fact]
    public async Task TellPlayer_SingleMessage_SendsJsonStringRequestBody()
    {
        // Arrange
        var restClientService = new FakeRestClientService();
        var api = CreateApi(restClientService);
        var gameServerId = Guid.NewGuid();
        const string message = "player message";

        // Act
        var result = await api.TellPlayer(gameServerId, 7, message);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        Assert.NotNull(restClientService.LastRequest);
        Assert.Single(restClientService.LastRequest!.Parameters, p => p.Type == ParameterType.RequestBody);
        var bodyParameter = restClientService.LastRequest!.Parameters.FirstOrDefault(p => p.Type == ParameterType.RequestBody);
        Assert.NotNull(bodyParameter);
        Assert.Equal("\"player message\"", bodyParameter.Value?.ToString());
    }

    [Fact]
    public async Task BanPlayerByPlayerIdentifier_UsesCod4xPermBanRoute()
    {
        // Arrange
        var restClientService = new FakeRestClientService();
        var api = CreateApi(restClientService);
        var gameServerId = Guid.NewGuid();

        // Act
        var result = await api.BanPlayerByPlayerIdentifier(gameServerId, new CoD4xPermBanRequestDto
        {
            PlayerIdentifier = "2310346615957836592"
        });

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        Assert.NotNull(restClientService.LastRequest);
        Assert.Equal($"v1/rcon/{gameServerId}/cod4x/permban", restClientService.LastRequest!.Resource);
        Assert.Single(restClientService.LastRequest!.Parameters, p => p.Type == ParameterType.RequestBody);
    }

    [Fact]
    public async Task TempBanPlayerByPlayerIdentifier_UsesCod4xTempBanRoute()
    {
        // Arrange
        var restClientService = new FakeRestClientService();
        var api = CreateApi(restClientService);
        var gameServerId = Guid.NewGuid();

        // Act
        var result = await api.TempBanPlayerByPlayerIdentifier(gameServerId, new CoD4xTempBanRequestDto
        {
            PlayerIdentifier = "2310346615957836592",
            DurationMinutes = 15
        });

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        Assert.NotNull(restClientService.LastRequest);
        Assert.Equal($"v1/rcon/{gameServerId}/cod4x/tempban", restClientService.LastRequest!.Resource);
        Assert.Single(restClientService.LastRequest!.Parameters, p => p.Type == ParameterType.RequestBody);
    }

    [Fact]
    public async Task UnbanPlayerByPlayerIdentifier_UsesCod4xUnbanRoute()
    {
        // Arrange
        var restClientService = new FakeRestClientService();
        var api = CreateApi(restClientService);
        var gameServerId = Guid.NewGuid();

        // Act
        var result = await api.UnbanPlayerByPlayerIdentifier(gameServerId, new CoD4xUnbanRequestDto
        {
            PlayerIdentifier = "2310346615957836592"
        });

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, result.StatusCode);
        Assert.NotNull(restClientService.LastRequest);
        Assert.Equal($"v1/rcon/{gameServerId}/cod4x/unban", restClientService.LastRequest!.Resource);
        Assert.Single(restClientService.LastRequest!.Parameters, p => p.Type == ParameterType.RequestBody);
    }

    private static RconApi CreateApi(FakeRestClientService restClientService)
    {
        return new RconApi(
            Mock.Of<ILogger<BaseApi<ServersApiClientOptions>>>(),
            null,
            restClientService,
            new ServersApiClientOptions
            {
                BaseUrl = "https://localhost"
            });
    }

    private sealed class FakeRestClientService : IRestClientService
    {
        public RestRequest? LastRequest { get; private set; }

        public Task<RestResponse> ExecuteAsync(string baseUrl, RestRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new RestResponse
            {
                StatusCode = HttpStatusCode.NoContent,
            });
        }

        public Task<RestResponse> ExecuteWithNamedOptionsAsync(string optionsName, RestRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new RestResponse
            {
                StatusCode = HttpStatusCode.NoContent,
            });
        }

        public void Dispose()
        {
        }
    }
}
