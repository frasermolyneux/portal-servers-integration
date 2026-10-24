using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MX.Api.Abstractions;
using Newtonsoft.Json;
using XtremeIdiots.Portal.Integrations.Servers.Api.V1.Helpers;
using XtremeIdiots.Portal.Integrations.Servers.Abstractions.Models.V1.Maps;
using XtremeIdiots.Portal.Repository.Abstractions.Constants.V1;
using XtremeIdiots.Portal.Repository.Abstractions.Models.V1.GameServers;
using XtremeIdiots.Redirect.Api.Abstractions.V1;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.IntegrationTests.V1;

[Trait("Category", "Integration")]
public class MapsVerificationEndpointTests : IClassFixture<MapsVerificationWebApplicationFactory>, IAsyncLifetime
{
    private static readonly string[] MapNames = ["mp_custom"];

    private readonly MapsVerificationWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public MapsVerificationEndpointTests(MapsVerificationWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync()
    {
        _factory.ResetMocks();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task VerifyMaps_WhenRedirectFilesArePresent_ReturnsSuccessfulVerification()
    {
        var gameServerId = Guid.NewGuid();
        var gameServer = JsonConvert.DeserializeObject<GameServerDto>(JsonConvert.SerializeObject(new
        {
            GameServerId = gameServerId,
            Title = "Server",
            GameType = (int)GameType.CallOfDuty4,
            Hostname = "127.0.0.1",
            QueryPort = 28960,
            FileTransportEnabled = true,
            FileTransportType = FileTransportType.Sftp,
            FtpEnabled = true
        }))!;
        _factory.MockRepositoryApiClient
            .Setup(x => x.GameServers.V1.GetGameServer(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<GameServerDto>(HttpStatusCode.OK, new ApiResponse<GameServerDto>(gameServer)));

        var session = new Mock<IGameServerFileTransportSession>();
        session.SetupGet(x => x.Transport).Returns(new ResolvedFileTransport(FileTransportType.Sftp, "sftp",
            new FileTransportCredentials("sftp-host", 22, "demo", "secret", "aa:bb")));
        session.Setup(x => x.DirectoryExists("usermaps/mp_custom", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        session.Setup(x => x.GetListing("usermaps/mp_custom", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new FileTransportEntry("mp_custom.ff", "usermaps/mp_custom/mp_custom.ff", false, 3, null)]);
        _factory.MockGameServerFileTransportFactory
            .Setup(x => x.CreateSession(gameServerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<IGameServerFileTransportSession>(HttpStatusCode.OK,
                new ApiResponse<IGameServerFileTransportSession>(session.Object)));

        var file = new ContentEntry("redirect/cod4/usermaps/mp_custom/mp_custom.ff", "mp_custom.ff", false, 3,
            DateTimeOffset.UtcNow, "current-version", "https://untrusted.invalid/not-a-download-source");
        var manifest = new MapManifest("cod4", "mp_custom", "redirect/cod4/usermaps/mp_custom", [file], 3, "catalogue-version");
        _factory.MockRedirectApiClient
            .Setup(x => x.Manifest("cod4", "mp_custom", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ApiResult<MapManifest>(HttpStatusCode.OK, new ApiResponse<MapManifest>(manifest)));

        var response = await _client.PostAsJsonAsync($"/v1.0/maps/{gameServerId}/host/verify", new { mapNames = MapNames });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var apiResponse = JsonConvert.DeserializeObject<ApiResponse<MapVerificationCollectionDto>>(
            await response.Content.ReadAsStringAsync());
        Assert.True(Assert.Single(apiResponse!.Data!.Items).ExistsOnServer);
    }
}

public class MapsVerificationWebApplicationFactory : CustomWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IGameServerFileTransportFactory>();
            services.RemoveAll<IRedirectApiClient>();
            services.RemoveAll<IRedirectDownloadClient>();
            services.AddSingleton(MockGameServerFileTransportFactory.Object);
            services.AddSingleton(MockRedirectApiClient.Object);
            services.AddSingleton(MockRedirectDownloadClient.Object);
        });
    }
}
