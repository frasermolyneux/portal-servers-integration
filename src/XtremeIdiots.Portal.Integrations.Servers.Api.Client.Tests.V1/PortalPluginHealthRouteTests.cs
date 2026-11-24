using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using MX.Api.Client;
using MX.Api.Client.Configuration;
using RestSharp;
using XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Tests.V1;

[Trait("Category", "Unit")]
public class PortalPluginHealthRouteTests
{
    [Fact]
    public async Task Health_UsesReadOnlyRouteAndReturnsRawOutput()
    {
        var service = new FakeRestClientService();
        var api = new CoD4xRconApi(Mock.Of<ILogger<BaseApi<ServersApiClientOptions>>>(), null, service,
            new ServersApiClientOptions { BaseUrl = "https://localhost" });
        var id = Guid.NewGuid();

        var response = await api.PortalPluginHealth(id);

        Assert.Equal($"v1/rcon/{id}/cod4x/plugins/portal-health", service.LastRequest!.Resource);
        Assert.Equal(Method.Get, service.LastRequest.Method);
        Assert.Equal("unknown command", response.Result!.Data);
    }

    private sealed class FakeRestClientService : IRestClientService
    {
        public RestRequest? LastRequest { get; private set; }

        public Task<RestResponse> ExecuteAsync(string baseUrl, RestRequest request, CancellationToken cancellationToken = default) =>
            Execute(request);

        public Task<RestResponse> ExecuteWithNamedOptionsAsync(string optionsName, RestRequest request, CancellationToken cancellationToken = default) =>
            Execute(request);

        private Task<RestResponse> Execute(RestRequest request)
        {
            LastRequest = request;
            return Task.FromResult(new RestResponse { StatusCode = HttpStatusCode.OK, Content = JsonSerializer.Serialize(new { data = "unknown command" }) });
        }

        public void Dispose() { }
    }
}
