using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using MX.Api.Client;
using MX.Api.Client.Configuration;
using RestSharp;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Files;
using XtremeIdiots.Portal.Integrations.Servers.Api.Client.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.Tests.V1;

[Trait("Category", "Unit")]
public class FilesApiRequestTests
{
    [Fact]
    public async Task PutContent_UsesPutRouteAndSerializesRequestBody()
    {
        var restClientService = new FakeRestClientService();
        var api = CreateApi(restClientService);
        var gameServerId = Guid.NewGuid();
        var request = new PutFileContentRequestDto
        {
            Path = "/cfg/server.cfg",
            TextContent = "set sv_hostname XI",
            Overwrite = false,
            CreateParentDirectories = true
        };

        var result = await api.PutContent(gameServerId, request);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        AssertRequest(restClientService.LastRequest, $"v1/files/{gameServerId}/content", Method.Put, request);
    }

    [Fact]
    public async Task CreateDirectory_UsesPostRouteAndSerializesRequestBody()
    {
        var restClientService = new FakeRestClientService();
        var api = CreateApi(restClientService);
        var gameServerId = Guid.NewGuid();
        var request = new CreateDirectoryRequestDto
        {
            Path = "/cfg/custom",
            CreateParents = true,
            IfNotExists = false
        };

        var result = await api.CreateDirectory(gameServerId, request);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        AssertRequest(restClientService.LastRequest, $"v1/files/{gameServerId}/directories", Method.Post, request);
    }

    [Fact]
    public async Task PatchEntry_UsesPatchRouteAndSerializesRequestBody()
    {
        var restClientService = new FakeRestClientService();
        var api = CreateApi(restClientService);
        var gameServerId = Guid.NewGuid();
        var request = new PatchFileEntryRequestDto
        {
            Operation = FileEntryPatchOperation.Move,
            SourcePath = "/cfg/source.cfg",
            DestinationPath = "/cfg/destination.cfg",
            Overwrite = true,
            CreateDestinationDirectories = true
        };

        var result = await api.PatchEntry(gameServerId, request);

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        AssertRequest(restClientService.LastRequest, $"v1/files/{gameServerId}/entries", Method.Patch, request);
    }

    [Fact]
    public async Task Mutations_PreserveErrorResponseStatus()
    {
        var restClientService = new FakeRestClientService
        {
            Response = new RestResponse { StatusCode = HttpStatusCode.BadRequest }
        };
        var api = CreateApi(restClientService);
        var gameServerId = Guid.NewGuid();

        var putException = await Assert.ThrowsAsync<HttpRequestException>(
            () => api.PutContent(gameServerId, new PutFileContentRequestDto()));
        var directoryException = await Assert.ThrowsAsync<HttpRequestException>(
            () => api.CreateDirectory(gameServerId, new CreateDirectoryRequestDto()));
        var patchException = await Assert.ThrowsAsync<HttpRequestException>(
            () => api.PatchEntry(gameServerId, new PatchFileEntryRequestDto()));

        Assert.Contains("BadRequest", putException.Message);
        Assert.Contains("BadRequest", directoryException.Message);
        Assert.Contains("BadRequest", patchException.Message);
    }

    private static void AssertRequest(RestRequest? request, string route, Method method, object body)
    {
        Assert.NotNull(request);
        Assert.Equal(route, request.Resource);
        Assert.Equal(method, request.Method);
        var bodyParameter = Assert.Single(request.Parameters, parameter => parameter.Type == ParameterType.RequestBody);
        Assert.Same(body, bodyParameter.Value);
    }

    private static FilesApi CreateApi(FakeRestClientService restClientService)
    {
        return new FilesApi(
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

        public RestResponse Response { get; init; } = new() { StatusCode = HttpStatusCode.OK };

        public Task<RestResponse> ExecuteAsync(string baseUrl, RestRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(Response);
        }

        public Task<RestResponse> ExecuteWithNamedOptionsAsync(string optionsName, RestRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(Response);
        }

        public void Dispose()
        {
        }
    }
}
