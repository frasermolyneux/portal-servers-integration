using System.Net;
using System.Net.Http.Json;

namespace XtremeIdiots.Portal.Integrations.Servers.Api.Client.IntegrationTests.V1;

[Trait("Category", "Integration")]
public class InfoAndHealthTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public InfoAndHealthTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetInfo_ReturnsOkWithVersionInfo()
    {
        var response = await _client.GetAsync("/v1.0/info");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Version", content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetHealth_ReturnsResponse()
    {
        var response = await _client.GetAsync("/v1.0/health");

        // Health may be 200 or 503 depending on health check dependencies,
        // but it should respond (not 404 or 500)
        Assert.True(
            response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.ServiceUnavailable,
            $"Expected 200 or 503, got {response.StatusCode}");
    }
}
