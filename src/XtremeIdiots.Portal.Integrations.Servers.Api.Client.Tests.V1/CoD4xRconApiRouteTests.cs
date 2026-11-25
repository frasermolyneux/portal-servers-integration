using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using MX.Api.Client;
using MX.Api.Client.Configuration;
using RestSharp;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Rcon;
using XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.RulesEngine;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Tests.V1;

[Trait("Category", "Unit")]
public class CoD4xRconApiRouteTests
{
    [Fact]
    public async Task RulesAction_UsesSharedCommandAndResult()
    {
        var id = Guid.NewGuid();
        var execution = Guid.NewGuid();
        var service = new FakeRestClientService
        {
            Content = JsonSerializer.Serialize(new { data = new { executionId = execution, outcome = "Dispatched" } })
        };
        var api = new RulesNativeActionApi(Mock.Of<ILogger<BaseApi<ServersApiClientOptions>>>(), null, service,
            new ServersApiClientOptions { BaseUrl = "https://localhost" });
        var result = await api.Execute(id, new RuleActionCommand { ExecutionId = execution });
        Assert.Equal($"v1/rcon/{id}/rules-action", service.LastRequest!.Resource);
        Assert.Equal(Method.Post, service.LastRequest.Method);
        Assert.Single(service.LastRequest.Parameters, p => p.Type == ParameterType.RequestBody);
        Assert.Equal(execution, result.Result!.Data!.ExecutionId);
        Assert.Equal(RuleActionExecutionOutcome.Dispatched, result.Result.Data.Outcome);
    }

    [Fact]
    public async Task RulesMessageBudget_UsesChannelQueryAndSharedBudget()
    {
        var id = Guid.NewGuid();
        var service = new FakeRestClientService
        {
            Content = /*lang=json,strict*/ """
                {"data":{"status":"Available","budget":{"channel":"rcon.chat.private","maxPayloadBytes":149,"reservedPrefixUtf8Bytes":20}}}
                """
        };
        var api = new RulesNativeActionApi(Mock.Of<ILogger<BaseApi<ServersApiClientOptions>>>(), null, service,
            new ServersApiClientOptions { BaseUrl = "https://localhost" });

        var result = await api.GetMessageChannelBudget(id, RuleMessageChannels.RconPrivateChat);

        Assert.Equal($"v1/rcon/{id}/rules-action/message-budget", service.LastRequest!.Resource);
        Assert.Equal(Method.Get, service.LastRequest.Method);
        Assert.Contains(service.LastRequest.Parameters, p => p.Name == "channel" && Equals(p.Value, RuleMessageChannels.RconPrivateChat));
        Assert.Equal(RuleMessageBudgetStatus.Available, result.Result!.Data!.Status);
        Assert.Equal(149, result.Result.Data.Budget!.MaxPayloadBytes);
        Assert.Equal(20, result.Result.Data.Budget.ReservedPrefixUtf8Bytes);
    }

    [Fact]
    public async Task RulesAction_DoesNotRetryAnAmbiguousTransportFailure()
    {
        var service = new FakeRestClientService { ThrowOnExecute = true };
        var api = new RulesNativeActionApi(Mock.Of<ILogger<BaseApi<ServersApiClientOptions>>>(), null, service,
            new ServersApiClientOptions { BaseUrl = "https://localhost" });
        await Assert.ThrowsAsync<HttpRequestException>(() => api.Execute(Guid.NewGuid(), new RuleActionCommand()));
        Assert.Equal(1, service.ExecutionCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task RulesAction_DoesNotRetryAnUnsuccessfulResponse(HttpStatusCode statusCode)
    {
        var service = new FakeRestClientService { StatusCode = statusCode };
        var api = new RulesNativeActionApi(Mock.Of<ILogger<BaseApi<ServersApiClientOptions>>>(), null, service,
            new ServersApiClientOptions { BaseUrl = "https://localhost" });

        await Assert.ThrowsAsync<HttpRequestException>(() => api.Execute(Guid.NewGuid(), new RuleActionCommand()));
        Assert.Equal(1, service.ExecutionCount);
    }

    [Fact]
    public async Task RulesAction_DoesNotRetryANullResponse()
    {
        var service = new FakeRestClientService { ReturnNull = true };
        var api = new RulesNativeActionApi(Mock.Of<ILogger<BaseApi<ServersApiClientOptions>>>(), null, service,
            new ServersApiClientOptions { BaseUrl = "https://localhost" });

        await Assert.ThrowsAsync<HttpRequestException>(() => api.Execute(Guid.NewGuid(), new RuleActionCommand()));
        Assert.Equal(1, service.ExecutionCount);
    }

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
    public async Task Say_UsesCod4xSayRoute()
    {
        var restClientService = new FakeRestClientService();
        var api = CreateApi(restClientService);
        var gameServerId = Guid.NewGuid();

        var result = await api.Say(gameServerId, new CoD4xMessageRequestDto
        {
            Message = "welcome to the server"
        });

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(restClientService.LastRequest);
        Assert.Equal($"v1/rcon/{gameServerId}/cod4x/say", restClientService.LastRequest!.Resource);
        Assert.Equal(Method.Post, restClientService.LastRequest.Method);
        Assert.Single(restClientService.LastRequest.Parameters, p => p.Type == ParameterType.RequestBody);
    }

    [Fact]
    public async Task Say_DoesNotRetryAmbiguousTransportFailure()
    {
        var service = new FakeRestClientService { ThrowOnExecute = true };
        var request = new CoD4xMessageRequestDto { Messages = ["first", "second"] };

        await Assert.ThrowsAsync<HttpRequestException>(() => CreateApi(service).Say(Guid.NewGuid(), request));

        Assert.Equal(1, service.ExecutionCount);
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
    public async Task Status_DeserializesAdditiveRoster()
    {
        var restClientService = new FakeRestClientService
        {
            Content = JsonSerializer.Serialize(new
            {
                data = new
                {
                    hostname = "Test",
                    players = new[] { new { num = 1, playerIdentifier = "123456" } },
                    roster = new { state = "Partial", players = new[] { new { slot = 1, playerIdentifier = "123456" } } }
                }
            })
        };
        var result = await CreateApi(restClientService).Status(Guid.NewGuid());

        Assert.Equal("Test", result.Result!.Data!.Hostname);
        Assert.Equal("123456", Assert.Single(result.Result.Data.Players).PlayerIdentifier);
        Assert.Equal(RconRosterState.Partial, result.Result.Data.Roster!.State);
        Assert.Equal("123456", Assert.Single(result.Result.Data.Roster.Players).PlayerIdentifier);
    }

    [Fact]
    public async Task GetMaps_UsesCod4xMapsRoute()
    {
        var restClientService = new FakeRestClientService();
        var api = CreateApi(restClientService);
        var gameServerId = Guid.NewGuid();

        var result = await api.GetMaps(gameServerId);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(restClientService.LastRequest);
        Assert.Equal($"v1/rcon/{gameServerId}/cod4x/maps", restClientService.LastRequest!.Resource);
        Assert.Equal(Method.Get, restClientService.LastRequest.Method);
    }

    [Fact]
    public async Task DumpBanList_UsesCod4xDumpBanListRoute()
    {
        var restClientService = new FakeRestClientService();
        var api = CreateApi(restClientService);
        var gameServerId = Guid.NewGuid();

        var result = await api.DumpBanList(gameServerId);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.NotNull(restClientService.LastRequest);
        Assert.Equal($"v1/rcon/{gameServerId}/cod4x/dumpbanlist", restClientService.LastRequest!.Resource);
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
        public string? Content { get; init; }
        public HttpStatusCode StatusCode { get; init; } = HttpStatusCode.OK;
        public bool ThrowOnExecute { get; init; }
        public bool ReturnNull { get; init; }
        public int ExecutionCount { get; private set; }

        public Task<RestResponse> ExecuteAsync(string baseUrl, RestRequest request, CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            if (ThrowOnExecute)
            {
                throw new HttpRequestException("Ambiguous transport failure");
            }
            if (ReturnNull)
            {
                return Task.FromResult<RestResponse>(null!);
            }
            LastRequest = request;
            return Task.FromResult(new RestResponse { StatusCode = StatusCode, Content = Content });
        }

        public Task<RestResponse> ExecuteWithNamedOptionsAsync(string optionsName, RestRequest request, CancellationToken cancellationToken = default)
        {
            ExecutionCount++;
            if (ThrowOnExecute)
            {
                throw new HttpRequestException("Ambiguous transport failure");
            }
            LastRequest = request;
            return Task.FromResult(new RestResponse { StatusCode = StatusCode, Content = Content });
        }

        public void Dispose()
        {
        }
    }
}
