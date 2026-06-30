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
public class GameScopedRconApiRouteTests
{
    [Fact]
    public async Task Cod2CvarList_UsesCod2CvarListRoute()
    {
        var restClientService = new FakeRestClientService();
        var api = CreateCod2Api(restClientService);
        var gameServerId = Guid.NewGuid();

        var result = await api.CvarList(gameServerId);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(restClientService.LastRequest);
        Assert.Equal($"v1/rcon/{gameServerId}/cod2/cvarlist", restClientService.LastRequest!.Resource);
        Assert.Equal(Method.Get, restClientService.LastRequest.Method);
    }

    [Fact]
    public async Task Cod2Seta_UsesCod2SetaRoute()
    {
        var restClientService = new FakeRestClientService();
        var api = CreateCod2Api(restClientService);
        var gameServerId = Guid.NewGuid();

        var result = await api.Seta(gameServerId, new SetDvarRequest
        {
            DvarName = "sv_hostname",
            Value = "XI Test"
        });

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(restClientService.LastRequest);
        Assert.Equal($"v1/rcon/{gameServerId}/cod2/seta", restClientService.LastRequest!.Resource);
        Assert.Equal(Method.Post, restClientService.LastRequest.Method);
        Assert.Single(restClientService.LastRequest.Parameters, p => p.Type == ParameterType.RequestBody);
    }

    [Fact]
    public async Task Cod2Tell_UsesCod2TellRoute()
    {
        var restClientService = new FakeRestClientService();
        var api = CreateCod2Api(restClientService);
        var gameServerId = Guid.NewGuid();

        var result = await api.Tell(gameServerId, new CoD4xTargetMessageRequestDto
        {
            Target = "2",
            Message = "test"
        });

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(restClientService.LastRequest);
        Assert.Equal($"v1/rcon/{gameServerId}/cod2/tell", restClientService.LastRequest!.Resource);
        Assert.Equal(Method.Post, restClientService.LastRequest.Method);
        Assert.Single(restClientService.LastRequest.Parameters, p => p.Type == ParameterType.RequestBody);
    }

    [Fact]
    public async Task Cod4DvarList_UsesCod4DvarListRoute()
    {
        var restClientService = new FakeRestClientService();
        var api = CreateCod4Api(restClientService);
        var gameServerId = Guid.NewGuid();

        var result = await api.DvarList(gameServerId);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(restClientService.LastRequest);
        Assert.Equal($"v1/rcon/{gameServerId}/cod4/dvarlist", restClientService.LastRequest!.Resource);
        Assert.Equal(Method.Get, restClientService.LastRequest.Method);
    }

    [Fact]
    public async Task Cod4Set_UsesCod4SetRoute()
    {
        var restClientService = new FakeRestClientService();
        var api = CreateCod4Api(restClientService);
        var gameServerId = Guid.NewGuid();

        var result = await api.Set(gameServerId, new SetDvarRequest
        {
            DvarName = "sv_maxclients",
            Value = "32"
        });

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(restClientService.LastRequest);
        Assert.Equal($"v1/rcon/{gameServerId}/cod4/set", restClientService.LastRequest!.Resource);
        Assert.Equal(Method.Post, restClientService.LastRequest.Method);
        Assert.Single(restClientService.LastRequest.Parameters, p => p.Type == ParameterType.RequestBody);
    }

    [Fact]
    public async Task Cod4Tell_UsesCod4TellRoute()
    {
        var restClientService = new FakeRestClientService();
        var api = CreateCod4Api(restClientService);
        var gameServerId = Guid.NewGuid();

        var result = await api.Tell(gameServerId, new CoD4xTargetMessageRequestDto
        {
            Target = "3",
            Message = "test"
        });

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(restClientService.LastRequest);
        Assert.Equal($"v1/rcon/{gameServerId}/cod4/tell", restClientService.LastRequest!.Resource);
        Assert.Equal(Method.Post, restClientService.LastRequest.Method);
        Assert.Single(restClientService.LastRequest.Parameters, p => p.Type == ParameterType.RequestBody);
    }

    [Fact]
    public async Task Cod5Status_UsesCod5StatusRoute()
    {
        var restClientService = new FakeRestClientService();
        var api = CreateCod5Api(restClientService);
        var gameServerId = Guid.NewGuid();

        var result = await api.Status(gameServerId);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(restClientService.LastRequest);
        Assert.Equal($"v1/rcon/{gameServerId}/cod5/status", restClientService.LastRequest!.Resource);
        Assert.Equal(Method.Get, restClientService.LastRequest.Method);
    }

    [Fact]
    public async Task Cod5Ban_UsesCod5BanRoute()
    {
        var restClientService = new FakeRestClientService();
        var api = CreateCod5Api(restClientService);
        var gameServerId = Guid.NewGuid();

        var result = await api.Ban(gameServerId, new ClientSlotRequest
        {
            ClientId = 4
        });

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(restClientService.LastRequest);
        Assert.Equal($"v1/rcon/{gameServerId}/cod5/ban", restClientService.LastRequest!.Resource);
        Assert.Equal(Method.Post, restClientService.LastRequest.Method);
        Assert.Single(restClientService.LastRequest.Parameters, p => p.Type == ParameterType.RequestBody);
    }

    [Fact]
    public async Task Cod5Tell_UsesCod5TellRoute()
    {
        var restClientService = new FakeRestClientService();
        var api = CreateCod5Api(restClientService);
        var gameServerId = Guid.NewGuid();

        var result = await api.Tell(gameServerId, new CoD4xTargetMessageRequestDto
        {
            Target = "4",
            Message = "test"
        });

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(restClientService.LastRequest);
        Assert.Equal($"v1/rcon/{gameServerId}/cod5/tell", restClientService.LastRequest!.Resource);
        Assert.Equal(Method.Post, restClientService.LastRequest.Method);
        Assert.Single(restClientService.LastRequest.Parameters, p => p.Type == ParameterType.RequestBody);
    }

    private static Cod2RconApi CreateCod2Api(FakeRestClientService restClientService)
    {
        return new Cod2RconApi(
            Mock.Of<ILogger<BaseApi<ServersApiClientOptions>>>(),
            null,
            restClientService,
            new ServersApiClientOptions
            {
                BaseUrl = "https://localhost"
            });
    }

    private static Cod4RconApi CreateCod4Api(FakeRestClientService restClientService)
    {
        return new Cod4RconApi(
            Mock.Of<ILogger<BaseApi<ServersApiClientOptions>>>(),
            null,
            restClientService,
            new ServersApiClientOptions
            {
                BaseUrl = "https://localhost"
            });
    }

    private static Cod5RconApi CreateCod5Api(FakeRestClientService restClientService)
    {
        return new Cod5RconApi(
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
