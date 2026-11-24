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

    [Fact]
    public async Task Health_PreservesObservedLegacyPluginOutput()
    {
        const string output = """
            [portal-cod4x-plugin] portalpluginhealth report
                     pluginVersion=0.2.35 loadedConfig=true gameServerId=de5ef248-1d5c-f111-8fcd-7ced8d3b16ff logLevel=info(2) gameType=CallOfDuty4x
                     banSyncStage=Idle nextBanSyncUtc=2026-11-24T15:11:17Z activeBanCacheCount=35 pendingBanCacheCount=0 banSyncInFlight=false
                     banEnforcementStatusChecks=141 statusHits=0 statusMisses=141 statusZeroPlayerIds=0 authenticatedChecks=141 authenticatedHits=0 authenticatedMisses=141 authenticatedZeroPlayerIds=0 proactiveDropAttempts=0
                     ingestStage=Idle bufferedEventCount=0 ingestFailureCount=0 nextIngestAttemptUtc=2026-11-24T15:08:54Z ingestInFlight=false
                     connectedPlayerCount=14 serverContextLastRefreshUtc=2026-11-24T05:58:25Z
                     lastTickUtc=2026-11-24T15:09:50Z secondsSinceLastTick=0
                     portalpluginhealthEnabled=true portalpluginhealthMinPower=98
            """;
        var service = new FakeRestClientService { Output = output };
        var api = new CoD4xRconApi(Mock.Of<ILogger<BaseApi<ServersApiClientOptions>>>(), null, service,
            new ServersApiClientOptions { BaseUrl = "https://localhost" });

        var response = await api.PortalPluginHealth(Guid.NewGuid());

        Assert.Equal(output, response.Result!.Data);
    }

    private sealed class FakeRestClientService : IRestClientService
    {
        public RestRequest? LastRequest { get; private set; }
        public string Output { get; init; } = "unknown command";

        public Task<RestResponse> ExecuteAsync(string baseUrl, RestRequest request, CancellationToken cancellationToken = default) =>
            Execute(request);

        public Task<RestResponse> ExecuteWithNamedOptionsAsync(string optionsName, RestRequest request, CancellationToken cancellationToken = default) =>
            Execute(request);

        private Task<RestResponse> Execute(RestRequest request)
        {
            LastRequest = request;
            return Task.FromResult(new RestResponse { StatusCode = HttpStatusCode.OK, Content = JsonSerializer.Serialize(new { data = Output }) });
        }

        public void Dispose() { }
    }
}
