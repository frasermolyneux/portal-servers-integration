using System.Net;
using Microsoft.Extensions.Logging;
using MX.Api.Client;
using MX.Api.Client.Configuration;
using RestSharp;
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
