using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using MX.Api.Client;
using MX.Api.Client.Configuration;
using RestSharp;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;
using XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Tests.V1;

[Trait("Category", "Unit")]
public class CoD4xRconApiRouteTests
{
    [Fact]
    public async Task PermBan_UsesCod4xPermBanRoute()
    {
        var restClientService = new FakeRestClientService();
        var api = CreateApi(restClientService);
        var gameServerId = Guid.NewGuid();

        var result = await api.PermBan(gameServerId, new CoD4xPermBanRequestDto
        {
            PlayerIdentifier = "2310346615957836592"
        });

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(restClientService.LastRequest);
        Assert.Equal($"v1/rcon/{gameServerId}/cod4x/permban", restClientService.LastRequest!.Resource);
        Assert.Single(restClientService.LastRequest.Parameters, p => p.Type == ParameterType.RequestBody);
    }

    [Fact]
    public async Task Status_UsesCod4xStatusRoute()
    {
        var restClientService = new FakeRestClientService();
        var api = CreateApi(restClientService);
        var gameServerId = Guid.NewGuid();

        var result = await api.Status(gameServerId);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(restClientService.LastRequest);
        Assert.Equal($"v1/rcon/{gameServerId}/cod4x/status", restClientService.LastRequest!.Resource);
        Assert.Equal(Method.Get, restClientService.LastRequest.Method);
    }

    [Fact]
    public async Task AdminChangePassword_UsesCod4xAdminRoute()
    {
        var restClientService = new FakeRestClientService();
        var api = CreateApi(restClientService);
        var gameServerId = Guid.NewGuid();

        var result = await api.AdminChangePassword(gameServerId, new CoD4xAdminChangePasswordRequestDto
        {
            User = "admin",
            NewPassword = "new-pass"
        });

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(restClientService.LastRequest);
        Assert.Equal($"v1/rcon/{gameServerId}/cod4x/admin/change-password", restClientService.LastRequest!.Resource);
        Assert.Single(restClientService.LastRequest.Parameters, p => p.Type == ParameterType.RequestBody);
    }

    private static CoD4xRconApi CreateApi(FakeRestClientService restClientService)
    {
        return new CoD4xRconApi(
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
            return Task.FromResult(new RestResponse { StatusCode = HttpStatusCode.OK });
        }

        public Task<RestResponse> ExecuteWithNamedOptionsAsync(string optionsName, RestRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new RestResponse { StatusCode = HttpStatusCode.OK });
        }

        public void Dispose()
        {
        }
    }
}
